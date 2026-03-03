using App.WinUI.Models.Apps;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Firmware;
using App.WinUI.ViewModels;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
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
    private readonly IAppCatalogService appCatalogService;
    private readonly SettingsRepository settingsRepository;
    private readonly AppSettingsDomainService settingsDomainService;

    private int lastRenderedLogCount;
    private string lastRenderedLogTail = string.Empty;
    private DeviceOperationsState currentState = new();
    private bool appCatalogLoadAttempted;
    private DeviceLifecycleThresholds lifecycleThresholds = DeviceLifecycleThresholds.Default;
    private bool isApplyingDeviceList;
    private IReadOnlyList<DeviceSnapshot>? pendingDeviceListSnapshot;
    private string? lastAppliedDeviceListSignature;
    private string? currentSelectedPreviewDeviceId;
    private string? currentSelectedPreviewAppId;
    private string? lastSelectedPreviewPlaceholderMessage;
    private bool isSelectedPreviewRunning;

    internal DevicesPage(
        DevicesPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        PrecompiledFirmwareService firmwareService,
        IAppCatalogService appCatalogService,
        SettingsRepository settingsRepository,
        AppSettingsDomainService settingsDomainService)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.firmwareService = firmwareService;
        this.appCatalogService = appCatalogService;
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;
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
            LogsTextBox.Text = "Servico de dispositivos indisponivel.";
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
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
            DeviceOps.DeviceListChanged -= OnDeviceOpsDeviceListChanged;
            DeviceOps.SetDevicesPageVisible(false);
        }

        SelectedDevicePreview.Stop();
        isSelectedPreviewRunning = false;
        currentSelectedPreviewDeviceId = null;
        currentSelectedPreviewAppId = null;
        lastSelectedPreviewPlaceholderMessage = null;
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
            UpdateLogs(currentState.Logs);
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
            UpdateLogs(currentState.Logs);
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
        UpdateLogs(currentState.Logs);
    }

    private async void OnEnterProvisioningClicked(object sender, RoutedEventArgs e)
    {
        await RunSelectedCommandAsync(DeviceCommandType.EnterProvisioning).ConfigureAwait(false);
    }

    private async void OnRevokeClicked(object sender, RoutedEventArgs e)
    {
        await RunSelectedCommandAsync(DeviceCommandType.RevokeAndRestart).ConfigureAwait(false);
    }

    private async void OnTestLedClicked(object sender, RoutedEventArgs e)
    {
        await RunSelectedCommandAsync(DeviceCommandType.TestLed).ConfigureAwait(false);
    }

    private async void OnRemoveDeviceClicked(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedDeviceItem();
        var ops = DeviceOps;
        if (selected is null || ops is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Remover dispositivo",
            PrimaryButtonText = "Remover",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            Content = "Isso remove o dispositivo da lista e do registro local deste app. Nenhum comando sera enviado ao ESP. Se quiser alterar o dispositivo fisico, use Revogar quando ele estiver online.",
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (ops.RemoveDevice(selected.DeviceId))
        {
            DevicesList.SelectedItem = null;
            PairingCodeText.Severity = InfoBarSeverity.Success;
            PairingCodeText.Message = $"Dispositivo removido: {selected.DeviceId}";
            AddLocalLog($"Dispositivo removido localmente: {selected.DeviceId}");
            ApplySelectionDetails();
            ApplyButtonState();
            return;
        }

        PairingCodeText.Severity = InfoBarSeverity.Error;
        PairingCodeText.Message = "Falha ao remover dispositivo.";
        AddLocalLog($"Falha ao remover dispositivo: {selected.DeviceId}");
        UpdateLogs(currentState.Logs);
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

    private async Task SaveFirmwareAsync()
    {
        var service = FirmwareService;
        if (service is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: servico indisponivel.";
            AddLocalLog("Servico de firmware indisponivel.");
            UpdateLogs(currentState.Logs);
            return;
        }

        var option = service.GetOptions().FirstOrDefault();
        if (option is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: opcao indisponivel.";
            AddLocalLog("Nenhuma opcao de firmware disponivel.");
            UpdateLogs(currentState.Logs);
            return;
        }

        if (!service.TryResolveSource(option.Id, out _, out var resolveError))
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: arquivo ausente.";
            AddLocalLog(resolveError);
            UpdateLogs(currentState.Logs);
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
            UpdateLogs(currentState.Logs);
            return;
        }

        if (targetFile is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Warning;
            PairingCodeText.Message = "Firmware: download cancelado.";
            AddLocalLog("Salvamento de firmware cancelado pelo usuario.");
            UpdateLogs(currentState.Logs);
            return;
        }

        try
        {
            await service.CopyToAsync(option.Id, targetFile.Path).ConfigureAwait(true);
            PairingCodeText.Severity = InfoBarSeverity.Success;
            PairingCodeText.Message = $"Firmware: salvo em {targetFile.Name}.";
            AddLocalLog($"Firmware salvo em: {targetFile.Path}");
            UpdateLogs(currentState.Logs);
        }
        catch (Exception ex)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: erro ao salvar arquivo.";
            AddLocalLog($"Falha ao salvar firmware: {ex.Message}");
            UpdateLogs(currentState.Logs);
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

    // DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
    private void ApplyState(DeviceOperationsState state)
    {
        currentState = state;

        UpdateLogs(state.Logs);

        CommandProgressRing.IsActive = state.CommandInProgress;
        CommandProgressRing.Visibility = state.CommandInProgress ? Visibility.Visible : Visibility.Collapsed;
        CommandStatusText.Text = state.CommandStatus;
        CommandPercentText.Text = $"{Math.Clamp(state.CommandPercent, 0, 100)}%";

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
                var ip = string.IsNullOrWhiteSpace(device.LastKnownIp) ? "-" : device.LastKnownIp;
                var rssi = device.LastKnownRssi?.ToString() ?? "-";

                allItems.Add(new DeviceListItem
                {
                    DeviceId = device.DeviceId,
                    Name = device.Name,
                    Status = device.Status,
                    StatusLine = $"{presence.PrimaryStateLabel} | {presence.SecondaryStateLabel} | {presence.LastSeenLabel} | Perfil {profile} | IP {ip} | RSSI {rssi}",
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

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(GetSelectedDeviceId());
        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void ApplyFilter(string? selectedDeviceId)
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        visibleItems.Clear();

        IEnumerable<DeviceListItem> source = allItems;
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.DeviceId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.StatusLine.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.AppName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        visibleItems.AddRange(source);
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

    private AppCatalogItem? ResolvePreviewApp(DeviceListItem item)
    {
        return DevicePreviewResolver.Resolve(item.AppId, item.AppName, appCatalogById);
    }

    private void AddLocalLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var entries = currentState.Logs.ToList();
        entries.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        UpdateLogs(entries);
    }

    private void UpdateLogs(IReadOnlyList<string> entries)
    {
        var count = entries.Count;
        if (count == 0)
        {
            if (lastRenderedLogCount != 0)
            {
                LogsTextBox.Text = string.Empty;
                lastRenderedLogCount = 0;
                lastRenderedLogTail = string.Empty;
            }

            return;
        }

        var tail = entries[^1];
        if (lastRenderedLogCount == count && string.Equals(lastRenderedLogTail, tail, StringComparison.Ordinal))
        {
            return;
        }

        LogsTextBox.Text = string.Join("\r\n", entries) + "\r\n";
        lastRenderedLogCount = count;
        lastRenderedLogTail = tail;
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

    private void ApplySelectionDetails()
    {
        var selectedVisual = GetSelectedVisualItem();
        if (selectedVisual is null)
        {
            SelectedDeviceTitleText.Text = "Nenhum dispositivo selecionado";
            SelectedDeviceSubtitleText.Text = "-";
            SelectedDeviceRegistrationText.Text = "-";
            SelectedDeviceAppText.Text = "App ativo: -";
            ShowSelectedPreviewPlaceholder("Selecione um dispositivo para ver o app ativo");
            return;
        }

        var selected = selectedVisual.Source;
        SelectedDeviceTitleText.Text = selected.Name;
        SelectedDeviceSubtitleText.Text = selected.StatusLine;
        SelectedDeviceRegistrationText.Text = BuildRegistrationLine(selected);
        SelectedDeviceAppText.Text = DevicePreviewVisibilityPolicy.BuildSelectedAppLabel(selected.Status, selected.AppName);

        if (!DevicePreviewVisibilityPolicy.ShouldShowSelectedPreview(selected.Status, selectedVisual.PreviewItem))
        {
            if (selected.Status == DeviceStatus.Online)
            {
                ShowSelectedPreviewPlaceholder("Nenhum app ativo reportado pelo dispositivo");
            }
            else
            {
                ShowSelectedPreviewPlaceholder("Dispositivo offline");
            }

            return;
        }

        ShowSelectedPreview(selected.DeviceId, selectedVisual.PreviewItem!);
    }

    private void ShowSelectedPreview(string deviceId, AppCatalogItem previewItem)
    {
        var previewAppId = previewItem.Id;
        if (isSelectedPreviewRunning
            && string.Equals(currentSelectedPreviewDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(currentSelectedPreviewAppId, previewAppId, StringComparison.OrdinalIgnoreCase))
        {
            if (SelectedDevicePreview.Visibility != Visibility.Visible)
            {
                SelectedDevicePreview.Visibility = Visibility.Visible;
            }

            if (SelectedDevicePreviewPlaceholderText.Visibility != Visibility.Collapsed)
            {
                SelectedDevicePreviewPlaceholderText.Visibility = Visibility.Collapsed;
            }

            return;
        }

        SelectedDevicePreview.Stop();
        SelectedDevicePreview.Bind(previewItem);
        SelectedDevicePreview.SetSelected(true);
        SelectedDevicePreview.Visibility = Visibility.Visible;
        SelectedDevicePreviewPlaceholderText.Visibility = Visibility.Collapsed;
        SelectedDevicePreview.Start();

        isSelectedPreviewRunning = true;
        currentSelectedPreviewDeviceId = deviceId;
        currentSelectedPreviewAppId = previewAppId;
        lastSelectedPreviewPlaceholderMessage = null;
    }

    private void ShowSelectedPreviewPlaceholder(string message)
    {
        var placeholderAlreadyApplied = !isSelectedPreviewRunning
            && SelectedDevicePreview.Visibility == Visibility.Collapsed
            && SelectedDevicePreviewPlaceholderText.Visibility == Visibility.Visible
            && string.Equals(lastSelectedPreviewPlaceholderMessage, message, StringComparison.Ordinal);

        if (!placeholderAlreadyApplied)
        {
            if (isSelectedPreviewRunning)
            {
                SelectedDevicePreview.Stop();
            }

            SelectedDevicePreview.SetSelected(false);
            SelectedDevicePreview.Visibility = Visibility.Collapsed;
            SelectedDevicePreviewPlaceholderText.Text = message;
            SelectedDevicePreviewPlaceholderText.Visibility = Visibility.Visible;
        }

        isSelectedPreviewRunning = false;
        currentSelectedPreviewDeviceId = null;
        currentSelectedPreviewAppId = null;
        lastSelectedPreviewPlaceholderMessage = message;
    }

    private void ApplyButtonState()
    {
        var selected = GetSelectedDeviceItem();
        var canRunCommand = selected is not null
            && selected.Presence.CanRunCommands
            && !currentState.CommandInProgress;
        var canRemove = selected is not null && !currentState.CommandInProgress;

        EnterProvisioningButton.IsEnabled = canRunCommand;
        RevokeButton.IsEnabled = canRunCommand;
        TestLedButton.IsEnabled = canRunCommand;
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

