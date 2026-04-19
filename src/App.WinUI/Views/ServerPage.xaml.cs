using App.WinUI.Services.Devices;
using App.WinUI.Services.Firmware;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
// DOCS: docs/wiki/guides/build-export-firmware.md#guia---download-de-firmware-pre-compilado
public sealed partial class ServerPage : Page
{
    private readonly List<string> localLogs = new();
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly PrecompiledFirmwareService firmwareService;
    private DeviceOperationsState currentState = new();
    private int lastRenderedLogCount;
    private string lastRenderedLogTail = string.Empty;

    internal ServerPage(DeviceOperationsCoordinator deviceOps, PrecompiledFirmwareService firmwareService)
    {
        this.deviceOps = deviceOps;
        this.firmwareService = firmwareService;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => deviceOps;

    private PrecompiledFirmwareService? FirmwareService => firmwareService;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is null)
        {
            LogsTextBox.Text = "Servico de dispositivos indisponivel.";
            return;
        }

        DeviceOps.StateChanged += OnDeviceOpsStateChanged;
        DeviceOps.RequestRefresh();

        if (FirmwareService is not null)
        {
            FirmwareService.LogMessage += OnFirmwareServiceLogMessage;
        }

        ApplyState(DeviceOps.GetStateSnapshot());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
        }

        if (FirmwareService is not null)
        {
            FirmwareService.LogMessage -= OnFirmwareServiceLogMessage;
        }
    }

    private void OnFirmwareServiceLogMessage(object? sender, string message)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            AddLocalLog(message);
            UpdateLogs(currentState.Logs);
        });
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
            : state.LastRefreshUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);

        ServerInfoText.Text = $"Servidor: {state.ServerBaseAddress} | mDNS: _micaaudio._tcp | Atualizado: {refreshText}";
        UpdateLogs(state.Logs);
    }

    private void UpdateLogs(IReadOnlyList<DeviceLogEntry> entries)
    {
        var totalCount = entries.Count + localLogs.Count;
        if (totalCount == 0)
        {
            if (lastRenderedLogCount != 0)
            {
                LogsTextBox.Text = string.Empty;
                lastRenderedLogCount = 0;
                lastRenderedLogTail = string.Empty;
            }

            return;
        }

        var tail = localLogs.Count > 0 ? localLogs[^1] : DeviceLogEntryFormatter.Format(entries[^1], includeDeviceId: true);
        if (lastRenderedLogCount == totalCount && string.Equals(lastRenderedLogTail, tail, StringComparison.Ordinal))
        {
            return;
        }

        var merged = new List<string>(entries.Count + localLogs.Count);
        merged.AddRange(entries.Select(entry => DeviceLogEntryFormatter.Format(entry, includeDeviceId: true)));
        merged.AddRange(localLogs);

        LogsTextBox.Text = string.Join("\r\n", merged) + "\r\n";
        lastRenderedLogCount = totalCount;
        lastRenderedLogTail = tail;
    }

    private async void OnDownloadDmaClicked(object sender, RoutedEventArgs e)
    {
        await SaveFirmwareAsync("dma_exp").ConfigureAwait(false);
    }

    private async Task SaveFirmwareAsync(string optionId)
    {
        var service = FirmwareService;
        if (service is null)
        {
            SetDownloadUiState(false, "Download: servico indisponivel", 0);
            AddLocalLog("Servico de firmware indisponivel.");
            UpdateLogs(currentState.Logs);
            return;
        }

        var option = service.GetOptions().FirstOrDefault(item => string.Equals(item.Id, optionId, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            SetDownloadUiState(false, "Download: opcao invalida", 0);
            AddLocalLog($"Opcao de firmware invalida: {optionId}");
            UpdateLogs(currentState.Logs);
            return;
        }

        SetDownloadUiState(true, $"Download: validando release oficial de {option.DisplayName}", 10);

        var export = await service.PrepareOfficialFirmwareExportAsync(optionId).ConfigureAwait(true);
        if (!export.Success || export.ResolvedArtifact is null)
        {
            SetDownloadUiState(false, "Download: release oficial indisponivel", 0);
            AddLocalLog(export.FailureReason);
            UpdateLogs(currentState.Logs);
            return;
        }

        SetDownloadUiState(true, $"Download: preparando {option.DisplayName}", 30);

        StorageFile? targetFile;
        try
        {
            targetFile = await PickDestinationFileAsync(export.SuggestedFileName).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetDownloadUiState(false, "Download: erro ao abrir seletor", 0);
            AddLocalLog($"Falha ao abrir seletor de arquivo: {ex.Message}");
            UpdateLogs(currentState.Logs);
            return;
        }

        if (targetFile is null)
        {
            SetDownloadUiState(false, "Download: cancelado", 0);
            AddLocalLog("Salvamento cancelado pelo usuario.");
            UpdateLogs(currentState.Logs);
            return;
        }

        try
        {
            SetDownloadUiState(true, "Download: copiando firmware...", 70);
            await service.CopyArtifactToAsync(export.ResolvedArtifact, targetFile.Path).ConfigureAwait(true);
            SetDownloadUiState(false, "Download: concluido", 100);
            AddLocalLog($"Firmware salvo em: {targetFile.Path}");
            UpdateLogs(currentState.Logs);
        }
        catch (Exception ex)
        {
            SetDownloadUiState(false, "Download: erro", 0);
            AddLocalLog($"Falha ao salvar firmware: {ex.Message}");
            UpdateLogs(currentState.Logs);
        }
    }

    private static async Task<StorageFile?> PickDestinationFileAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
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

    private void SetDownloadUiState(bool busy, string status, int percent)
    {
        DownloadProgressRing.IsActive = busy;
        DownloadProgressRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        DownloadStatusText.Text = status;
        DownloadPercentText.Text = $"{Math.Clamp(percent, 0, 100)}%";
    }

    private void AddLocalLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        localLogs.Add($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
        while (localLogs.Count > 200)
        {
            localLogs.RemoveAt(0);
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



