using App.WinUI.Infrastructure.Serial;
using App.WinUI.Models.Apps;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Devices.Onboarding;
using App.WinUI.Services.Firmware;
using App.WinUI.ViewModels;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Output.Led;
using Windows.ApplicationModel.DataTransfer;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03---fase-9-wave-2-e-wave-3-monolitos-do-app-decompostos
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
    private const int SafeBrightnessMin = 30;
    private const int SafeBrightnessMax = 160;
    private const int HeapTotalBytesBaseline = 320000;
    private const int PsramTotalBytesBaseline = 8000000;
    private const string OfflineDashboardFallbackText = "Offline: exibindo snapshot seguro";
    private const string PendingTelemetryFallbackText = "Online: aguardando primeira telemetria do dispositivo";

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
    private string? selectedDeviceId;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? previewPumpTimer;
    private bool suppressBrightnessSliderEvents;
    private bool suppressDeviceSelectionChanged;
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
        selectedDeviceId = null;
        suppressBrightnessSliderEvents = false;
        suppressDeviceSelectionChanged = false;
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
            selectedDeviceId = null;
            SetListSelectedItem(null);

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
            RowControl = new DeviceListRowControl
            {
                Tag = this,
            };
            RowControl.Bind(source.Name, previewItem, previewPlaceholderText);
        }

        public DeviceListItem Source { get; private set; }

        public string DeviceId => Source.DeviceId;

        public AppCatalogItem? PreviewItem { get; private set; }

        public DeviceListRowControl RowControl { get; }

        public void Update(DeviceListItem source, AppCatalogItem? previewItem, string previewPlaceholderText)
        {
            Source = source;
            PreviewItem = previewItem;
            RowControl.Bind(source.Name, previewItem, previewPlaceholderText);
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
}
