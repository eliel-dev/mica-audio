using App.WinUI.Models.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
public sealed partial class DevicesPage : Page
{
    private readonly List<DeviceListItem> allItems = new();
    private readonly List<DeviceListItem> visibleItems = new();
    private readonly List<DeviceListVisualItem> renderedItems = new();
    private readonly DeviceOperationsCoordinator deviceOps;

    private int lastRenderedLogCount;
    private string lastRenderedLogTail = string.Empty;
    private DeviceOperationsState currentState = new();

    public DevicesPage(IServiceProvider services)
        : this(services.GetRequiredService<DeviceOperationsCoordinator>())
    {
    }

    internal DevicesPage(DeviceOperationsCoordinator deviceOps)
    {
        this.deviceOps = deviceOps;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => deviceOps;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            LogsTextBox.Text = "Serviço de dispositivos indisponível.";
            return;
        }

        DeviceOps.StateChanged += OnDeviceOpsStateChanged;
        DeviceOps.DeviceListChanged += OnDeviceOpsDeviceListChanged;
        DeviceOps.SetDevicesPageVisible(true);
        DeviceOps.RequestRefresh();

        ApplyState(DeviceOps.GetStateSnapshot());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
            DeviceOps.DeviceListChanged -= OnDeviceOpsDeviceListChanged;
            DeviceOps.SetDevicesPageVisible(false);
        }

        ClearRenderedItems();
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
        DeviceOps?.RequestRefresh();
    }

    private void OnGeneratePairingCodeClicked(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            return;
        }

        var code = DeviceOps.CreatePairingCode(TimeSpan.FromMinutes(10));
        PairingCodeText.Message = $"Pareamento: {code.Code} (expira {code.ExpiresAtUtc:HH:mm:ss} UTC)";
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

    // DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
    private void ApplyState(DeviceOperationsState state)
    {
        currentState = state;

        ApplyDevices(state.DeviceListSnapshot);
        UpdateLogs(state.Logs);

        CommandProgressRing.IsActive = state.CommandInProgress;
        CommandProgressRing.Visibility = state.CommandInProgress ? Visibility.Visible : Visibility.Collapsed;
        CommandStatusText.Text = state.CommandStatus;
        CommandPercentText.Text = $"{Math.Clamp(state.CommandPercent, 0, 100)}%";

        var refreshText = state.LastRefreshUtc == default
            ? "sem atualização"
            : state.LastRefreshUtc.ToLocalTime().ToString("HH:mm:ss");
        ServerInfoText.Text = $"Servidor: {state.ServerBaseAddress} | mDNS: _micaaudio._tcp | Atualizado: {refreshText}";

        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        var selectedId = GetSelectedDeviceId();

        allItems.Clear();
        foreach (var device in devices.Where(static d => d.Status == DeviceStatus.Online))
        {
            allItems.Add(new DeviceListItem
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                StatusLine = $"{device.Status} | Perfil {device.Profile} | IP {device.LastKnownIp ?? "-"} | RSSI {device.LastKnownRssi?.ToString() ?? "-"}",
                AppId = string.IsNullOrWhiteSpace(device.ActiveAppId) ? string.Empty : device.ActiveAppId!,
                AppName = string.IsNullOrWhiteSpace(device.ActiveAppName) ? "-" : device.ActiveAppName!,
            });
        }

        ApplyFilter(selectedId);
        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(GetSelectedDeviceId());
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
        RebuildRenderedItems(selectedDeviceId);
    }

    private void RebuildRenderedItems(string? selectedDeviceId)
    {
        ClearRenderedItems();

        foreach (var item in visibleItems)
        {
            var previewModel = BuildPreviewModel(item);
            var visualItem = new DeviceListVisualItem(item, previewModel);
            renderedItems.Add(visualItem);
        }

        DevicesList.ItemsSource = null;
        DevicesList.ItemsSource = renderedItems;

        if (!string.IsNullOrWhiteSpace(selectedDeviceId))
        {
            var selectedVisual = renderedItems.FirstOrDefault(item => string.Equals(item.DeviceId, selectedDeviceId, StringComparison.OrdinalIgnoreCase));
            if (selectedVisual is not null)
            {
                DevicesList.SelectedItem = selectedVisual;
            }
        }

        UpdateDeviceRowSelection();
    }

    private void ClearRenderedItems()
    {
        foreach (var item in renderedItems)
        {
            item.StopPreview();
        }

        renderedItems.Clear();
    }

    private static AppCatalogItem BuildPreviewModel(DeviceListItem item)
    {
        var kind = ResolvePreviewKind(item.AppId, item.AppName);
        var category = kind switch
        {
            "clock" => "relógio",
            "weather" => "clima",
            _ => "geral",
        };

        return new AppCatalogItem
        {
            Id = string.IsNullOrWhiteSpace(item.AppId) ? "device-preview" : item.AppId,
            Name = item.AppName,
            Category = category,
            Preview = new AppPreviewDefinition
            {
                Kind = kind,
                Speed = 1f,
            },
        };
    }

    private static string ResolvePreviewKind(string appId, string appName)
    {
        var id = appId.Trim().ToLowerInvariant();
        var name = appName.Trim().ToLowerInvariant();

        if (id.Contains("weather") || id.Contains("clima") || name.Contains("weather") || name.Contains("clima") || id.Contains("accuweather"))
        {
            return "weather";
        }

        if (id.Contains("clock") || id.Contains("relog") || id.Contains("relóg") || name.Contains("clock") || name.Contains("relog") || name.Contains("relóg") || id.Contains("analogclock"))
        {
            return "clock";
        }

        return "decorative";
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

    private DeviceListItem? GetSelectedDeviceItem()
    {
        return DevicesList.SelectedItem switch
        {
            DeviceListVisualItem visual => visual.Source,
            DeviceListItem item => item,
            _ => null,
        };
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
        foreach (var item in renderedItems)
        {
            item.SetSelectedVisual(string.Equals(item.DeviceId, selectedId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void ApplySelectionDetails()
    {
        var selected = GetSelectedDeviceItem();
        if (selected is null)
        {
            SelectedDeviceTitleText.Text = "Nenhum dispositivo selecionado";
            SelectedDeviceSubtitleText.Text = "-";
            SelectedDeviceAppText.Text = "App ativo: -";
            return;
        }

        SelectedDeviceTitleText.Text = selected.Name;
        SelectedDeviceSubtitleText.Text = selected.StatusLine;
        SelectedDeviceAppText.Text = $"App ativo: {selected.AppName}";
    }

    private void ApplyButtonState()
    {
        var hasSelection = GetSelectedDeviceItem() is not null;
        var canRunCommand = hasSelection && !currentState.CommandInProgress;

        EnterProvisioningButton.IsEnabled = canRunCommand;
        RevokeButton.IsEnabled = canRunCommand;
        TestLedButton.IsEnabled = canRunCommand;
    }

    private sealed class DeviceListItem
    {
        public string DeviceId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string StatusLine { get; init; } = string.Empty;

        public string AppId { get; init; } = string.Empty;

        public string AppName { get; init; } = string.Empty;
    }

    private sealed class DeviceListVisualItem
    {
        public DeviceListVisualItem(DeviceListItem source, AppCatalogItem previewItem)
        {
            Source = source;
            Preview = new AppPreviewThumbnailControl
            {
                Width = 96,
                Height = 48,
            };

            Preview.Bind(previewItem);
            Preview.SetSelected(false);
            Preview.Start();
        }

        public DeviceListItem Source { get; }

        public string DeviceId => Source.DeviceId;

        public string Name => Source.Name;

        public string StatusLine => Source.StatusLine;

        public string AppName => Source.AppName;

        public AppPreviewThumbnailControl Preview { get; }

        public void SetSelectedVisual(bool selected)
        {
            Preview.SetSelected(selected);
        }

        public void StopPreview()
        {
            Preview.Stop();
        }
    }
}


