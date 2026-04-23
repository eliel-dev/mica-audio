using App.WinUI.Models.Apps;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Firmware;
using App.WinUI.ViewModels;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Output.Led;
using Windows.ApplicationModel.DataTransfer;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
// DOCS: docs/wiki/modules/app-winui.md
// DOCS: docs/handoffs/2026-04-20-remove-usb-flash-flow.md
public sealed partial class DevicesPage : Page
{
    private const string LocalDraftScope = "__local__";
    private readonly List<DeviceListItem> allItems = new();
    private readonly List<DeviceListItem> visibleItems = new();
    private readonly Dictionary<string, DeviceListVisualItem> renderedItemsByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> renderedOrder = new();
    private readonly Dictionary<string, AppCatalogItem> appCatalogById = new(StringComparer.OrdinalIgnoreCase);
    private readonly DevicesPageViewModel viewModel;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly PrecompiledFirmwareService firmwareService;
    private readonly IAppCatalogService appCatalogService;
    private readonly IAppModifierStateStore modifierStore;
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
    private string? lastAppliedPreviewConfigSignature;
    private int previewConfigRefreshVersion;
    private bool dashboardWebViewInitialized;
    private bool dashboardWebViewReady;
    private bool dashboardWebViewEventsHooked;
    private Uri? dashboardWebViewUri;
    private string? lastDashboardPostedDeviceId;

    internal DevicesPage(
        DevicesPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        PrecompiledFirmwareService firmwareService,
        IAppCatalogService appCatalogService,
        IAppModifierStateStore modifierStore,
        SettingsRepository settingsRepository,
        AppSettingsDomainService settingsDomainService,
        SimulatorLedOutput simulatorLedOutput)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.firmwareService = firmwareService;
        this.appCatalogService = appCatalogService;
        this.modifierStore = modifierStore;
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

    private IAppCatalogService? AppCatalogService => appCatalogService;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            SetDetailsPaneVisible(false);
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
        await RefreshVisiblePreviewConfigsAsync(force: true).ConfigureAwait(true);

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
        dashboardWebViewReady = false;
        lastDashboardPostedDeviceId = null;
        dashboardWebViewUri = null;
        lastAppliedPreviewConfigSignature = null;
        previewConfigRefreshVersion++;
        DetachDashboardWebViewBridge();
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

