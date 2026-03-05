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
using Microsoft.UI.Xaml.Shapes;
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
    private readonly Dictionary<string, Queue<int>> espDashLoopHistoryByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<int>> espDashHeapHistoryByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, uint?> espDashLastSequenceByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private const int SafeBrightnessMin = 30;
    private const int SafeBrightnessMax = 160;
    private const int DashboardTrendSampleCapacity = 20;
    private const int EspDashHistorySampleCapacity = 30;
    private const int HeapTotalBytesBaseline = 320000;
    private const int PsramTotalBytesBaseline = 8000000;
    private const string OfflineDashboardFallbackText = "Offline: exibindo snapshot seguro";

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
    private bool wizardBindingsInitialized;
    private bool wizardOperationInFlight;

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
        espDashLoopHistoryByDeviceId.Clear();
        espDashHeapHistoryByDeviceId.Clear();
        espDashLastSequenceByDeviceId.Clear();
        suppressBrightnessSliderEvents = false;
        brightnessCommitPending = false;
        HideNewDeviceWizard();
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
        DashboardBrightnessValueText.Text = $"{normalized}/160";
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

        EnsureWizardBindings();

        wizardOperationInFlight = false;
        WizardStatusText.Text = string.Empty;
        WizardSummaryNoteText.Text = "Selecione a porta COM para gravar o firmware. O Wi-Fi sera configurado no AP do ESP32 apos o flash.";
        WizardPortComboBox.ItemsSource = Array.Empty<SerialPortDescriptor>();
        WizardPortComboBox.SelectedIndex = -1;
        ResetWizardFlashProgressUi();
        ApplyWizardBusyState(false);
        ShowWizardOverlay();
        await RefreshWizardPortsAsync().ConfigureAwait(true);
    }

    private void EnsureWizardBindings()
    {
        if (wizardBindingsInitialized)
        {
            return;
        }

        wizardBindingsInitialized = true;
        WizardCloseButton.Click += (_, _) =>
        {
            if (!wizardOperationInFlight)
            {
                HideNewDeviceWizard();
            }
        };

        WizardOverlay.PointerPressed += (_, e) =>
        {
            if (!wizardOperationInFlight && ReferenceEquals(e.OriginalSource, WizardOverlay))
            {
                HideNewDeviceWizard();
            }
        };

        WizardRefreshPortsButton.Click += async (_, _) => await RefreshWizardPortsAsync().ConfigureAwait(true);
        WizardFinishButton.Click += async (_, _) => await RunWizardOnboardingAsync().ConfigureAwait(true);
    }

    private void ShowWizardOverlay()
    {
        WizardOverlay.Visibility = Visibility.Visible;
        WizardOverlay.IsHitTestVisible = true;
    }

    private void HideNewDeviceWizard()
    {
        WizardOverlay.Visibility = Visibility.Collapsed;
        WizardOverlay.IsHitTestVisible = false;
        wizardOperationInFlight = false;
        ResetWizardFlashProgressUi();
    }

    private void ApplyWizardBusyState(bool busy)
    {
        wizardOperationInFlight = busy;
        WizardCloseButton.IsEnabled = !busy;
        WizardFinishButton.IsEnabled = !busy;
        WizardPortComboBox.IsEnabled = !busy;
        WizardRefreshPortsButton.IsEnabled = !busy;
        WizardFinishButton.Content = busy
            ? BuildButtonWithGlyph("\uE895", "Processando...")
            : BuildButtonWithGlyph("\uE73E", "Concluir");
    }

    private void ResetWizardFlashProgressUi()
    {
        WizardFlashProgressBar.Value = 0;
        WizardFlashPercentText.Text = "0%";
        WizardFlashProgressHost.Visibility = Visibility.Collapsed;
        WizardFlashProgressBar.IsIndeterminate = false;
    }

    private void SetWizardFlashProgressUi(int percent, bool visible)
    {
        var normalized = Math.Clamp(percent, 0, 100);
        WizardFlashProgressBar.IsIndeterminate = false;
        WizardFlashProgressBar.Value = normalized;
        WizardFlashPercentText.Text = $"{normalized}%";
        WizardFlashProgressHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshWizardPortsAsync()
    {
        var catalog = SerialPortCatalogService;
        if (catalog is null)
        {
            WizardStatusText.Text = "Catalogo serial indisponivel.";
            return;
        }

        var selectedPort = (WizardPortComboBox.SelectedItem as SerialPortDescriptor)?.PortName;
        var ports = await catalog.ListAsync(includeAllPorts: false).ConfigureAwait(true);
        WizardPortComboBox.ItemsSource = ports;
        if (!string.IsNullOrWhiteSpace(selectedPort))
        {
            WizardPortComboBox.SelectedItem = ports.FirstOrDefault(item =>
                string.Equals(item.PortName, selectedPort, StringComparison.OrdinalIgnoreCase));
        }

        if (WizardPortComboBox.SelectedIndex < 0 && ports.Count > 0)
        {
            WizardPortComboBox.SelectedIndex = 0;
        }

        WizardStatusText.Text = ports.Count == 0
            ? "Nenhuma porta COM detectada. Conecte o ESP32 e atualize a lista."
            : $"Portas detectadas: {ports.Count}.";
    }

    private async Task RunWizardOnboardingAsync()
    {
        if (wizardOperationInFlight)
        {
            return;
        }

        var selectedPort = WizardPortComboBox.SelectedItem as SerialPortDescriptor;
        if (selectedPort is null)
        {
            WizardStatusText.Text = "Selecione uma porta COM valida para continuar.";
            return;
        }

        var onboarding = OnboardingService;
        if (onboarding is null)
        {
            WizardStatusText.Text = "Servico de onboarding indisponivel.";
            return;
        }

        ApplyWizardBusyState(true);

        var progress = new Progress<DeviceOnboardingProgress>(update =>
        {
            WizardStatusText.Text = $"[{DescribeOnboardingStage(update.Stage)}] {update.Message}";
            if (update.Stage == DeviceOnboardingStage.Flashing)
            {
                SetWizardFlashProgressUi(update.Percent, visible: true);
            }
            else if (WizardFlashProgressHost.Visibility == Visibility.Visible && update.Percent >= 100)
            {
                SetWizardFlashProgressUi(100, visible: true);
            }
        });

        var result = await onboarding.RunAsync(
            new DeviceOnboardingRequest
            {
                PortName = selectedPort.PortName,
            },
            progress).ConfigureAwait(true);

        if (result.Success)
        {
            var pairCode = string.IsNullOrWhiteSpace(result.PairCode) ? "-" : result.PairCode;
            PairingCodeText.Severity = InfoBarSeverity.Informational;
            PairingCodeText.Message = $"Pareamento pendente: use o codigo {pairCode} no AP do dispositivo.";
            WizardStatusText.Text = result.Message;
            AddLocalLog($"Flash concluido na porta {selectedPort.PortName}; aguardando provisionamento via AP.");
            ApplyWizardBusyState(false);
            await ShowPairCodeDialogAsync(pairCode).ConfigureAwait(true);
            HideNewDeviceWizard();
            return;
        }

        PairingCodeText.Severity = InfoBarSeverity.Error;
        PairingCodeText.Message = $"Onboarding falhou: {result.Message}";
        WizardStatusText.Text = $"Falha ({result.ErrorCode ?? "erro"}): {result.Message}";
        ApplyWizardBusyState(false);
    }

    private async Task ShowPairCodeDialogAsync(string pairCode)
    {
        var dialog = new ContentDialog
        {
            Title = "Flash concluido",
            PrimaryButtonText = "Entendi",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = "Codigo de pareamento: " + pairCode + Environment.NewLine
                + "1) Conecte no AP MicaAudio-Setup-xxxx." + Environment.NewLine
                + "2) Configure Wi-Fi/servidor no portal." + Environment.NewLine
                + "3) Informe este codigo no campo de pareamento.",
        };

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
        try
        {
            if (ShouldUseOfflineDashboardFallback(hasSelection, snapshot))
            {
                var offlineSignature = BuildOfflineDashboardSignature(selectionDeviceId, snapshot);
                if (!string.Equals(lastRenderedDashboardSignature, offlineSignature, StringComparison.Ordinal))
                {
                    ApplyOfflineDashboardFallback(snapshot, OfflineDashboardFallbackText);
                    lastRenderedDashboardSignature = offlineSignature;
                }

                return;
            }

            var placeholder = ResolveDashboardPlaceholder(hasSelection, metrics);
            var loopTrendSamples = CaptureLoopTrendSamples(selectionDeviceId, snapshot, metrics);
            var loopTrendSignature = BuildLoopTrendSignature(loopTrendSamples);
            var brightnessValueLabel = BuildBrightnessValueLabel(snapshot);
            var brightnessStatusLabel = BuildBrightnessStatusLabel(snapshot);
            var heartbeatLabel = BuildHeartbeatLabel(snapshot);
            UpdateEspDashHistory(selectionDeviceId, snapshot);
            var loopChartSeries = GetEspDashLoopSeries(selectionDeviceId);
            var heapChartSeries = GetEspDashHeapSeries(selectionDeviceId);
            var loopChartSignature = BuildLoopTrendSignature(loopChartSeries);
            var heapChartSignature = BuildLoopTrendSignature(heapChartSeries);

            var signature = BuildDashboardSignature(
                selectionDeviceId,
                hasSelection,
                snapshot,
                metrics,
                placeholder,
                brightnessValueLabel,
                brightnessStatusLabel,
                heartbeatLabel,
                loopTrendSignature,
                loopChartSignature,
                heapChartSignature);

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

            var loopPercent = metrics.LoopLoadPercent is int rawLoop ? Math.Clamp(rawLoop, 0, 100) : (int?)null;
            DashboardLoopLoadText.Text = loopPercent.HasValue
                ? $"{loopPercent.Value} %"
                : "-";
            DashboardLoopLoadBar.Value = SafeProgress(metrics.LoopLoadProgress);

            var heapPercent = TryComputePercent(snapshot?.FreeHeapBytes, HeapTotalBytesBaseline, out var resolvedHeapPercent)
                ? resolvedHeapPercent
                : (int?)null;
            DashboardHeapText.Text = heapPercent.HasValue ? $"{heapPercent.Value} %" : "-";
            DashboardHeapFragmentationText.Text = BuildHeapSubLabel(snapshot);
            DashboardHeapFragmentationBar.Value = SafeProgress(metrics.HeapFragmentationProgress);
            DashboardHeapFragmentationBar.Visibility = metrics.HeapFragmentationProgress.HasValue ? Visibility.Visible : Visibility.Collapsed;

            var psramPercent = snapshot?.PsramAvailable == true && TryComputePercent(snapshot.FreePsramBytes, PsramTotalBytesBaseline, out var resolvedPsramPercent)
                ? resolvedPsramPercent
                : (int?)null;
            DashboardPsramText.Text = psramPercent.HasValue ? $"{psramPercent.Value} %" : "-";
            DashboardPsramFragmentationText.Text = BuildPsramSubLabel(snapshot);
            DashboardPsramFragmentationBar.Value = SafeProgress(metrics.PsramFragmentationProgress);
            DashboardPsramFragmentationBar.Visibility = metrics.PsramFragmentationProgress.HasValue ? Visibility.Visible : Visibility.Collapsed;

            DashboardNetworkText.Text = metrics.NetworkLabel;
            DashboardUptimeText.Text = metrics.UptimeLabel;

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
            ApplyEspDashSection(hasSelection, snapshot, loopChartSeries, heapChartSeries);
            ApplyConnectivitySection(hasSelection, snapshot);
            lastRenderedDashboardSignature = signature;
        }
        catch (Exception ex)
        {
            LogRenderException("dashboard", selectionDeviceId, snapshot, ex);
            ApplyOfflineDashboardFallback(snapshot, OfflineDashboardFallbackText);
            lastRenderedDashboardSignature = BuildOfflineDashboardSignature(selectionDeviceId, snapshot);
        }
    }

    private static string BuildDashboardSignature(
        string? selectionDeviceId,
        bool hasSelection,
        DeviceSnapshot? snapshot,
        DeviceMetricsPresentation metrics,
        string placeholder,
        string brightnessValueLabel,
        string brightnessStatusLabel,
        string heartbeatLabel,
        string loopTrendSignature,
        string loopChartSignature,
        string heapChartSignature)
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
            loopChartSignature, "|",
            heapChartSignature, "|",
            snapshot?.WifiState ?? "-", "|",
            snapshot?.LastWifiEvent ?? "-", "|",
            snapshot?.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamFramesReceived?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamFramesApplied?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamSequenceGapCount?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamInvalidFrameCount?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            placeholder);
    }

    private static bool ShouldUseOfflineDashboardFallback(bool hasSelection, DeviceSnapshot? snapshot)
    {
        return hasSelection && (snapshot is null || snapshot.Status != DeviceStatus.Online);
    }

    private static string BuildOfflineDashboardSignature(string? selectionDeviceId, DeviceSnapshot? snapshot)
    {
        return string.Concat(
            "offline|",
            selectionDeviceId ?? "-",
            "|",
            snapshot?.Status.ToString() ?? "-",
            "|",
            snapshot?.LastTelemetryUtc?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            snapshot?.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            snapshot?.LastWifiEvent ?? "-");
    }

    private void ApplyOfflineDashboardFallback(DeviceSnapshot? snapshot, string placeholder)
    {
        DashboardBrightnessValueText.Text = BuildBrightnessValueLabel(snapshot);
        DashboardBrightnessStatusText.Text = BuildBrightnessStatusLabel(snapshot);
        DashboardTelemetryHeartbeatText.Text = BuildHeartbeatLabel(snapshot);

        var sliderValue = snapshot?.BrightnessCap is int brightnessCap
            ? Math.Clamp(brightnessCap, SafeBrightnessMin, SafeBrightnessMax)
            : SafeBrightnessMax;
        suppressBrightnessSliderEvents = true;
        DashboardBrightnessSlider.Value = sliderValue;
        suppressBrightnessSliderEvents = false;
        brightnessCommitPending = false;

        DashboardLoopLoadText.Text = "-";
        DashboardHeapText.Text = "-";
        DashboardPsramText.Text = "-";
        DashboardHeapFragmentationText.Text = "Frag. de memoria -";
        DashboardPsramFragmentationText.Text = "PSRAM indisponivel";
        DashboardLoopLoadBar.Value = 0d;
        DashboardHeapFragmentationBar.Value = 0d;
        DashboardPsramFragmentationBar.Value = 0d;
        DashboardHeapFragmentationBar.Visibility = Visibility.Collapsed;
        DashboardPsramFragmentationBar.Visibility = Visibility.Collapsed;

        DashboardNetworkText.Text = snapshot is null ? "Wi-Fi: -" : "Wi-Fi: indisponivel (offline)";
        DashboardUptimeText.Text = snapshot?.UptimeSeconds is int uptime && uptime >= 0
            ? $"Uptime: {FormatUptimeForEspDash(uptime)}"
            : "Uptime: -";

        DashboardMetricsGrid.Visibility = Visibility.Collapsed;
        DashboardPlaceholderText.Text = placeholder;
        DashboardPlaceholderText.Visibility = Visibility.Visible;

        ApplyLoopTrendBars(Array.Empty<int>(), hasSelection: false);
        EspDashSectionBorder.Visibility = Visibility.Collapsed;
        ConnectivitySectionBorder.Visibility = Visibility.Collapsed;
    }

    private static bool TryComputePercent(long? value, int baseline, out int percent)
    {
        percent = 0;
        if (!value.HasValue || value.Value < 0 || baseline <= 0)
        {
            return false;
        }

        var ratio = value.Value / (double)baseline;
        if (!double.IsFinite(ratio))
        {
            return false;
        }

        var scaled = ratio * 100d;
        if (!double.IsFinite(scaled))
        {
            return false;
        }

        percent = Math.Clamp((int)Math.Round(scaled), 0, 100);
        return true;
    }

    private static double SafeProgress(double? value)
    {
        if (!value.HasValue || !double.IsFinite(value.Value))
        {
            return 0d;
        }

        return Math.Clamp(value.Value, 0d, 1d);
    }

    private static void LogRenderException(string stage, string? deviceId, DeviceSnapshot? snapshot, Exception ex)
    {
        AddLocalLog(
            $"Falha no render {stage} (device={deviceId ?? "-"}, status={snapshot?.Status.ToString() ?? "-"}, " +
            $"wifiState={snapshot?.WifiState ?? "-"}, telemetry={snapshot?.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-"}): {ex.Message}");
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
        DashboardLoopTrendCaptionText.Text = "Historico de uso do processador";
        DashboardLoopTrendGrid.Visibility = hasSamples ? Visibility.Visible : Visibility.Collapsed;
        DashboardLoopTrendPlaceholderText.Visibility = hasSamples ? Visibility.Collapsed : Visibility.Visible;
        DashboardLoopTrendPlaceholderText.Text = !hasSelection
            ? "Historico de uso do processador: selecione um dispositivo"
            : "Historico de uso do processador: aguardando amostras";

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
                bar.Background = new SolidColorBrush(Color.FromArgb(255, 59, 63, 70));
                continue;
            }

            var sample = Math.Clamp(samples[sourceStart + (index - targetStart)], 0, 100);
            var ratio = sample / 100d;
            bar.Height = minHeight + ((maxHeight - minHeight) * ratio);
            bar.Opacity = index == DashboardLoopTrendBars.Count - 1 ? 1d : 0.75;
            bar.Background = BuildTrendBarBrush(sample);
        }
    }

    private static Brush BuildTrendBarBrush(int sample)
    {
        return sample > 70
            ? new SolidColorBrush(Color.FromArgb(255, 232, 160, 0))
            : new SolidColorBrush(Color.FromArgb(255, 150, 117, 30));
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

    private static string BuildHeapSubLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot?.FreeHeapBytes is not long freeHeap || snapshot.LargestHeapBlockBytes is not long largest)
        {
            return "Frag. de memoria -";
        }

        if (freeHeap <= 0 || largest < 0)
        {
            return "Frag. de memoria -";
        }

        var fragmentation = Math.Clamp((int)Math.Round((1d - (largest / (double)freeHeap)) * 100d), 0, 100);
        return $"Frag. de memoria {fragmentation}%";
    }

    private static string BuildPsramSubLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot?.PsramAvailable != true)
        {
            return "PSRAM indisponivel";
        }

        return "Base 8 MB";
    }

    private void UpdateEspDashHistory(string? deviceId, DeviceSnapshot? snapshot)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || snapshot is null)
        {
            return;
        }

        var sequence = snapshot.TelemetrySequence;
        if (espDashLastSequenceByDeviceId.TryGetValue(deviceId, out var lastSequence) && lastSequence.HasValue && sequence.HasValue && lastSequence.Value == sequence.Value)
        {
            return;
        }

        espDashLastSequenceByDeviceId[deviceId] = sequence;

        if (!espDashLoopHistoryByDeviceId.TryGetValue(deviceId, out var loopHistory))
        {
            loopHistory = new Queue<int>(EspDashHistorySampleCapacity);
            espDashLoopHistoryByDeviceId[deviceId] = loopHistory;
        }

        if (!espDashHeapHistoryByDeviceId.TryGetValue(deviceId, out var heapHistory))
        {
            heapHistory = new Queue<int>(EspDashHistorySampleCapacity);
            espDashHeapHistoryByDeviceId[deviceId] = heapHistory;
        }

        var loop = snapshot.LoopLoadPercent is int rawLoop ? Math.Clamp(rawLoop, 0, 100) : 0;
        var heapKb = snapshot.FreeHeapBytes is long freeHeap
            ? Math.Clamp((int)Math.Round(freeHeap / 1024d), 0, 320)
            : 0;

        if (loopHistory.Count >= EspDashHistorySampleCapacity)
        {
            _ = loopHistory.Dequeue();
        }

        if (heapHistory.Count >= EspDashHistorySampleCapacity)
        {
            _ = heapHistory.Dequeue();
        }

        loopHistory.Enqueue(loop);
        heapHistory.Enqueue(heapKb);
    }

    private IReadOnlyList<int> GetEspDashLoopSeries(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || !espDashLoopHistoryByDeviceId.TryGetValue(deviceId, out var series))
        {
            return Array.Empty<int>();
        }

        return series.ToArray();
    }

    private IReadOnlyList<int> GetEspDashHeapSeries(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || !espDashHeapHistoryByDeviceId.TryGetValue(deviceId, out var series))
        {
            return Array.Empty<int>();
        }

        return series.ToArray();
    }

    private void ApplyEspDashSection(bool hasSelection, DeviceSnapshot? snapshot, IReadOnlyList<int> loopSeries, IReadOnlyList<int> heapSeries)
    {
        try
        {
            var show = hasSelection && snapshot is not null;
            EspDashSectionBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show || snapshot is null)
            {
                return;
            }

            EspDashUptimeValueText.Text = FormatUptimeForEspDash(snapshot.UptimeSeconds);

            var fps = 0;
            if (snapshot.UptimeSeconds is int uptimeSeconds && uptimeSeconds > 0 && snapshot.StreamFramesReceived is uint streamFrames)
            {
                fps = Math.Clamp((int)Math.Round(streamFrames / (double)uptimeSeconds), 0, 240);
            }

            EspDashFpsValueText.Text = $"{fps} fps";
            var heapKb = snapshot.FreeHeapBytes is long freeHeap ? Math.Clamp((int)Math.Round(freeHeap / 1024d), 0, 4096) : 0;
            EspDashHeapValueText.Text = $"{heapKb} KB";
            EspDashHeapSubText.Text = BuildHeapFragmentationSummary(snapshot);

            var loopPercent = snapshot.LoopLoadPercent is int loopLoad ? Math.Clamp(loopLoad, 0, 100) : 0;
            EspGaugePercentText.Text = $"{loopPercent}%";
            const double arcLength = 157d;
            var gaugeOffset = arcLength - Math.Round((arcLength * loopPercent) / 100d, 2, MidpointRounding.AwayFromZero);
            EspGaugeFillPath.StrokeDashOffset = double.IsFinite(gaugeOffset) ? gaugeOffset : arcLength;

            StreamFramesReceivedText.Text = (snapshot.StreamFramesReceived ?? 0).ToString("N0", CultureInfo.InvariantCulture);
            StreamFramesAppliedText.Text = (snapshot.StreamFramesApplied ?? 0).ToString("N0", CultureInfo.InvariantCulture);
            StreamGapCountText.Text = (snapshot.StreamSequenceGapCount ?? 0).ToString(CultureInfo.InvariantCulture);
            StreamInvalidCountText.Text = (snapshot.StreamInvalidFrameCount ?? 0).ToString(CultureInfo.InvariantCulture);
            StreamLastSequenceText.Text = (snapshot.StreamLastSequence ?? 0).ToString("N0", CultureInfo.InvariantCulture);
            StreamFramesAppliedText.Foreground = new SolidColorBrush(Color.FromArgb(255, 108, 203, 95));
            StreamSuccessRateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 108, 203, 95));
            StreamGapCountText.Foreground = new SolidColorBrush(Color.FromArgb(255, 252, 225, 0));
            StreamInvalidCountText.Foreground = (snapshot.StreamInvalidFrameCount ?? 0) > 0
                ? new SolidColorBrush(Color.FromArgb(255, 255, 153, 164))
                : new SolidColorBrush(Color.FromArgb(255, 108, 203, 95));

            var successRate = 0;
            if (snapshot.StreamFramesReceived is uint rx && rx > 0 && snapshot.StreamFramesApplied is uint applied)
            {
                successRate = Math.Clamp((int)Math.Round((applied / (double)rx) * 100d), 0, 100);
            }

            StreamSuccessRateText.Text = $"{successRate}%";

            RenderLineChart(EspDashLoopChartCanvas, EspDashLoopChartLine, EspDashLoopChartFill, loopSeries, 100);
            RenderLineChart(EspDashHeapChartCanvas, EspDashHeapChartLine, EspDashHeapChartFill, heapSeries, 320);
        }
        catch (Exception ex)
        {
            LogRenderException("espdash", snapshot?.DeviceId, snapshot, ex);
            EspDashSectionBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyConnectivitySection(bool hasSelection, DeviceSnapshot? snapshot)
    {
        try
        {
            ConnectivitySectionBorder.Visibility = hasSelection && snapshot is not null ? Visibility.Visible : Visibility.Collapsed;
            if (snapshot is null)
            {
                return;
            }

            var wifiLabel = snapshot.WifiState?.ToLowerInvariant() switch
            {
                "connected" => "Conectado",
                "disconnected" => "Desconectado",
                "portal" => "Modo configuracao",
                "connecting" => "Conectando",
                _ => snapshot.WifiState ?? "-",
            };

            ConnectivityWifiStateText.Text = $"Rede Wi-Fi: {wifiLabel}";
            ConnectivityPortalStateText.Text = $"Modo configuracao: {(snapshot.ProvisioningPortalActive == true ? "Ativo" : "-")}";
            ConnectivityUptimeText.Text = $"Ligado ha: {FormatUptimeForEspDash(snapshot.UptimeSeconds)}";
            ConnectivityLastEventText.Text = $"Ultimo evento de rede: {snapshot.LastWifiEvent ?? "-"}";
            ConnectivityAuxLedText.Text = $"LED auxiliar: {(snapshot.AuxLedAvailable == true ? "Disponivel" : snapshot.AuxLedAvailable == false ? "Nao disponivel" : "-")}";
        }
        catch (Exception ex)
        {
            LogRenderException("connectivity", snapshot?.DeviceId, snapshot, ex);
            ConnectivitySectionBorder.Visibility = Visibility.Collapsed;
        }
    }

    private static string BuildHeapFragmentationSummary(DeviceSnapshot? snapshot)
    {
        if (snapshot?.FreeHeapBytes is not long freeHeap || snapshot.LargestHeapBlockBytes is not long largest || freeHeap <= 0)
        {
            return "Fragmentacao -";
        }

        var fragmentation = Math.Clamp((int)Math.Round((1d - (largest / (double)freeHeap)) * 100d), 0, 100);
        return $"Fragmentacao {fragmentation}%";
    }

    private static string FormatUptimeForEspDash(int? uptimeSeconds)
    {
        if (!uptimeSeconds.HasValue || uptimeSeconds.Value < 0)
        {
            return "-";
        }

        var uptime = TimeSpan.FromSeconds(uptimeSeconds.Value);
        return $"{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
    }

    private static void RenderLineChart(Canvas canvas, Polyline line, Polygon fill, IReadOnlyList<int> values, int maxValue)
    {
        try
        {
            var width = canvas.ActualWidth > 4d && double.IsFinite(canvas.ActualWidth) ? canvas.ActualWidth : 420d;
            var height = canvas.ActualHeight > 4d && double.IsFinite(canvas.ActualHeight) ? canvas.ActualHeight : 120d;
            Canvas.SetLeft(line, 0);
            Canvas.SetTop(line, 0);
            Canvas.SetLeft(fill, 0);
            Canvas.SetTop(fill, 0);

            if (values.Count == 0)
            {
                line.Points = new PointCollection();
                fill.Points = new PointCollection();
                return;
            }

            var points = new PointCollection();
            var fillPoints = new PointCollection
            {
                new Windows.Foundation.Point(0, height),
            };

            var max = Math.Max(maxValue, 1);
            for (var index = 0; index < values.Count; index++)
            {
                var x = values.Count == 1
                    ? width
                    : (index / (double)(values.Count - 1)) * width;
                var normalized = Math.Clamp(values[index], 0, max) / (double)max;
                var y = height - (normalized * (height - 6d));
                if (!double.IsFinite(x) || !double.IsFinite(y))
                {
                    continue;
                }

                var point = new Windows.Foundation.Point(x, y);
                points.Add(point);
                fillPoints.Add(point);
            }

            fillPoints.Add(new Windows.Foundation.Point(width, height));
            line.Points = points;
            fill.Points = fillPoints;
        }
        catch
        {
            line.Points = new PointCollection();
            fill.Points = new PointCollection();
        }
    }

    private static string ResolveDashboardPlaceholder(bool hasSelection, DeviceMetricsPresentation metrics)
    {
        if (!hasSelection)
        {
            return "Selecione um dispositivo para ver o status";
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

    private static string BuildBrightnessValueLabel(DeviceSnapshot? snapshot)
    {
        var cap = snapshot?.BrightnessCap is int capValue
            ? Math.Clamp(capValue, SafeBrightnessMin, SafeBrightnessMax)
            : SafeBrightnessMax;
        return $"{cap}/160";
    }

    private static string BuildBrightnessStatusLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Brilho atual no painel: -";
        }

        var applied = snapshot.BrightnessApplied is int appliedValue
            ? Math.Clamp(appliedValue, 0, 255).ToString(CultureInfo.InvariantCulture)
            : "-";
        return $"Brilho atual no painel: {applied}";
    }

    private static string BuildHeartbeatLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Sinal de vida: - | LED de teste: - | Intensidade do LED: -";
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
        return $"Sinal de vida: #{sequence} | LED de teste: {ledState} | Intensidade do LED: {duty}";
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
        try
        {
            var metrics = DeviceMetricsFormatter.Build(selectedSnapshot);
            ApplyDashboard(selectionDeviceId: selected.DeviceId, hasSelection: true, snapshot: selectedSnapshot, metrics);
        }
        catch (Exception ex)
        {
            LogRenderException("selection", selected.DeviceId, selectedSnapshot, ex);
            ApplyOfflineDashboardFallback(selectedSnapshot, OfflineDashboardFallbackText);
            lastRenderedDashboardSignature = BuildOfflineDashboardSignature(selected.DeviceId, selectedSnapshot);
        }

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

