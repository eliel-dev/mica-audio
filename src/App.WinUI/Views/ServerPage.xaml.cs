using App.WinUI.Services.Devices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
public sealed partial class ServerPage : Page
{
    private DeviceOperationsState currentState = new();
    private int lastRenderedLogCount;
    private string lastRenderedLogTail = string.Empty;

    public ServerPage()
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
    }

    private void OnDeviceOpsStateChanged(object? sender, EventArgs e)
    {
        var ops = DeviceOps;
        if (ops is null)
        {
            return;
        }

        var state = ops.GetStateSnapshot();
        _ = DispatcherQueue.TryEnqueue(() => ApplyState(state));
    }

    // DOCS: docs/wiki/reference/troubleshooting-matrix.md
    private void ApplyState(DeviceOperationsState state)
    {
        currentState = state;
        var refreshText = state.LastRefreshUtc == default
            ? "sem atualizacao"
            : state.LastRefreshUtc.ToLocalTime().ToString("HH:mm:ss");

        ServerInfoText.Text = $"Servidor: {state.ServerBaseAddress} | mDNS: _micaaudio._tcp | Atualizado: {refreshText}";

        BuildProgressRing.IsActive = state.BuildInProgress;
        BuildProgressRing.Visibility = state.BuildInProgress ? Visibility.Visible : Visibility.Collapsed;
        BuildStatusText.Text = state.BuildStatus;
        BuildPercentText.Text = $"{Math.Clamp(state.BuildPercent, 0, 100)}%";

        UpdateLogs(state.Logs);
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

    private async void OnBuildStableClicked(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            return;
        }

        await DeviceOps.BuildAndExportAsync("matrixportal_s3_stable").ConfigureAwait(false);
    }

    private async void OnBuildDmaClicked(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            return;
        }

        await DeviceOps.BuildAndExportAsync("matrixportal_s3_dma_exp").ConfigureAwait(false);
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            LogsTextBox.Text += $"[{DateTimeOffset.Now:HH:mm:ss}] Falha ao abrir pasta de firmware: {ex.Message}\r\n";
        }
    }

    private void OnCopyHostClicked(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(currentState.ServerBaseAddress);
        Clipboard.SetContent(data);
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        DeviceOps?.RequestRefresh();
    }
}
