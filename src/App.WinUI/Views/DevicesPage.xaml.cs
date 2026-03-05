using App.WinUI.Models.Apps;
using App.WinUI.Infrastructure.Serial;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Devices.Onboarding;
using App.WinUI.Services.Firmware;
using App.WinUI.ViewModels;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Output.Led;
using System.Globalization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
public sealed partial class DevicesPage : Page
{
    private readonly List<DeviceListItem> allItems = new();
    private readonly List<DeviceListItem> visibleItems = new();
    private readonly Dictionary<string, DeviceListVisualItem> renderedItemsByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> renderedOrder = new();
    private readonly Dictionary<string, AppCatalogItem> appCatalogById = new(StringComparer.OrdinalIgnoreCase);
    private readonly DevicesPageViewModel viewModel;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly PrecompiledFirmwareService firmwareService;
    private readonly ISerialPortCatalogService serialPortCatalogService;
    private readonly IDeviceUsbOnboardingService onboardingService;
    private readonly IAppCatalogService appCatalogService;
    private readonly SettingsRepository settingsRepository;
    private readonly AppSettingsDomainService settingsDomainService;
    private readonly SimulatorLedOutput simulatorLedOutput;
    private readonly Dictionary<string, Queue<int>> loopTrendByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset?> lastLoopTrendStampByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> lastLoopTrendValueByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private const int SafeBrightnessMin = 30;
    private const int SafeBrightnessMax = 160;
    private const int DashboardTrendSampleCapacity = 24;

    private string? lastRenderedDashboardSignature;
    private string? lastRenderedDeviceLogsDeviceId;
    private int lastRenderedDeviceLogCount;
    private string lastRenderedDeviceLogTail = string.Empty;
    private string? lastRenderedDeviceLogsHeader;
    private string? lastRenderedDeviceLogsPlaceholder;
    private DeviceOperationsState currentState = new();
    private bool appCatalogLoadAttempted;
    private DeviceLifecycleThresholds lifecycleThresholds = DeviceLifecycleThresholds.Default;
    private bool isApplyingDeviceList;
    private IReadOnlyList<DeviceSnapshot>? pendingDeviceListSnapshot;
    private string? lastAppliedDeviceListSignature;
    private DispatcherQueueTimer? previewPumpTimer;
    private bool suppressBrightnessSliderEvents;
    private bool brightnessCommitPending;

    internal DevicesPage(
        DevicesPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        PrecompiledFirmwareService firmwareService,
        ISerialPortCatalogService serialPortCatalogService,
        IDeviceUsbOnboardingService onboardingService,
        IAppCatalogService appCatalogService,
        SettingsRepository settingsRepository,
        AppSettingsDomainService settingsDomainService,
        SimulatorLedOutput simulatorLedOutput)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.firmwareService = firmwareService;
        this.serialPortCatalogService = serialPortCatalogService;
        this.onboardingService = onboardingService;
        this.appCatalogService = appCatalogService;
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;
        this.simulatorLedOutput = simulatorLedOutput;
        this.viewModel.ConfigureCommands(
            refresh: () => DeviceOps?.RequestRefresh(),
            generatePairing: GeneratePairingCodeCore);

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => deviceOps;

    private PrecompiledFirmwareService? FirmwareService => firmwareService;

    private ISerialPortCatalogService? SerialPortCatalogService => serialPortCatalogService;

    private IDeviceUsbOnboardingService? OnboardingService => onboardingService;

