using App.WinUI.Services.Firmware;
using Device.Protocol.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md
// DOCS: docs/wiki/reference/device-observability-dashboard.md
// DOCS: docs/handoffs/2026-04-14-ota-firmware-update-flow-e-hub75-status.md
public sealed partial class DevicesPage
{
    private static readonly TimeSpan FirmwareVersionObservationTimeout = TimeSpan.FromSeconds(15);

    private async Task ExecuteUpdateFirmwareAsync(string? requestedDeviceId = null)
    {
        var selected = ResolveCommandDevice(requestedDeviceId);
        var snapshot = selected is null ? null : FindDeviceSnapshot(selected.DeviceId);
        var ops = DeviceOps;
        if (selected is null || snapshot is null || ops is null)
        {
            return;
        }

        var (artifact, error) = await TryResolveLatestFirmwareArtifactAsync(snapshot).ConfigureAwait(true);
        if (artifact is null)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Warning, "Release oficial indisponivel para este dispositivo.");
            AddLocalLog(error);
            return;
        }

        if (!IsFirmwareUpdateAvailable(snapshot, artifact.Manifest.FirmwareVersion))
        {
            ShowInlineStatusMessage(InfoBarSeverity.Informational, $"Dispositivo ja esta no ultimo release {artifact.Manifest.FirmwareVersion}.");
            AddLocalLog($"Atualizacao ignorada para {snapshot.DeviceId}: device ja esta no ultimo release {artifact.Manifest.FirmwareVersion}.");
            return;
        }

        var action = await ShowFirmwareUpdateDialogAsync(snapshot, artifact).ConfigureAwait(true);
        switch (action)
        {
            case FirmwareUpdateAction.StartOta:
                await StartFirmwareOtaAsync(snapshot, artifact).ConfigureAwait(true);
                break;

            case FirmwareUpdateAction.StartUsbReflash:
                await ShowUsbFirmwareRefreshWizardAsync(snapshot, artifact.Manifest.FirmwareVersion).ConfigureAwait(true);
                break;
        }
    }

    private async Task<FirmwareUpdateAction> ShowFirmwareUpdateDialogAsync(DeviceSnapshot snapshot, ResolvedFirmwareArtifact artifact)
    {
        var currentVersion = string.IsNullOrWhiteSpace(snapshot.FirmwareVersion)
            ? "Firmware atual nao identificado"
            : snapshot.FirmwareVersion;
        var latestVersion = artifact.Manifest.FirmwareVersion;
        var online = snapshot.Status == DeviceStatus.Online;

        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = $"Firmware atual: {currentVersion}",
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = $"Ultimo release: {latestVersion}",
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = online
                        ? "O dispositivo esta online. Voce pode iniciar a atualizacao OTA agora ou optar pelo reflash oficial por USB."
                        : "O dispositivo esta offline. O caminho disponivel agora e o reflash oficial por USB, que apaga a flash e exige novo provisionamento.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.82,
                },
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Atualizar firmware",
            XamlRoot = XamlRoot,
            Content = content,
            CloseButtonText = "Cancelar",
        };

        if (online)
        {
            dialog.PrimaryButtonText = "Atualizar agora";
            dialog.SecondaryButtonText = "Reflash por USB";
            dialog.DefaultButton = ContentDialogButton.Primary;
        }
        else
        {
            dialog.PrimaryButtonText = "Reflash por USB";
            dialog.DefaultButton = ContentDialogButton.Primary;
        }

        var result = await dialog.ShowAsync();
        if (online)
        {
            return result switch
            {
                ContentDialogResult.Primary => FirmwareUpdateAction.StartOta,
                ContentDialogResult.Secondary => FirmwareUpdateAction.StartUsbReflash,
                _ => FirmwareUpdateAction.Cancelled,
            };
        }

        return result == ContentDialogResult.Primary
            ? FirmwareUpdateAction.StartUsbReflash
            : FirmwareUpdateAction.Cancelled;
    }

    private async Task StartFirmwareOtaAsync(DeviceSnapshot snapshot, ResolvedFirmwareArtifact artifact)
    {
        var ops = DeviceOps;
        if (ops is null)
        {
            return;
        }

        var targetVersion = artifact.Manifest.FirmwareVersion;
        var currentVersion = string.IsNullOrWhiteSpace(snapshot.FirmwareVersion)
            ? "desconhecida"
            : snapshot.FirmwareVersion;

        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            IsIndeterminate = false,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };
        var stageText = new TextBlock
        {
            Text = "Iniciando OTA...",
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        };
        var percentText = new TextBlock
        {
            Text = "0%",
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        };

        var dialogContent = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = $"Firmware atual: {currentVersion}", TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = $"Release: {targetVersion}", TextWrapping = TextWrapping.Wrap },
                progressBar,
                percentText,
                stageText,
            },
        };

        var dialog = new ContentDialog
        {
            Title = "Atualizando firmware",
            XamlRoot = XamlRoot,
            Content = dialogContent,
            CloseButtonText = "Fechar",
        };

        void OnStateChanged(object? sender, EventArgs e)
        {
            var state = ops.GetStateSnapshot();
            var deviceId = snapshot.DeviceId;
            if (state.CommandByDevice.TryGetValue(deviceId, out var cmd))
            {
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    progressBar.Value = Math.Clamp(cmd.Percent, 0, 100);
                    percentText.Text = $"{Math.Clamp(cmd.Percent, 0, 100)}%";
                    stageText.Text = string.IsNullOrWhiteSpace(cmd.Status)
                        ? "Processando..."
                        : cmd.Status;
                });
            }
        }

        ops.StateChanged += OnStateChanged;
        var showTask = dialog.ShowAsync();

        AddLocalLog($"Iniciando OTA para {snapshot.DeviceId}: atual={currentVersion} releaseOficial={targetVersion}; aguardando safe update mode.");

        var result = await ops.UpdateFirmwareAsync(snapshot.DeviceId, targetVersion).ConfigureAwait(true);
        ops.StateChanged -= OnStateChanged;

        progressBar.Value = 100;
        percentText.Text = "100%";

        if (result.Accepted && result.Completed && result.Success)
        {
            stageText.Text = "Firmware validado com sucesso!";
            ShowInlineStatusMessage(InfoBarSeverity.Success, $"Firmware atualizado e validado em {targetVersion}.");
            AddLocalLog($"Safe update mode concluiu com sucesso em {snapshot.DeviceId}: {targetVersion}.");
        }
        else
        {
            var reason = string.IsNullOrWhiteSpace(result.Message) ? result.ErrorCode : result.Message;
            var isRollback = string.Equals(result.Stage, "rolled-back", StringComparison.OrdinalIgnoreCase);
            stageText.Text = isRollback
                ? "OTA revertida pelo safe update mode."
                : $"Falha: {reason ?? "erro desconhecido"}";
            ShowInlineStatusMessage(
                isRollback ? InfoBarSeverity.Warning : InfoBarSeverity.Error,
                isRollback
                ? $"OTA revertida em {snapshot.DeviceId} pelo safe update mode."
                : $"OTA falhou para {snapshot.DeviceId}.");
            AddLocalLog($"Falha ao concluir OTA em {snapshot.DeviceId}: {reason ?? "erro desconhecido"}");
        }

        try { showTask.Cancel(); } catch { /* dialog may already be closed */ }

        var confirmed = await WaitForFirmwareVersionAsync(snapshot.DeviceId, targetVersion, FirmwareVersionObservationTimeout).ConfigureAwait(true);
        if (!confirmed && result.Success)
        {
            AddLocalLog($"OTA validada para {snapshot.DeviceId}, mas o snapshot ainda nao refletiu {targetVersion} dentro da janela diagnostica.");
        }
    }

    private async Task<bool> WaitForFirmwareVersionAsync(string deviceId, string expectedVersion, TimeSpan timeout)
    {
        var ops = DeviceOps;
        if (ops is null || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(expectedVersion))
        {
            return false;
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = ops.GetStateSnapshot().DeviceListSnapshot.FirstOrDefault(item =>
                string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (snapshot is not null
                && string.Equals(snapshot.FirmwareVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            ops.RequestRefresh();
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        }

        return false;
    }

    private async Task<(ResolvedFirmwareArtifact? Artifact, string Error)> TryResolveLatestFirmwareArtifactAsync(DeviceSnapshot snapshot)
    {
        var service = FirmwareService;
        if (service is null)
        {
            return (null, "Servico de firmware indisponivel.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.BoardModel)
            || string.IsNullOrWhiteSpace(snapshot.PanelType)
            || string.IsNullOrWhiteSpace(snapshot.Profile))
        {
            return (null, "Metadados do dispositivo insuficientes para resolver o release oficial.");
        }

        var refresh = await service
            .EnsureOfficialFirmwareFreshAsync(snapshot.BoardModel, snapshot.PanelType, snapshot.Profile)
            .ConfigureAwait(true);
        if (!refresh.IsFresh || refresh.ResolvedArtifact is null)
        {
            return (null, refresh.FailureReason);
        }

        return (refresh.ResolvedArtifact, string.Empty);
    }

    private static bool IsFirmwareUpdateAvailable(DeviceSnapshot snapshot, string latestFirmwareVersion)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return !string.IsNullOrWhiteSpace(latestFirmwareVersion)
            && !string.Equals(snapshot.FirmwareVersion, latestFirmwareVersion, StringComparison.OrdinalIgnoreCase);
    }

    private enum FirmwareUpdateAction
    {
        Cancelled = 0,
        StartOta = 1,
        StartUsbReflash = 2,
    }
}