    private async void GeneratePairingCodeCore()
    {
        if (DeviceOps is null)
        {
            return;
        }

        try
        {
            var code = await DeviceOps.CreatePairingCodeAsync(TimeSpan.FromMinutes(10)).ConfigureAwait(true);
            ShowPairingCodeNotice(code.Code, code.ExpiresAtUtc);
            AddLocalLog($"Codigo de pareamento gerado: {code.Code}.");
        }
        catch (Exception ex)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Falha ao gerar codigo de pareamento.");
            AddLocalLog($"Falha ao gerar codigo de pareamento: {ex.Message}");
        }
    }

    private async void OnTestLedClicked(object sender, RoutedEventArgs e)
    {
        await ExecuteTestLedAsync().ConfigureAwait(false);
    }

    private void OnBrightnessSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressBrightnessSliderEvents)
        {
            return;
        }

        var normalized = Math.Clamp((int)Math.Round(e.NewValue), SafeBrightnessMin, SafeBrightnessMax);
        var percentage = Math.Round((double)normalized / SafeBrightnessMax * 100.0);
        DashboardBrightnessValueText.Text = $"{percentage:F0}%";
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
        await ExecuteRemoveDeviceAsync().ConfigureAwait(false);
    }

    private void OnCopyHostClicked(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(currentState.ServerBaseAddress);
        Clipboard.SetContent(data);
    }

    private void OnCopyDashboardLinkClicked(object sender, RoutedEventArgs e)
    {
        var selectedDeviceId = GetSelectedDeviceId();
        if (string.IsNullOrWhiteSpace(selectedDeviceId))
        {
            ShowInlineStatusMessage(InfoBarSeverity.Warning, "Selecione um dispositivo para copiar o link do dashboard.");
            AddLocalLog("Nao foi possivel copiar o link do dashboard: nenhum dispositivo selecionado.");
            return;
        }

        var dashboardUri = BuildDashboardShareUri(currentState.ServerBaseAddress, selectedDeviceId);
        if (dashboardUri is null)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Warning, "Servidor local ainda nao esta acessivel na rede.");
            AddLocalLog("Nao foi possivel copiar o link do dashboard: URL local nao compartilhavel.");
            return;
        }

        var data = new DataPackage();
        data.SetText(dashboardUri.ToString());
        Clipboard.SetContent(data);

        ShowInlineStatusMessage(InfoBarSeverity.Success, $"Link do dashboard copiado: {dashboardUri}");
        AddLocalLog($"Link do dashboard copiado: {dashboardUri}");
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
        if (!brightnessCommitPending || suppressBrightnessSliderEvents || DashboardBrightnessSlider is null)
        {
            return;
        }

        brightnessCommitPending = false;
        await ExecuteSetBrightnessAsync(Math.Clamp((int)Math.Round(DashboardBrightnessSlider.Value), SafeBrightnessMin, SafeBrightnessMax))
            .ConfigureAwait(false);
    }

    private DeviceListItem? ResolveCommandDevice(string? requestedDeviceId = null)
    {
        var selected = GetSelectedDeviceItem();
        if (selected is not null
            && (string.IsNullOrWhiteSpace(requestedDeviceId)
                || string.Equals(selected.DeviceId, requestedDeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            return selected;
        }

        if (string.IsNullOrWhiteSpace(requestedDeviceId))
        {
            return null;
        }

        return allItems.FirstOrDefault(item =>
            string.Equals(item.DeviceId, requestedDeviceId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ExecuteTestLedAsync(string? requestedDeviceId = null)
    {
        var selected = ResolveCommandDevice(requestedDeviceId);
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

    private async Task ExecuteSetBrightnessAsync(int brightness, string? requestedDeviceId = null)
    {
        var selected = ResolveCommandDevice(requestedDeviceId);
        var ops = DeviceOps;
        if (selected is null || ops is null || selected.Status != DeviceStatus.Online)
        {
            return;
        }

        var result = await ops.SetBrightnessAsync(selected.DeviceId, Math.Clamp(brightness, SafeBrightnessMin, SafeBrightnessMax)).ConfigureAwait(false);
        if (result.Accepted && result.Completed && result.Success)
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(result.Message) ? result.ErrorCode : result.Message;
        AddLocalLog($"Falha ao ajustar brilho em {selected.DeviceId}: {reason ?? "erro desconhecido"}");
    }

    private async Task ExecuteRemoveDeviceAsync(string? requestedDeviceId = null)
    {
        var selected = ResolveCommandDevice(requestedDeviceId);
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

        if (!await ops.RemoveDeviceAsync(selected.DeviceId).ConfigureAwait(true))
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Falha ao remover dispositivo.");
            AddLocalLog($"Falha ao remover dispositivo: {selected.DeviceId}");
            return;
        }

        selectedDeviceId = null;
        SetListSelectedItem(null);

        if (isOnline)
        {
            if (revokeSucceeded)
            {
                ShowInlineStatusMessage(InfoBarSeverity.Success, $"Dispositivo revogado e removido: {selected.DeviceId}");
                AddLocalLog($"Dispositivo revogado e removido localmente: {selected.DeviceId}");
            }
            else
            {
                ShowInlineStatusMessage(InfoBarSeverity.Warning, $"Dispositivo removido localmente; revogacao nao concluida: {selected.DeviceId}");
                AddLocalLog($"Dispositivo removido localmente, mas revogacao falhou: {selected.DeviceId} ({revokeFailureMessage ?? "erro desconhecido"})");
            }
        }
        else
        {
            ShowInlineStatusMessage(InfoBarSeverity.Success, $"Dispositivo removido: {selected.DeviceId}");
            AddLocalLog($"Dispositivo removido localmente: {selected.DeviceId}");
        }

        ApplySelectionDetails();
        ApplyButtonState();
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

        public void SetPreviewConfig(IReadOnlyDictionary<string, string>? values)
        {
            RowControl.SetPreviewConfig(values);
        }
    }
}