    private IAppCatalogService? AppCatalogService => appCatalogService;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            ApplyDashboard(selectionDeviceId: null, hasSelection: false, snapshot: null, DeviceMetricsFormatter.Build(null));
            UpdateDeviceLogs(deviceId: null, entries: Array.Empty<string>(), placeholder: "Servico de dispositivos indisponivel.");
            return;
        }

        DeviceOps.StateChanged += OnDeviceOpsStateChanged;
        DeviceOps.DeviceListChanged += OnDeviceOpsDeviceListChanged;
        DeviceOps.SetDevicesPageVisible(true);
        DeviceOps.RequestRefresh();

        await EnsureAppCatalogLoadedAsync().ConfigureAwait(true);
        await EnsureLifecycleThresholdsLoadedAsync().ConfigureAwait(true);

        var initialState = DeviceOps.GetStateSnapshot();
        ApplyDevices(initialState.DeviceListSnapshot);
        ApplyState(initialState);

        StartPreviewPump();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopPreviewPump();

        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
            DeviceOps.DeviceListChanged -= OnDeviceOpsDeviceListChanged;
            DeviceOps.SetDevicesPageVisible(false);
        }

        lastRenderedDashboardSignature = null;
        lastRenderedDeviceLogsDeviceId = null;
        lastRenderedDeviceLogCount = 0;
        lastRenderedDeviceLogTail = string.Empty;
        lastRenderedDeviceLogsHeader = null;
        lastRenderedDeviceLogsPlaceholder = null;
        loopTrendByDeviceId.Clear();
        lastLoopTrendStampByDeviceId.Clear();
        lastLoopTrendValueByDeviceId.Clear();
        suppressBrightnessSliderEvents = false;
        brightnessCommitPending = false;
        ClearRenderedItems();
    }

    private async Task EnsureAppCatalogLoadedAsync()
    {
        if (appCatalogLoadAttempted)
        {
            return;
        }

        appCatalogLoadAttempted = true;

        var service = AppCatalogService;
        if (service is null)
        {
            return;
        }

        try
        {
            var catalog = await service.LoadCatalogAsync().ConfigureAwait(true);
            appCatalogById.Clear();
            foreach (var item in catalog)
            {
                appCatalogById[item.Id] = item;
            }
        }
        catch (Exception ex)
        {
            AddLocalLog($"Falha ao carregar catalogo de apps para preview: {ex.Message}");
        }
    }

    private async Task EnsureLifecycleThresholdsLoadedAsync()
    {
        try
        {
            var settings = settingsDomainService.Migrate(await settingsRepository.LoadAsync().ConfigureAwait(true));
            lifecycleThresholds = DeviceLifecycleThresholds.FromSettings(settings);
        }
        catch (Exception ex)
        {
            lifecycleThresholds = DeviceLifecycleThresholds.Default;
            AddLocalLog($"Falha ao carregar thresholds de presence: {ex.Message}");
        }
    }

    private void OnDeviceOpsStateChanged(object? sender, EventArgs e)
    {
        var ops = DeviceOps;
        if (ops is null)
        {
            return;
        }

        var snapshot = ops.GetStateSnapshot();
        _ = DispatcherQueue.TryEnqueue(() => ApplyState(snapshot));
    }

    private void OnDeviceOpsDeviceListChanged(object? sender, EventArgs e)
    {
        var ops = DeviceOps;
        if (ops is null)
        {
            return;
        }

        var snapshot = ops.GetStateSnapshot();
        _ = DispatcherQueue.TryEnqueue(() => ApplyDevices(snapshot.DeviceListSnapshot));
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        viewModel.RefreshCommand.Execute(null);
    }

    private void OnGeneratePairingCodeClicked(object sender, RoutedEventArgs e)
    {
        viewModel.GeneratePairingCommand.Execute(null);
    }

    private void GeneratePairingCodeCore()
    {
        if (DeviceOps is null)
        {
            return;
        }

        var code = DeviceOps.CreatePairingCode(TimeSpan.FromMinutes(10));
        PairingCodeText.Severity = InfoBarSeverity.Informational;
        PairingCodeText.Message = $"Pareamento: {code.Code} (expira {code.ExpiresAtUtc:HH:mm:ss} UTC)";
        AddLocalLog($"Codigo de pareamento gerado: {code.Code}.");
    }

    private async void OnTestLedClicked(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedDeviceItem();
        var ops = DeviceOps;
        if (selected is null || ops is null)
        {
            return;
        }

        var result = await ops.TriggerTestLedAsync(selected.DeviceId).ConfigureAwait(false);
        if (result.Accepted && result.Completed && result.Success)
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(result.Message) ? result.ErrorCode : result.Message;
        AddLocalLog($"Falha ao acionar teste de LED em {selected.DeviceId}: {reason ?? "erro desconhecido"}");
    }

    private void OnBrightnessSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressBrightnessSliderEvents)
        {
            return;
        }

        var normalized = Math.Clamp((int)Math.Round(e.NewValue), SafeBrightnessMin, SafeBrightnessMax);
        DashboardBrightnessValueText.Text = $"{normalized}/255";
        brightnessCommitPending = true;
    }

    private async void OnBrightnessSliderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        await CommitBrightnessIfPendingAsync().ConfigureAwait(false);
    }

    private async void OnBrightnessSliderLostFocus(object sender, RoutedEventArgs e)
    {
        await CommitBrightnessIfPendingAsync().ConfigureAwait(false);
    }

    private async void OnRemoveDeviceClicked(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedDeviceItem();
        var ops = DeviceOps;
        if (selected is null || ops is null)
        {
            return;
        }

        var isOnline = selected.Status == DeviceStatus.Online;
        var removePrompt = isOnline
            ? "O dispositivo esta online: o app tentara revogar/reiniciar e depois remover o registro local."
            : "O dispositivo esta offline: sera removido apenas do registro local.";

        var dialog = new ContentDialog
        {
            Title = "Remover dispositivo",
            PrimaryButtonText = "Remover",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            Content = removePrompt,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var revokeSucceeded = false;
        string? revokeFailureMessage = null;
        if (isOnline)
        {
            var revokeResult = await ops.RunCommandAsync(selected.DeviceId, DeviceCommandType.RevokeAndRestart);
            revokeSucceeded = revokeResult.Accepted && revokeResult.Completed && revokeResult.Success;
            if (!revokeSucceeded)
            {
                revokeFailureMessage = string.IsNullOrWhiteSpace(revokeResult.Message)
                    ? revokeResult.ErrorCode
                    : revokeResult.Message;
            }
        }

        if (ops.RemoveDevice(selected.DeviceId))
        {
            DevicesList.SelectedItem = null;

            if (isOnline)
            {
                if (revokeSucceeded)
                {
                    PairingCodeText.Severity = InfoBarSeverity.Success;
                    PairingCodeText.Message = $"Dispositivo revogado e removido: {selected.DeviceId}";
                    AddLocalLog($"Dispositivo revogado e removido localmente: {selected.DeviceId}");
                }
                else
                {
                    PairingCodeText.Severity = InfoBarSeverity.Warning;
                    PairingCodeText.Message = $"Dispositivo removido localmente; revogacao nao concluida: {selected.DeviceId}";
                    AddLocalLog($"Dispositivo removido localmente, mas revogacao falhou: {selected.DeviceId} ({revokeFailureMessage ?? "erro desconhecido"})");
                }
            }
            else
            {
                PairingCodeText.Severity = InfoBarSeverity.Success;
                PairingCodeText.Message = $"Dispositivo removido: {selected.DeviceId}";
                AddLocalLog($"Dispositivo removido localmente: {selected.DeviceId}");
            }

            ApplySelectionDetails();
            ApplyButtonState();
            return;
        }

        PairingCodeText.Severity = InfoBarSeverity.Error;
        PairingCodeText.Message = "Falha ao remover dispositivo.";
        AddLocalLog($"Falha ao remover dispositivo: {selected.DeviceId}");
    }

    private void OnCopyHostClicked(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(currentState.ServerBaseAddress);
        Clipboard.SetContent(data);
    }

    private async void OnDownloadFirmwareClicked(object sender, RoutedEventArgs e)
    {
        await SaveFirmwareAsync().ConfigureAwait(false);
    }

    private async void OnNewDeviceClicked(object sender, RoutedEventArgs e)
    {
        await ShowNewDeviceWizardAsync().ConfigureAwait(false);
    }

    private async Task ShowNewDeviceWizardAsync()
    {
        var serialCatalog = SerialPortCatalogService;
        var onboarding = OnboardingService;
        if (serialCatalog is null || onboarding is null)
        {
            AddLocalLog("Fluxo de onboarding indisponivel: servicos nao resolvidos.");
            return;
        }

        var cardBorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81));
        var cardBackground = ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 18, 24, 32));
        var cardElevatedBackground = ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42));
        var activeStepBrush = ResolveBrush("AppAccentBrush", Color.FromArgb(255, 44, 180, 255));
        var inactiveStepBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 62, 73, 92));

        var headerTitle = new TextBlock
        {
            Text = "Novo dispositivo",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 21,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var headerSubtitle = new TextBlock
        {
            Text = "Etapa 1 de 2",
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Cascadia Mono"),
        };

        var closeButton = new Button
        {
            Content = new SymbolIcon(Symbol.Cancel),
            Width = 38,
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var headerGrid = new Grid { ColumnSpacing = 10 };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(headerTitle);
        Grid.SetColumn(headerSubtitle, 1);
        headerGrid.Children.Add(headerSubtitle);
        Grid.SetColumn(closeButton, 2);
        headerGrid.Children.Add(closeButton);

        var progressStepOne = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(4),
            Background = activeStepBrush,
        };
        var progressStepTwo = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(4),
            Background = inactiveStepBrush,
        };

        var progressGrid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 6, 0, 4) };
        progressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progressGrid.Children.Add(progressStepOne);
        Grid.SetColumn(progressStepTwo, 1);
        progressGrid.Children.Add(progressStepTwo);

        var stepTitleText = new TextBlock
        {
            Text = "Rede Wi-Fi (SSID)",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };

        var stepDescriptionText = new TextBlock
        {
            Text = "Informe SSID e senha para prosseguir com a configuracao.",
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };

        var wifiSsidBox = new TextBox
        {
            Header = "Rede Wi-Fi (SSID)",
            PlaceholderText = "Nome da rede",
        };
        var wifiPasswordBox = new PasswordBox
        {
            Header = "Senha Wi-Fi",
            PlaceholderText = "Senha",
        };

        var wifiStepPanel = new StackPanel { Spacing = 8 };
        wifiStepPanel.Children.Add(wifiSsidBox);
        wifiStepPanel.Children.Add(wifiPasswordBox);

        var portsComboBox = new ComboBox
        {
            Header = "Porta USB (COM)",
            MinWidth = 340,
            DisplayMemberPath = nameof(SerialPortDescriptor.Label),
        };
        var includeAllPortsCheckBox = new CheckBox
        {
            Content = "Mostrar todas as portas",
        };
        var refreshPortsButton = new Button
        {
            Content = "Atualizar portas",
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var serialActionsGrid = new Grid { ColumnSpacing = 10 };
        serialActionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        serialActionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        serialActionsGrid.Children.Add(includeAllPortsCheckBox);
        Grid.SetColumn(refreshPortsButton, 1);
        serialActionsGrid.Children.Add(refreshPortsButton);

        var serialStepPanel = new StackPanel
        {
            Spacing = 10,
            Visibility = Visibility.Collapsed,
        };
        serialStepPanel.Children.Add(portsComboBox);
        serialStepPanel.Children.Add(serialActionsGrid);

        var statusText = new TextBlock
        {
            Text = "Preencha os dados para iniciar.",
            Opacity = 0.84,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };

        var bodyStack = new StackPanel { Spacing = 10 };
        bodyStack.Children.Add(progressGrid);
        bodyStack.Children.Add(stepTitleText);
        bodyStack.Children.Add(wifiStepPanel);
        bodyStack.Children.Add(serialStepPanel);
        bodyStack.Children.Add(stepDescriptionText);
        bodyStack.Children.Add(statusText);

        var backButton = new Button
        {
            Content = "Voltar",
            IsEnabled = false,
            MinWidth = 96,
        };
        var nextButton = new Button
        {
            Content = "Proximo",
            MinWidth = 144,
        };

        var footerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
        };
        footerActions.Children.Add(backButton);
        footerActions.Children.Add(nextButton);

        var dialogGrid = new Grid();
        dialogGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dialogGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerCard = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = cardElevatedBackground,
            BorderBrush = cardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10, 10, 0, 0),
            Child = headerGrid,
        };
        dialogGrid.Children.Add(headerCard);

        var bodyCard = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = cardBackground,
            BorderBrush = cardBorderBrush,
            BorderThickness = new Thickness(1, 0, 1, 0),
            Child = bodyStack,
        };
        Grid.SetRow(bodyCard, 1);
        dialogGrid.Children.Add(bodyCard);

        var footerCard = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = cardElevatedBackground,
            BorderBrush = cardBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0, 0, 10, 10),
            Child = footerActions,
        };
        Grid.SetRow(footerCard, 2);
        dialogGrid.Children.Add(footerCard);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Content = dialogGrid,
            DefaultButton = ContentDialogButton.None,
            FullSizeDesired = false,
        };

        var currentStep = 0;
        var operationInFlight = false;

        async Task RefreshPortsAsync(bool includeAllPorts)
        {
            var selectedPort = (portsComboBox.SelectedItem as SerialPortDescriptor)?.PortName;
            var ports = await serialCatalog.ListAsync(includeAllPorts).ConfigureAwait(true);
            portsComboBox.ItemsSource = ports;
            if (!string.IsNullOrWhiteSpace(selectedPort))
            {
                portsComboBox.SelectedItem = ports.FirstOrDefault(item =>
                    string.Equals(item.PortName, selectedPort, StringComparison.OrdinalIgnoreCase));
            }

            if (portsComboBox.SelectedIndex < 0 && ports.Count > 0)
            {
                portsComboBox.SelectedIndex = 0;
            }

            statusText.Text = ports.Count == 0
                ? "Nenhuma porta COM detectada. Conecte o ESP32 e atualize a lista."
                : $"Portas detectadas: {ports.Count}.";
        }

        void ApplyStepUi()
        {
            var stepOne = currentStep == 0;
            headerSubtitle.Text = stepOne ? "Etapa 1 de 2" : "Etapa 2 de 2";
            stepTitleText.Text = stepOne ? "Rede Wi-Fi (SSID)" : "Conexao USB (COM)";
            stepDescriptionText.Text = stepOne
                ? "Informe SSID e senha para prosseguir com a configuracao."
                : "Selecione a porta COM para flash + provisionamento automatico.";
            wifiStepPanel.Visibility = stepOne ? Visibility.Visible : Visibility.Collapsed;
            serialStepPanel.Visibility = stepOne ? Visibility.Collapsed : Visibility.Visible;
            progressStepOne.Background = activeStepBrush;
            progressStepTwo.Background = stepOne ? inactiveStepBrush : activeStepBrush;

            backButton.IsEnabled = !stepOne && !operationInFlight;
            nextButton.Content = stepOne
                ? "Proximo"
                : operationInFlight
                    ? "Processando..."
                    : "Iniciar onboarding";
        }

        void ApplyBusyState(bool busy)
        {
            operationInFlight = busy;
            closeButton.IsEnabled = !busy;
            backButton.IsEnabled = !busy && currentStep > 0;
            nextButton.IsEnabled = !busy;
            wifiSsidBox.IsEnabled = !busy;
            wifiPasswordBox.IsEnabled = !busy;
            portsComboBox.IsEnabled = !busy;
            includeAllPortsCheckBox.IsEnabled = !busy;
            refreshPortsButton.IsEnabled = !busy;
            ApplyStepUi();
        }

        includeAllPortsCheckBox.Checked += async (_, _) => await RefreshPortsAsync(includeAllPorts: true).ConfigureAwait(true);
        includeAllPortsCheckBox.Unchecked += async (_, _) => await RefreshPortsAsync(includeAllPorts: false).ConfigureAwait(true);
        refreshPortsButton.Click += async (_, _) => await RefreshPortsAsync(includeAllPortsCheckBox.IsChecked == true).ConfigureAwait(true);

        closeButton.Click += (_, _) =>
        {
            if (!operationInFlight)
            {
                dialog.Hide();
            }
        };

        backButton.Click += (_, _) =>
        {
            if (operationInFlight || currentStep == 0)
            {
                return;
            }

            currentStep = 0;
            statusText.Text = "Revise SSID e senha para continuar.";
            ApplyStepUi();
        };

        nextButton.Click += async (_, _) =>
        {
            if (operationInFlight)
            {
                return;
            }

            if (currentStep == 0)
            {
                if (string.IsNullOrWhiteSpace(wifiSsidBox.Text))
                {
                    statusText.Text = "Informe o SSID antes de continuar.";
                    return;
                }

                currentStep = 1;
                ApplyStepUi();
                await RefreshPortsAsync(includeAllPorts: false).ConfigureAwait(true);
                return;
            }

            var selectedPort = portsComboBox.SelectedItem as SerialPortDescriptor;
            if (selectedPort is null)
            {
                statusText.Text = "Selecione uma porta COM valida para continuar.";
                return;
            }

            ApplyBusyState(true);

            var progress = new Progress<DeviceOnboardingProgress>(update =>
            {
                statusText.Text = $"[{DescribeOnboardingStage(update.Stage)}] {update.Message}";
            });

            DeviceOnboardingResult result;
            try
            {
                result = await onboarding.RunAsync(
                    new DeviceOnboardingRequest
                    {
                        Ssid = wifiSsidBox.Text.Trim(),
                        Password = wifiPasswordBox.Password,
                        PortName = selectedPort.PortName,
                    },
                    progress).ConfigureAwait(true);
            }
            finally
            {
                wifiPasswordBox.Password = string.Empty;
            }

            if (result.Success)
            {
                PairingCodeText.Severity = InfoBarSeverity.Success;
                PairingCodeText.Message = $"Novo dispositivo pronto: {result.DeviceId}";
                statusText.Text = result.Message;
                DeviceOps?.RequestRefresh();
                dialog.Hide();
                return;
            }

            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = $"Onboarding falhou: {result.Message}";
            statusText.Text = $"Falha ({result.ErrorCode ?? "erro"}): {result.Message}";
            ApplyBusyState(false);
        };

        ApplyStepUi();
        await dialog.ShowAsync();
    }

    private static string DescribeOnboardingStage(DeviceOnboardingStage stage)
    {
        return stage switch
        {
            DeviceOnboardingStage.Flashing => "Flashing",
            DeviceOnboardingStage.Provisioning => "Provisionando",
            DeviceOnboardingStage.Pairing => "Pareando",
            DeviceOnboardingStage.Verifying => "Verificando",
            DeviceOnboardingStage.Done => "Concluido",
            DeviceOnboardingStage.Failed => "Falha",
            DeviceOnboardingStage.SelectPort => "Selecionando porta",
            DeviceOnboardingStage.InputWifi => "Wi-Fi",
            _ => "Onboarding",
        };
    }

    private async Task SaveFirmwareAsync()
    {
        var service = FirmwareService;
        if (service is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: servico indisponivel.";
            AddLocalLog("Servico de firmware indisponivel.");
            return;
        }

        var option = service.GetOptions().FirstOrDefault();
        if (option is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: opcao indisponivel.";
            AddLocalLog("Nenhuma opcao de firmware disponivel.");
            return;
        }

        if (!service.TryResolveSource(option.Id, out _, out var resolveError))
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: arquivo ausente.";
            AddLocalLog(resolveError);
            return;
        }

        StorageFile? targetFile;
        try
        {
            targetFile = await PickFirmwareDestinationFileAsync(option).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: erro ao abrir seletor.";
            AddLocalLog($"Falha ao abrir seletor de arquivo: {ex.Message}");
            return;
        }

        if (targetFile is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Warning;
            PairingCodeText.Message = "Firmware: download cancelado.";
            AddLocalLog("Salvamento de firmware cancelado pelo usuario.");
            return;
        }

        try
        {
            await service.CopyToAsync(option.Id, targetFile.Path).ConfigureAwait(true);
            PairingCodeText.Severity = InfoBarSeverity.Success;
            PairingCodeText.Message = $"Firmware: salvo em {targetFile.Name}.";
            AddLocalLog($"Firmware salvo em: {targetFile.Path}");
        }
        catch (Exception ex)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: erro ao salvar arquivo.";
            AddLocalLog($"Falha ao salvar firmware: {ex.Message}");
        }
    }

    private static async Task<StorageFile?> PickFirmwareDestinationFileAsync(PrecompiledFirmwareOption option)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = option.FileName,
            SuggestedStartLocation = PickerLocationId.Downloads,
        };

        picker.FileTypeChoices.Add("Firmware BIN", new List<string> { ".bin" });

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSaveFileAsync();
    }

    private async Task RunSelectedCommandAsync(DeviceCommandType commandType)
    {
        var selected = GetSelectedDeviceItem();
        if (selected is null || DeviceOps is null)
        {
            return;
        }

        await DeviceOps.RunCommandAsync(selected.DeviceId, commandType).ConfigureAwait(false);
    }

    private async Task CommitBrightnessIfPendingAsync()
    {
        if (!brightnessCommitPending || suppressBrightnessSliderEvents)
        {
            return;
        }

        var selected = GetSelectedDeviceItem();
        var ops = DeviceOps;
        if (selected is null || ops is null || selected.Status != DeviceStatus.Online)
        {
            brightnessCommitPending = false;
            return;
        }

        brightnessCommitPending = false;
        var brightness = Math.Clamp((int)Math.Round(DashboardBrightnessSlider.Value), SafeBrightnessMin, SafeBrightnessMax);
        var result = await ops.SetBrightnessAsync(selected.DeviceId, brightness).ConfigureAwait(false);
        if (result.Accepted && result.Completed && result.Success)
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(result.Message) ? result.ErrorCode : result.Message;
        AddLocalLog($"Falha ao ajustar brilho em {selected.DeviceId}: {reason ?? "erro desconhecido"}");
    }

    // DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
    private void ApplyState(DeviceOperationsState state)
    {
        currentState = state;

        var refreshText = state.LastRefreshUtc == default
            ? "sem atualizacao"
            : state.LastRefreshUtc.ToLocalTime().ToString("HH:mm:ss");
        ServerInfoText.Text = $"Servidor: {state.ServerBaseAddress} | mDNS: _micaaudio._tcp | Atualizado: {refreshText}";

        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        if (isApplyingDeviceList)
        {
            pendingDeviceListSnapshot = devices.ToArray();
            return;
        }

        isApplyingDeviceList = true;
        try
        {
            var selectedId = GetSelectedDeviceId();

            allItems.Clear();
            foreach (var device in devices)
            {
                var presence = DeviceLifecyclePolicy.Build(device, lifecycleThresholds, DateTimeOffset.UtcNow);
                var profile = string.IsNullOrWhiteSpace(device.Profile) ? "-" : device.Profile;

                allItems.Add(new DeviceListItem
                {
                    DeviceId = device.DeviceId,
                    Name = device.Name,
                    Status = device.Status,
                    StatusLine = $"{presence.PrimaryStateLabel} | {presence.SecondaryStateLabel} | {presence.LastSeenLabel} | Perfil {profile}",
                    AppId = string.IsNullOrWhiteSpace(device.ActiveAppId) ? string.Empty : device.ActiveAppId!,
                    AppName = string.IsNullOrWhiteSpace(device.ActiveAppName) ? string.Empty : device.ActiveAppName!,
                    Presence = presence,
                });
            }

            ApplyFilter(selectedId);
            ApplySelectionDetails();
            ApplyButtonState();
        }
        finally
        {
            isApplyingDeviceList = false;
        }

        if (pendingDeviceListSnapshot is { } pending)
        {
            pendingDeviceListSnapshot = null;
            ApplyDevices(pending);
        }
    }

    private void ApplyFilter(string? selectedDeviceId)
    {
        visibleItems.Clear();
        visibleItems.AddRange(allItems);
        ApplyRenderedItemsDiff(selectedDeviceId);
    }

    private void ApplyRenderedItemsDiff(string? selectedDeviceId)
    {
        var nextIds = visibleItems.Select(static item => item.DeviceId).ToList();
        var nextSignature = DeviceListRenderDiff.BuildSignature(visibleItems.Select(BuildRenderToken));
        if (string.Equals(lastAppliedDeviceListSignature, nextSignature, StringComparison.Ordinal))
        {
            RestoreSelection(selectedDeviceId);
            UpdateDeviceRowSelection();
            return;
        }

        var nextIdSet = new HashSet<string>(nextIds, StringComparer.OrdinalIgnoreCase);
        foreach (var existingId in renderedOrder.ToArray())
        {
            if (nextIdSet.Contains(existingId))
            {
                continue;
            }

            if (!renderedItemsByDeviceId.TryGetValue(existingId, out var removed))
            {
                continue;
            }

            var currentIndex = DevicesList.Items.IndexOf(removed.Container);
            if (currentIndex >= 0)
            {
                DevicesList.Items.RemoveAt(currentIndex);
            }

            removed.StopPreview();
            renderedItemsByDeviceId.Remove(existingId);
        }

        var orderChanged = !DeviceListRenderDiff.HasSameOrder(renderedOrder, nextIds);
        for (var index = 0; index < visibleItems.Count; index++)
        {
            var item = visibleItems[index];
            var previewModel = ResolvePreviewApp(item);
            var inlinePreviewItem = DevicePreviewVisibilityPolicy.ShouldShowInlinePreview(item.Status, previewModel) ? previewModel : null;
            var previewPlaceholderText = DevicePreviewVisibilityPolicy.BuildInlinePreviewPlaceholder(item.Status, previewModel);

            if (!renderedItemsByDeviceId.TryGetValue(item.DeviceId, out var visualItem))
            {
                visualItem = new DeviceListVisualItem(item, inlinePreviewItem, previewPlaceholderText);
                renderedItemsByDeviceId[item.DeviceId] = visualItem;
            }
            else
            {
                visualItem.Update(item, inlinePreviewItem, previewPlaceholderText);
            }

            var currentIndex = DevicesList.Items.IndexOf(visualItem.Container);
            if (currentIndex < 0)
            {
                DevicesList.Items.Insert(Math.Min(index, DevicesList.Items.Count), visualItem.Container);
                continue;
            }

            if (orderChanged && currentIndex != index)
            {
                DevicesList.Items.RemoveAt(currentIndex);
                DevicesList.Items.Insert(Math.Min(index, DevicesList.Items.Count), visualItem.Container);
            }
        }

        renderedOrder.Clear();
        renderedOrder.AddRange(nextIds);
        lastAppliedDeviceListSignature = nextSignature;

        RestoreSelection(selectedDeviceId);
        UpdateDeviceRowSelection();
    }

    private void RestoreSelection(string? selectedDeviceId)
    {
        if (!string.IsNullOrWhiteSpace(selectedDeviceId)
            && renderedItemsByDeviceId.TryGetValue(selectedDeviceId, out var selectedVisual))
        {
            if (!ReferenceEquals(DevicesList.SelectedItem, selectedVisual.Container))
            {
                DevicesList.SelectedItem = selectedVisual.Container;
            }

            return;
        }

        if (DevicesList.SelectedItem is ListViewItem selectedContainer
            && selectedContainer.Tag is DeviceListVisualItem selectedVisualItem
            && renderedItemsByDeviceId.ContainsKey(selectedVisualItem.DeviceId))
        {
            return;
        }

        if (DevicesList.SelectedItem is not null)
        {
            DevicesList.SelectedItem = null;
        }
    }

    private static string BuildRenderToken(DeviceListItem item)
    {
        return string.Concat(
            item.DeviceId,
            "|",
            item.Status,
            "|",
            item.StatusLine,
            "|",
            item.AppId,
            "|",
            item.AppName);
    }

    private void ClearRenderedItems()
    {
        foreach (var item in renderedItemsByDeviceId.Values)
        {
            item.StopPreview();
        }

        renderedItemsByDeviceId.Clear();
        renderedOrder.Clear();
        lastAppliedDeviceListSignature = null;
        DevicesList.Items.Clear();
    }

    private void StartPreviewPump()
    {
        if (previewPumpTimer is not null)
        {
            return;
        }

        previewPumpTimer = DispatcherQueue.CreateTimer();
        previewPumpTimer.Interval = TimeSpan.FromMilliseconds(125);
        previewPumpTimer.Tick += OnPreviewPumpTick;
        previewPumpTimer.Start();
    }

    private void StopPreviewPump()
    {
        if (previewPumpTimer is null)
        {
            return;
        }

        previewPumpTimer.Stop();
        previewPumpTimer.Tick -= OnPreviewPumpTick;
        previewPumpTimer = null;
    }

    private void OnPreviewPumpTick(DispatcherQueueTimer sender, object args)
    {
        if (isApplyingDeviceList)
        {
            return;
        }

        var snapshot = renderedItemsByDeviceId.Values.ToArray();
        MicaAudio.Core.Presets.RgbaColor[]? frameCache = null;

        foreach (var visualItem in snapshot)
        {
            if (!string.Equals(visualItem.Source.AppId, Hub75VisualizerSessionService.VisualizerAppId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            frameCache ??= simulatorLedOutput.GetFrameSnapshot();
            visualItem.SetRuntimeFrame(frameCache);
        }
    }

    private AppCatalogItem? ResolvePreviewApp(DeviceListItem item)
    {
        return DevicePreviewResolver.Resolve(item.AppId, item.AppName, appCatalogById);
    }

    private static void AddLocalLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[DevicesPage] {message}");
    }

    private void UpdateDeviceLogs(string? deviceId, IReadOnlyList<string> entries, string placeholder)
    {
        var header = string.IsNullOrWhiteSpace(deviceId)
            ? "Logs do dispositivo"
            : $"Logs do dispositivo · {deviceId}";
        var count = entries.Count;
        var normalizedPlaceholder = count == 0 ? placeholder : string.Empty;
        var tail = count > 0 ? entries[^1] : string.Empty;
        var deviceUnchanged = string.Equals(lastRenderedDeviceLogsDeviceId, deviceId, StringComparison.OrdinalIgnoreCase);

        if (deviceUnchanged
            && lastRenderedDeviceLogCount == count
            && string.Equals(lastRenderedDeviceLogTail, tail, StringComparison.Ordinal)
            && string.Equals(lastRenderedDeviceLogsHeader, header, StringComparison.Ordinal)
            && string.Equals(lastRenderedDeviceLogsPlaceholder, normalizedPlaceholder, StringComparison.Ordinal))
        {
            return;
        }

        DeviceLogsTextBox.Header = header;
        DeviceLogsTextBox.Text = count == 0
            ? normalizedPlaceholder
            : string.Join("\r\n", entries) + "\r\n";

        lastRenderedDeviceLogsDeviceId = deviceId;
        lastRenderedDeviceLogCount = count;
        lastRenderedDeviceLogTail = tail;
        lastRenderedDeviceLogsHeader = header;
        lastRenderedDeviceLogsPlaceholder = normalizedPlaceholder;
    }

    // DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#consumo-na-devicespage-entrega-3
    private void ApplyDashboard(string? selectionDeviceId, bool hasSelection, DeviceSnapshot? snapshot, DeviceMetricsPresentation metrics)
    {
        var placeholder = ResolveDashboardPlaceholder(hasSelection, metrics);
        var loopTrendSamples = CaptureLoopTrendSamples(selectionDeviceId, snapshot, metrics);
        var loopTrendSignature = BuildLoopTrendSignature(loopTrendSamples);
        var brightnessValueLabel = BuildBrightnessValueLabel(snapshot);
        var brightnessStatusLabel = BuildBrightnessStatusLabel(snapshot);
        var heartbeatLabel = BuildHeartbeatLabel(snapshot);

        var signature = BuildDashboardSignature(
            selectionDeviceId,
            hasSelection,
            metrics,
            placeholder,
            brightnessValueLabel,
            brightnessStatusLabel,
            heartbeatLabel,
            loopTrendSignature);

        if (string.Equals(lastRenderedDashboardSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        DashboardBrightnessValueText.Text = brightnessValueLabel;
        DashboardBrightnessStatusText.Text = brightnessStatusLabel;
        DashboardTelemetryHeartbeatText.Text = heartbeatLabel;

        var sliderValue = snapshot?.BrightnessCap is int brightnessCap
            ? Math.Clamp(brightnessCap, SafeBrightnessMin, SafeBrightnessMax)
            : SafeBrightnessMax;
        suppressBrightnessSliderEvents = true;
        DashboardBrightnessSlider.Value = sliderValue;
        suppressBrightnessSliderEvents = false;
        brightnessCommitPending = false;

        DashboardLoopLoadText.Text = metrics.LoopLoadPercent.HasValue
            ? $"{Math.Clamp(metrics.LoopLoadPercent.Value, 0, 100)}%"
            : "-";
        DashboardLoopLoadBar.Value = Math.Clamp(metrics.LoopLoadProgress, 0d, 1d);

        DashboardUptimeText.Text = metrics.UptimeLabel;
        DashboardHeapText.Text = metrics.HeapLabel;
        DashboardHeapFragmentationText.Text = BuildFragmentationLabel("Heap", metrics.HeapFragmentationProgress, available: true);
        DashboardHeapFragmentationBar.Value = Math.Clamp(metrics.HeapFragmentationProgress ?? 0d, 0d, 1d);
        DashboardHeapFragmentationBar.Visibility = metrics.HeapFragmentationProgress.HasValue ? Visibility.Visible : Visibility.Collapsed;

        DashboardPsramText.Text = metrics.PsramLabel;
        DashboardPsramFragmentationText.Text = BuildFragmentationLabel("PSRAM", metrics.PsramFragmentationProgress, metrics.IsPsramAvailable);
        DashboardPsramFragmentationBar.Value = Math.Clamp(metrics.PsramFragmentationProgress ?? 0d, 0d, 1d);
        DashboardPsramFragmentationBar.Visibility = metrics.PsramFragmentationProgress.HasValue ? Visibility.Visible : Visibility.Collapsed;

        DashboardNetworkText.Text = metrics.NetworkLabel;

        DashboardMetricsGrid.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(placeholder))
        {
            DashboardPlaceholderText.Visibility = Visibility.Collapsed;
        }
        else
        {
            DashboardPlaceholderText.Text = placeholder;
            DashboardPlaceholderText.Visibility = Visibility.Visible;
        }

        ApplyTileTone(DashboardLoopTile);
        ApplyTileTone(DashboardHeapTile);
        ApplyTileTone(DashboardPsramTile);
        ApplyTileTone(DashboardNetworkTile);

        ApplyLoopTrendBars(loopTrendSamples, hasSelection);
        lastRenderedDashboardSignature = signature;
    }

    private static string BuildDashboardSignature(
        string? selectionDeviceId,
        bool hasSelection,
        DeviceMetricsPresentation metrics,
        string placeholder,
        string brightnessValueLabel,
        string brightnessStatusLabel,
        string heartbeatLabel,
        string loopTrendSignature)
    {
        var loopProgressScaled = (int)Math.Round(Math.Clamp(metrics.LoopLoadProgress, 0d, 1d) * 1000d);
        var heapFragmentationScaled = metrics.HeapFragmentationProgress.HasValue
            ? (int)Math.Round(Math.Clamp(metrics.HeapFragmentationProgress.Value, 0d, 1d) * 1000d)
            : -1;
        var psramFragmentationScaled = metrics.PsramFragmentationProgress.HasValue
            ? (int)Math.Round(Math.Clamp(metrics.PsramFragmentationProgress.Value, 0d, 1d) * 1000d)
            : -1;

        return string.Concat(
            hasSelection ? "1" : "0", "|",
            selectionDeviceId ?? "-", "|",
            metrics.StatusLabel, "|",
            metrics.UptimeLabel, "|",
            metrics.HeapLabel, "|",
            metrics.PsramLabel, "|",
            metrics.NetworkLabel, "|",
            metrics.LoopLoadPercent?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            loopProgressScaled.ToString(CultureInfo.InvariantCulture), "|",
            heapFragmentationScaled.ToString(CultureInfo.InvariantCulture), "|",
            psramFragmentationScaled.ToString(CultureInfo.InvariantCulture), "|",
            metrics.HasMetrics ? "1" : "0", "|",
            metrics.IsOfflineSnapshot ? "1" : "0", "|",
            metrics.IsPsramAvailable ? "1" : "0", "|",
            brightnessValueLabel, "|",
            brightnessStatusLabel, "|",
            heartbeatLabel, "|",
            loopTrendSignature, "|",
            placeholder);
    }

    private IReadOnlyList<int> CaptureLoopTrendSamples(string? deviceId, DeviceSnapshot? snapshot, DeviceMetricsPresentation metrics)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Array.Empty<int>();
        }

        if (!loopTrendByDeviceId.TryGetValue(deviceId, out var samples))
        {
            samples = new Queue<int>(DashboardTrendSampleCapacity);
            loopTrendByDeviceId[deviceId] = samples;
        }

        if (metrics.LoopLoadPercent.HasValue)
        {
            var normalized = Math.Clamp(metrics.LoopLoadPercent.Value, 0, 100);
            var stamp = snapshot?.LastTelemetryUtc;

            var hasLastValue = lastLoopTrendValueByDeviceId.TryGetValue(deviceId, out var lastValue);
            var hasLastStamp = lastLoopTrendStampByDeviceId.TryGetValue(deviceId, out var lastStamp);

            var duplicate = stamp.HasValue
                ? hasLastStamp && lastStamp.HasValue && lastStamp.Value == stamp.Value && hasLastValue && lastValue == normalized
                : hasLastValue && lastValue == normalized;

            if (!duplicate)
            {
                if (samples.Count >= DashboardTrendSampleCapacity)
                {
                    _ = samples.Dequeue();
                }

                samples.Enqueue(normalized);
                lastLoopTrendValueByDeviceId[deviceId] = normalized;
                lastLoopTrendStampByDeviceId[deviceId] = stamp;
            }
        }

        return samples.ToArray();
    }

    private void ApplyLoopTrendBars(IReadOnlyList<int> samples, bool hasSelection)
    {
        var hasSamples = hasSelection && samples.Count > 0;
        DashboardLoopTrendGrid.Opacity = hasSelection ? 1d : 0.45d;
        DashboardLoopTrendCaptionText.Text = !hasSelection
            ? "Tendencia carga do loop: selecione um dispositivo"
            : hasSamples
                ? $"Tendencia carga do loop: ultimas {samples.Count} amostras"
                : "Tendencia carga do loop: aguardando amostras";

        var visibleCount = Math.Min(samples.Count, DashboardLoopTrendBars.Count);
        var sourceStart = samples.Count - visibleCount;
        var targetStart = DashboardLoopTrendBars.Count - visibleCount;

        const double minHeight = 4d;
        const double maxHeight = 58d;

        for (var index = 0; index < DashboardLoopTrendBars.Count; index++)
        {
            var bar = DashboardLoopTrendBars[index];
            if (!hasSamples || index < targetStart)
            {
                bar.Height = minHeight;
                bar.Opacity = 0.22;
                bar.Background = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 65, 82, 104));
                continue;
            }

            var sample = Math.Clamp(samples[sourceStart + (index - targetStart)], 0, 100);
            var ratio = sample / 100d;
            bar.Height = minHeight + ((maxHeight - minHeight) * ratio);
            bar.Opacity = 0.35 + (ratio * 0.45);
            bar.Background = BuildTrendBarBrush();
        }
    }

    private static Brush BuildTrendBarBrush()
    {
        return ResolveBrush("AppAccentBrush", Color.FromArgb(255, 15, 108, 189));
    }

    private static string BuildLoopTrendSignature(IReadOnlyList<int> samples)
    {
        if (samples.Count == 0)
        {
            return "-";
        }

        var builder = new System.Text.StringBuilder(samples.Count * 4);
        for (var index = 0; index < samples.Count; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(',');
            }

            _ = builder.Append(samples[index].ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string ResolveDashboardPlaceholder(bool hasSelection, DeviceMetricsPresentation metrics)
    {
        if (!hasSelection)
        {
            return "Selecione um dispositivo para ver metricas";
        }

        if (metrics.IsOfflineSnapshot)
        {
            return "Offline: exibindo ultimo snapshot conhecido";
        }

        if (!metrics.HasMetrics)
        {
            return metrics.PlaceholderMessage;
        }

        return string.Empty;
    }

    private static string BuildFragmentationLabel(string memoryKind, double? progress, bool available)
    {
        if (!available)
        {
            return $"{memoryKind} integridade bloco: n/a";
        }

        if (!progress.HasValue)
        {
            return $"{memoryKind} integridade bloco: -";
        }

        var percent = Math.Clamp((int)Math.Round(progress.Value * 100d), 0, 100);
        return $"{memoryKind} integridade bloco: {percent}%";
    }

    private static string BuildBrightnessValueLabel(DeviceSnapshot? snapshot)
    {
        var cap = snapshot?.BrightnessCap is int capValue
            ? Math.Clamp(capValue, SafeBrightnessMin, SafeBrightnessMax)
            : SafeBrightnessMax;
        return $"{cap}/255";
    }

    private static string BuildBrightnessStatusLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Brilho aplicado: - | Limite: -";
        }

        var applied = snapshot.BrightnessApplied is int appliedValue
            ? Math.Clamp(appliedValue, 0, 255).ToString(CultureInfo.InvariantCulture)
            : "-";
        var cap = snapshot.BrightnessCap is int capValue
            ? Math.Clamp(capValue, SafeBrightnessMin, SafeBrightnessMax).ToString(CultureInfo.InvariantCulture)
            : "-";
        return $"Brilho aplicado: {applied} | Limite: {cap}";
    }

    private static string BuildHeartbeatLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Heartbeat: - | LED teste: - | Duty: -";
        }

        var sequence = snapshot.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var ledState = snapshot.TestLedEnabled switch
        {
            true => "habilitado",
            false => "desabilitado",
            _ => snapshot.TestLedAvailable == false ? "indisponivel" : "-",
        };
        var duty = snapshot.TestLedDuty is int dutyValue
            ? Math.Clamp(dutyValue, 0, 255).ToString(CultureInfo.InvariantCulture)
            : "-";
        return $"Heartbeat: #{sequence} | LED teste: {ledState} | Duty: {duty}";
    }

    private static string BuildSelectedSignalLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Sinal -";
        }

        return snapshot.LastKnownRssi is int rssi
            ? $"Sinal {rssi} dBm"
            : "Sinal indisponivel";
    }

    private void ApplyTileTone(Border tileBorder)
    {
        tileBorder.BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81));
        tileBorder.Background = ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42));
    }

    private DeviceListVisualItem? GetSelectedVisualItem()
    {
        return DevicesList.SelectedItem switch
        {
            ListViewItem container when container.Tag is DeviceListVisualItem visual => visual,
            DeviceListVisualItem visual => visual,
            _ => null,
        };
    }

    private DeviceListItem? GetSelectedDeviceItem()
    {
        return GetSelectedVisualItem()?.Source;
    }

    private string? GetSelectedDeviceId()
    {
        return GetSelectedDeviceItem()?.DeviceId;
    }

    private DeviceSnapshot? FindDeviceSnapshot(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        foreach (var snapshot in currentState.DeviceListSnapshot)
        {
            if (string.Equals(snapshot.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return snapshot;
            }
        }

        return null;
    }

    private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDeviceRowSelection();
        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void UpdateDeviceRowSelection()
    {
        var selectedId = GetSelectedDeviceId();
        foreach (var item in renderedItemsByDeviceId.Values)
        {
            item.SetSelectedVisual(string.Equals(item.DeviceId, selectedId, StringComparison.OrdinalIgnoreCase));
        }
    }

    // DOCS: docs/wiki/guides/setup-new-device.md#tela-dispositivos
    private void ApplySelectionDetails()
    {
        var selectedVisual = GetSelectedVisualItem();
        if (selectedVisual is null)
        {
            SetDetailsPaneVisible(false);
            SelectedDeviceTitleText.Text = "Nenhum dispositivo selecionado";
            SelectedDeviceSubtitleText.Text = "-";
            SelectedDeviceRegistrationText.Text = "-";
            SelectedDeviceAppText.Text = "App ativo: -";
            SelectedDeviceSignalText.Text = "Sinal -";
            TestLedButton.Label = "Testar LED";
            ApplyDashboard(selectionDeviceId: null, hasSelection: false, snapshot: null, DeviceMetricsFormatter.Build(null));
            UpdateDeviceLogs(deviceId: null, entries: Array.Empty<string>(), placeholder: "Selecione um dispositivo para ver logs do dispositivo.");
            return;
        }

        SetDetailsPaneVisible(true);

        var selected = selectedVisual.Source;
        SelectedDeviceTitleText.Text = selected.Name;
        SelectedDeviceSubtitleText.Text = selected.StatusLine;
        SelectedDeviceRegistrationText.Text = BuildRegistrationLine(selected);
        SelectedDeviceAppText.Text = DevicePreviewVisibilityPolicy.BuildSelectedAppLabel(selected.Status, selected.AppName);

        var selectedSnapshot = FindDeviceSnapshot(selected.DeviceId);
        SelectedDeviceSignalText.Text = BuildSelectedSignalLabel(selectedSnapshot);
        var testLedAvailable = selectedSnapshot?.TestLedAvailable != false;
        TestLedButton.Label = testLedAvailable ? "Testar LED" : "LED indisponivel";
        var metrics = DeviceMetricsFormatter.Build(selectedSnapshot);
        ApplyDashboard(selectionDeviceId: selected.DeviceId, hasSelection: true, snapshot: selectedSnapshot, metrics);

        var deviceLogs = DeviceOps?.GetDeviceLogs(selected.DeviceId) ?? Array.Empty<string>();
        var logsPlaceholder = deviceLogs.Count == 0
            ? "Sem eventos para este dispositivo ainda."
            : string.Empty;
        UpdateDeviceLogs(selected.DeviceId, deviceLogs, logsPlaceholder);
    }

    private void SetDetailsPaneVisible(bool visible)
    {
        DeviceDetailsGrid.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        DevicesDetailsColumn.Width = visible
            ? new GridLength(3, GridUnitType.Star)
            : new GridLength(0);
    }

    private void ApplyButtonState()
    {
        var selected = GetSelectedDeviceItem();
        var selectedSnapshot = selected is null ? null : FindDeviceSnapshot(selected.DeviceId);
        var canRunCommand = selected is not null
            && selected.Presence.CanRunCommands
            && !currentState.CommandInProgress;
        var canRemove = selected is not null && !currentState.CommandInProgress;
        var testLedAvailable = selectedSnapshot?.TestLedAvailable != false;

        TestLedButton.IsEnabled = canRunCommand && testLedAvailable;
        if (selected is null)
        {
            TestLedButton.Label = "Testar LED";
        }
        else
        {
            TestLedButton.Label = testLedAvailable ? "Testar LED" : "LED indisponivel";
        }

        DashboardBrightnessSlider.IsEnabled = canRunCommand;
        RemoveDeviceButton.IsEnabled = canRemove;
    }

    private sealed class DeviceListItem
    {
        public string DeviceId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public DeviceStatus Status { get; init; }

        public string StatusLine { get; init; } = string.Empty;

        public string AppId { get; init; } = string.Empty;

        public string AppName { get; init; } = string.Empty;

        public DeviceLifecyclePresentation Presence { get; init; }
    }

    private sealed class DeviceListVisualItem
    {
        public DeviceListVisualItem(DeviceListItem source, AppCatalogItem? previewItem, string previewPlaceholderText)
        {
            Source = source;
            PreviewItem = previewItem;
            RowControl = new DeviceListRowControl();
            Container = new ListViewItem
            {
                Content = RowControl,
                Tag = this,
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            RowControl.Bind(source.Name, source.DeviceId, source.StatusLine, source.Presence, previewItem, previewPlaceholderText);
        }

        public DeviceListItem Source { get; private set; }

        public string DeviceId => Source.DeviceId;

        public AppCatalogItem? PreviewItem { get; private set; }

        public DeviceListRowControl RowControl { get; }

        public ListViewItem Container { get; }

        public void Update(DeviceListItem source, AppCatalogItem? previewItem, string previewPlaceholderText)
        {
            Source = source;
            PreviewItem = previewItem;
            RowControl.Bind(source.Name, source.DeviceId, source.StatusLine, source.Presence, previewItem, previewPlaceholderText);
        }

        public void SetSelectedVisual(bool selected)
        {
            RowControl.SetSelected(selected);
        }

        public void StopPreview()
        {
            RowControl.StopPreview();
        }

        public void SetRuntimeFrame(MicaAudio.Core.Presets.RgbaColor[]? frame)
        {
            RowControl.SetRuntimeFrame(frame);
        }
    }

    private static string BuildRegistrationLine(DeviceListItem selected)
    {
        if (selected.Status == DeviceStatus.Pairing)
        {
            return "Vinculo: pareamento iniciado; aguardando a primeira sessao do dispositivo";
        }

        if (selected.Presence.IsNeverSeen)
        {
            return "Vinculo: registrado localmente; aguardando primeiro provisionamento";
        }

        if (selected.Status == DeviceStatus.Online)
        {
            return "Vinculo: configurado e em comunicacao";
        }

        if (selected.Presence.IsConfigUncertain)
        {
            return "Vinculo: registro salvo localmente; a configuracao no ESP pode ter mudado";
        }

        return "Vinculo: registro salvo localmente; sem telemetria no momento";
    }
}

