using System.Diagnostics;
using App.WinUI.Services.Devices;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.ApplicationModel.DataTransfer;

namespace App.WinUI.Views;

public sealed class DevicesPage : Page
{
    private readonly List<DeviceListItem> items = new();
    private readonly ListView devicesList;
    private readonly TextBox logsTextBox;
    private readonly TextBlock pairingCodeText;
    private readonly TextBlock serverInfoText;
    private readonly Button enterProvisioningButton;
    private readonly Button revokeButton;
    private readonly Button testLedButton;
    private readonly Button otaButton;

    private readonly ProgressRing commandProgressRing;
    private readonly TextBlock commandStatusText;

    private readonly ProgressRing buildProgressRing;
    private readonly TextBlock buildStatusText;
    private readonly Button buildStableButton;
    private readonly Button buildDmaButton;

    private int lastRenderedLogCount;
    private string lastRenderedLogTail = string.Empty;
    private DeviceOperationsState currentState = new();

    public DevicesPage()
    {
        var root = new Grid
        {
            Padding = new Thickness(16),
            RowSpacing = 12,
            ColumnSpacing = 12,
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        actions.Children.Add(BuildButton("Gerar codigo pareamento", OnGeneratePairingCodeClicked));
        actions.Children.Add(BuildButton("Atualizar", OnRefreshClicked));

        enterProvisioningButton = BuildButton("Entrar provisioning", OnEnterProvisioningClicked);
        revokeButton = BuildButton("Revogar/Reiniciar", OnRevokeClicked);
        testLedButton = BuildButton("Testar LED", OnTestLedClicked);
        otaButton = BuildButton("Atualizar firmware OTA", OnOtaClicked);

        actions.Children.Add(enterProvisioningButton);
        actions.Children.Add(revokeButton);
        actions.Children.Add(testLedButton);
        actions.Children.Add(otaButton);
        actions.Children.Add(BuildButton("Copiar host", OnCopyHostClicked));

        commandProgressRing = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = false,
            Visibility = Visibility.Collapsed,
        };

        commandStatusText = new TextBlock
        {
            Text = "Comandos: pronto",
            Opacity = 0.85,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var commandStatusPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        commandStatusPanel.Children.Add(commandProgressRing);
        commandStatusPanel.Children.Add(commandStatusText);
        actions.Children.Add(commandStatusPanel);

        buildStableButton = BuildButton("Build stable + export", OnBuildStableClicked);
        buildDmaButton = BuildButton("Build dma_exp + export", OnBuildDmaClicked);
        actions.Children.Add(buildStableButton);
        actions.Children.Add(buildDmaButton);

        actions.Children.Add(BuildButton("Abrir pasta firmware", OnOpenFirmwareFolderClicked));

        var buildStatusPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        buildProgressRing = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = false,
            Visibility = Visibility.Collapsed,
        };

        buildStatusText = new TextBlock
        {
            Text = "Build: pronto",
            Opacity = 0.85,
            VerticalAlignment = VerticalAlignment.Center,
        };

        buildStatusPanel.Children.Add(buildProgressRing);
        buildStatusPanel.Children.Add(buildStatusText);
        actions.Children.Add(buildStatusPanel);

        root.Children.Add(actions);

        var content = new Grid
        {
            ColumnSpacing = 12,
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        Grid.SetRow(content, 1);

        devicesList = new ListView { SelectionMode = ListViewSelectionMode.Single };
        devicesList.ItemTemplate = BuildDeviceItemTemplate();
        devicesList.SelectionChanged += OnDeviceSelectionChanged;
        content.Children.Add(devicesList);

        var rightPanel = new Border
        {
            BorderBrush = Application.Current.Resources["AppSurfaceStrokeBrush"] as Microsoft.UI.Xaml.Media.Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
        };
        Grid.SetColumn(rightPanel, 1);

        var rightGrid = new Grid { RowSpacing = 8 };
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        serverInfoText = new TextBlock { Text = "Servidor: iniciando..." };
        rightGrid.Children.Add(serverInfoText);

        logsTextBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(logsTextBox, 1);
        ScrollViewer.SetVerticalScrollBarVisibility(logsTextBox, ScrollBarVisibility.Auto);
        rightGrid.Children.Add(logsTextBox);

        rightPanel.Child = rightGrid;
        content.Children.Add(rightPanel);
        root.Children.Add(content);

        pairingCodeText = new TextBlock { Text = "Pareamento: -" };
        Grid.SetRow(pairingCodeText, 2);
        root.Children.Add(pairingCodeText);

        Content = root;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => App.DeviceOps;

    private static Button BuildButton(string text, RoutedEventHandler clickHandler)
    {
        var button = new Button { Content = text };
        button.Click += clickHandler;
        return button;
    }

    private static DataTemplate BuildDeviceItemTemplate()
    {
        return (DataTemplate)XamlReader.Load(@"
<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
  <StackPanel Spacing='2'>
    <TextBlock Text='{Binding Name}' FontWeight='SemiBold'/>
    <TextBlock Text='{Binding DeviceId}' Opacity='0.8'/>
    <TextBlock Text='{Binding StatusLine}' Opacity='0.8'/>
  </StackPanel>
</DataTemplate>");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        devicesList.ItemsSource = items;

        if (DeviceOps is null)
        {
            logsTextBox.Text = "Servico de dispositivos indisponivel.";
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
        pairingCodeText.Text = $"Pareamento: {code.Code} (expira {code.ExpiresAtUtc:HH:mm:ss} UTC)";
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

    private async void OnOtaClicked(object sender, RoutedEventArgs e)
    {
        var selected = devicesList.SelectedItem as DeviceListItem;
        if (selected is null || DeviceOps is null)
        {
            return;
        }

        await DeviceOps.StartOtaForDeviceAsync(selected.DeviceId).ConfigureAwait(false);
    }

    private async void OnBuildStableClicked(object sender, RoutedEventArgs e)
    {
        await BuildAndExportAsync("matrixportal_s3_stable").ConfigureAwait(false);
    }

    private async void OnBuildDmaClicked(object sender, RoutedEventArgs e)
    {
        await BuildAndExportAsync("matrixportal_s3_dma_exp").ConfigureAwait(false);
    }

    private void OnOpenFirmwareFolderClicked(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            return;
        }

        try
        {
            var targetDirectory = DeviceOps.ResolveFirmwareDirectoryToOpen();
            Directory.CreateDirectory(targetDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = targetDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            logsTextBox.Text += $"[{DateTimeOffset.Now:HH:mm:ss}] Falha ao abrir pasta de firmware: {ex.Message}\r\n";
        }
    }

    private void OnCopyHostClicked(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(currentState.ServerBaseAddress);
        Clipboard.SetContent(data);
    }

    private async Task RunSelectedCommandAsync(DeviceCommandType commandType)
    {
        var selected = devicesList.SelectedItem as DeviceListItem;
        if (selected is null || DeviceOps is null)
        {
            return;
        }

        await DeviceOps.RunCommandAsync(selected.DeviceId, commandType).ConfigureAwait(false);
    }

    private async Task BuildAndExportAsync(string profile)
    {
        if (DeviceOps is null)
        {
            return;
        }

        await DeviceOps.BuildAndExportAsync(profile).ConfigureAwait(false);
    }

    private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyButtonState();
    }

    private void ApplyState(DeviceOperationsState state)
    {
        currentState = state;

        ApplyDevices(state.DeviceListSnapshot);
        UpdateLogs(state.Logs);

        commandProgressRing.IsActive = state.CommandInProgress;
        commandProgressRing.Visibility = state.CommandInProgress ? Visibility.Visible : Visibility.Collapsed;
        commandStatusText.Text = state.CommandStatus;

        buildProgressRing.IsActive = state.BuildInProgress;
        buildProgressRing.Visibility = state.BuildInProgress ? Visibility.Visible : Visibility.Collapsed;
        buildStatusText.Text = state.BuildStatus;

        var refreshText = state.LastRefreshUtc == default
            ? "sem atualizacao"
            : state.LastRefreshUtc.ToLocalTime().ToString("HH:mm:ss");
        serverInfoText.Text = $"Servidor: {state.ServerBaseAddress} | mDNS: _micaaudio._tcp | Atualizado: {refreshText}";

        ApplyButtonState();
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        var selectedId = (devicesList.SelectedItem as DeviceListItem)?.DeviceId;

        items.Clear();
        foreach (var device in devices)
        {
            items.Add(new DeviceListItem
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                StatusLine = $"{device.Status} | Perfil {device.Profile} | IP {device.LastKnownIp ?? "-"} | RSSI {device.LastKnownRssi?.ToString() ?? "-"}",
            });
        }

        devicesList.ItemsSource = null;
        devicesList.ItemsSource = items;

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            var match = items.FirstOrDefault(item => string.Equals(item.DeviceId, selectedId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                devicesList.SelectedItem = match;
            }
        }

        ApplyButtonState();
    }

    private void UpdateLogs(IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
        {
            if (lastRenderedLogCount != 0)
            {
                logsTextBox.Text = string.Empty;
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

        logsTextBox.Text = string.Join("\r\n", entries) + "\r\n";
        lastRenderedLogCount = entries.Count;
        lastRenderedLogTail = tail;
    }

    private void ApplyButtonState()
    {
        var selected = devicesList.SelectedItem as DeviceListItem;
        var commandEnabled = selected is not null && !currentState.CommandInProgress;

        enterProvisioningButton.IsEnabled = commandEnabled;
        revokeButton.IsEnabled = commandEnabled;
        testLedButton.IsEnabled = commandEnabled;
        otaButton.IsEnabled = commandEnabled;

        var buildEnabled = !currentState.BuildInProgress;
        buildStableButton.IsEnabled = buildEnabled;
        buildDmaButton.IsEnabled = buildEnabled;
    }

    private sealed class DeviceListItem
    {
        public string DeviceId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string StatusLine { get; init; } = string.Empty;
    }
}
