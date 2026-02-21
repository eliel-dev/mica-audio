using App.WinUI.Services.Devices;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
public sealed partial class DevicesPage : Page
{
    private readonly List<DeviceListItem> allItems = new();
    private readonly List<DeviceListItem> visibleItems = new();

    private int lastRenderedLogCount;
    private string lastRenderedLogTail = string.Empty;
    private DeviceOperationsState currentState = new();

    public DevicesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => App.DeviceOps;

    private void OnLoaded(object sender, RoutedEventArgs e)
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

        ApplyState(DeviceOps.GetStateSnapshot());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            return;
        }

        DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
        DeviceOps.DeviceListChanged -= OnDeviceOpsDeviceListChanged;
        DeviceOps.SetDevicesPageVisible(false);
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
        var selected = DevicesList.SelectedItem as DeviceListItem;
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
            ? "sem atualizacao"
            : state.LastRefreshUtc.ToLocalTime().ToString("HH:mm:ss");
        ServerInfoText.Text = $"Servidor: {state.ServerBaseAddress} | mDNS: _micaaudio._tcp | Atualizado: {refreshText}";

        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        var selectedId = (DevicesList.SelectedItem as DeviceListItem)?.DeviceId;

        allItems.Clear();
        foreach (var device in devices)
        {
            allItems.Add(new DeviceListItem
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                AppName = string.IsNullOrWhiteSpace(device.ActiveAppName) ? "-" : device.ActiveAppName!,
                StatusLine = $"{device.Status} | Perfil {device.Profile} | IP {device.LastKnownIp ?? "-"} | RSSI {device.LastKnownRssi?.ToString() ?? "-"}",
            });
        }

        ApplyFilter();

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            var match = visibleItems.FirstOrDefault(item => string.Equals(item.DeviceId, selectedId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                DevicesList.SelectedItem = match;
            }
        }

        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
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

        DevicesList.ItemsSource = null;
        DevicesList.ItemsSource = visibleItems;
    }

    private void UpdateLogs(IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
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
        if (lastRenderedLogCount == entries.Count && string.Equals(lastRenderedLogTail, tail, StringComparison.Ordinal))
        {
            return;
        }

        LogsTextBox.Text = string.Join("\r\n", entries) + "\r\n";
        lastRenderedLogCount = entries.Count;
        lastRenderedLogTail = tail;
    }

    private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void ApplySelectionDetails()
    {
        var selected = DevicesList.SelectedItem as DeviceListItem;
        if (selected is null)
        {
            SelectedDeviceTitleText.Text = "Nenhum dispositivo selecionado";
            SelectedDeviceSubtitleText.Text = "-";
            SelectedDeviceAppText.Text = "App ativo: -";
            return;
        }

        SelectedDeviceTitleText.Text = selected.Name;
        SelectedDeviceSubtitleText.Text = $"{selected.DeviceId} | {selected.StatusLine}";
        SelectedDeviceAppText.Text = $"App ativo: {selected.AppName}";
    }

    private void ApplyButtonState()
    {
        var selected = DevicesList.SelectedItem as DeviceListItem;
        var commandEnabled = selected is not null && !currentState.CommandInProgress;

        EnterProvisioningButton.IsEnabled = commandEnabled;
        RevokeButton.IsEnabled = commandEnabled;
        TestLedButton.IsEnabled = commandEnabled;
    }

    private sealed class DeviceListItem
    {
        public string DeviceId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string StatusLine { get; init; } = string.Empty;

        public string AppName { get; init; } = "-";

        public override string ToString() => $"{Name} ({DeviceId})";
    }
}
