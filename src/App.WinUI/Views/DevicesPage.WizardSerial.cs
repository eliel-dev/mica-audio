using App.WinUI.Infrastructure.Serial;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

// DOCS: docs/wiki/guides/setup-new-device.md#logs-seriais-no-wizard
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-04---logs-seriais-sob-demanda-no-wizard-usb
// DOCS: docs/handoffs/2026-04-16-ap-first-wifi-mem-and-copy-logs.md
public sealed partial class DevicesPage
{
    private static readonly TimeSpan WizardSerialQuietWindow = TimeSpan.FromSeconds(4);

    private bool wizardDiagnosticsExpanded;
    private bool wizardSerialMonitorSubscribed;
    private bool wizardSerialAutoStopRequested;
    private int wizardSerialCaptureGeneration;
    private int wizardSerialParsedLineCount;
    private IReadOnlyList<SerialMonitorLine> wizardCurrentSerialLines = Array.Empty<SerialMonitorLine>();
    private CancellationTokenSource? wizardSerialQuietWindowCts;
    private SerialPortDescriptor? wizardPreferredPort;
    private string? wizardTargetDeviceId;

    private async Task PrepareWizardSerialMonitorAsync()
    {
        var serialMonitor = SerialMonitorService;
        if (serialMonitor is null)
        {
            WizardStatusText.Text = "Monitor serial indisponivel para o wizard.";
            return;
        }

        EnsureWizardSerialMonitorSubscription();
        CancelWizardSerialQuietWindow();
        wizardDiagnosticsExpanded = false;
        wizardSerialAutoStopRequested = false;
        wizardSerialCaptureGeneration++;
        wizardSerialParsedLineCount = 0;
        wizardCurrentSerialLines = Array.Empty<SerialMonitorLine>();
        wizardPreferredPort = null;
        wizardTargetDeviceId = null;

        await serialMonitor.DisconnectAsync().ConfigureAwait(true);
        serialMonitor.Clear();
        await serialMonitor.RefreshPortsAsync().ConfigureAwait(true);

        SetWizardDiagnosticsExpanded(false);
        ApplyWizardSerialMonitorSnapshot(serialMonitor.GetStateSnapshot());
    }

    private async Task ShutdownWizardSerialMonitorAsync(bool clearLines)
    {
        var serialMonitor = SerialMonitorService;
        CancelWizardSerialQuietWindow();
        wizardSerialAutoStopRequested = false;

        if (serialMonitor is null)
        {
            if (clearLines)
            {
                wizardSerialParsedLineCount = 0;
                wizardCurrentSerialLines = Array.Empty<SerialMonitorLine>();
                wizardPreferredPort = null;
                wizardTargetDeviceId = null;
                RefreshWizardSerialList(wizardCurrentSerialLines);
            }

            return;
        }

        try
        {
            await serialMonitor.DisconnectAsync().ConfigureAwait(true);
            if (clearLines)
            {
                serialMonitor.Clear();
            }
        }
        catch (Exception ex)
        {
            AddLocalLog($"Falha ao encerrar monitor serial do wizard: {ex.Message}");
        }

        if (clearLines)
        {
            wizardSerialParsedLineCount = 0;
            wizardCurrentSerialLines = Array.Empty<SerialMonitorLine>();
            wizardPreferredPort = null;
            wizardTargetDeviceId = null;
        }

        ApplyWizardSerialMonitorSnapshot(serialMonitor.GetStateSnapshot());
    }

    private void EnsureWizardSerialMonitorSubscription()
    {
        if (wizardSerialMonitorSubscribed || SerialMonitorService is null)
        {
            return;
        }

        SerialMonitorService.StateChanged += OnWizardSerialMonitorStateChanged;
        wizardSerialMonitorSubscribed = true;
    }

    private void ReleaseWizardSerialMonitorSubscription()
    {
        if (!wizardSerialMonitorSubscribed || SerialMonitorService is null)
        {
            return;
        }

        SerialMonitorService.StateChanged -= OnWizardSerialMonitorStateChanged;
        wizardSerialMonitorSubscribed = false;
    }

    private void OnWizardSerialMonitorStateChanged(object? sender, EventArgs e)
    {
        var serialMonitor = SerialMonitorService;
        if (serialMonitor is null)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => ApplyWizardSerialMonitorSnapshot(serialMonitor.GetStateSnapshot()));
    }

    private void ApplyWizardSerialMonitorSnapshot(SerialMonitorSessionState state)
    {
        if (WizardSerialStatusText is null
            || WizardSerialListView is null
            || WizardSerialPlaceholderText is null
            || WizardRecaptureBootButton is null
            || WizardCopySerialLogsButton is null
            || WizardClearSerialLogsButton is null)
        {
            return;
        }

        ProcessWizardSerialLines(state.VisibleLines);

        RefreshWizardSerialList(state.VisibleLines);
        var status = state.StatusText;
        if (!string.IsNullOrWhiteSpace(wizardTargetDeviceId))
        {
            status = $"{status}{Environment.NewLine}Device identificado: {wizardTargetDeviceId}";
        }

        WizardSerialStatusText.Text = status;
        WizardSerialPlaceholderText.Text = ResolveWizardSerialPlaceholder(state);
        WizardSerialPlaceholderText.Visibility = state.LineCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        WizardRecaptureBootButton.IsEnabled = !wizardOperationInFlight && wizardPreferredPort is not null;
        WizardCopySerialLogsButton.IsEnabled = !wizardOperationInFlight && state.LineCount > 0;
        WizardClearSerialLogsButton.IsEnabled = !wizardOperationInFlight && state.LineCount > 0;

        if (state.ConnectionState == SerialMonitorConnectionState.Error && !wizardDiagnosticsExpanded)
        {
            SetWizardDiagnosticsExpanded(true);
        }
    }

    private void RefreshWizardSerialList(IReadOnlyList<SerialMonitorLine> nextLines)
    {
        if (WizardSerialListView is null)
        {
            return;
        }

        var shouldRebuild = nextLines.Count < wizardCurrentSerialLines.Count;
        if (!shouldRebuild)
        {
            for (var index = 0; index < wizardCurrentSerialLines.Count; index++)
            {
                if (!wizardCurrentSerialLines[index].Equals(nextLines[index]))
                {
                    shouldRebuild = true;
                    break;
                }
            }
        }

        if (shouldRebuild)
        {
            WizardSerialListView.Items.Clear();
            foreach (var line in nextLines)
            {
                WizardSerialListView.Items.Add(BuildWizardSerialMonitorRow(line));
            }
        }
        else
        {
            for (var index = wizardCurrentSerialLines.Count; index < nextLines.Count; index++)
            {
                WizardSerialListView.Items.Add(BuildWizardSerialMonitorRow(nextLines[index]));
            }
        }

        wizardCurrentSerialLines = nextLines;
        if (nextLines.Count > 0)
        {
            ScrollWizardSerialMonitorToEnd();
        }
    }

    private void ScrollWizardSerialMonitorToEnd()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (WizardSerialListView is null || WizardSerialListView.Items.Count == 0)
            {
                return;
            }

            WizardSerialListView.UpdateLayout();
            WizardSerialListView.ScrollIntoView(WizardSerialListView.Items[WizardSerialListView.Items.Count - 1]);
        });
    }

    private void ProcessWizardSerialLines(IReadOnlyList<SerialMonitorLine> lines)
    {
        if (lines.Count < wizardSerialParsedLineCount)
        {
            wizardSerialParsedLineCount = 0;
        }

        for (var index = wizardSerialParsedLineCount; index < lines.Count; index++)
        {
            InspectWizardSerialLine(lines[index].Text);
        }

        wizardSerialParsedLineCount = lines.Count;
    }

    private void InspectWizardSerialLine(string line)
    {
        if (!MicaSerialProtocol.TryParseDocument(line, out _, out var document))
        {
            return;
        }

        if (document is null)
        {
            return;
        }

        using (document)
        {
            if (!MicaSerialProtocol.TryReadHello(document.RootElement, out var hello))
            {
                return;
            }

            if (!string.Equals(hello.Protocol, MicaSerialProtocol.ProtocolId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(hello.DeviceId))
            {
                return;
            }

            var normalizedDeviceId = hello.DeviceId.Trim();
            if (string.Equals(wizardTargetDeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            wizardTargetDeviceId = normalizedDeviceId;
            wizardSerialAutoStopRequested = false;
            AddLocalLog($"Wizard serial identificou deviceId={normalizedDeviceId} no boot.");
            EvaluateWizardSerialAutoStop();
        }
    }

    private void EvaluateWizardSerialAutoStop()
    {
        var snapshot = string.IsNullOrWhiteSpace(wizardTargetDeviceId)
            ? null
            : FindDeviceSnapshot(wizardTargetDeviceId);
        if (!WizardSerialMonitorPolicy.ShouldAutoStop(
                wizardCompletedSuccessfully,
                wizardSerialAutoStopRequested,
                wizardTargetDeviceId,
                snapshot))
        {
            return;
        }

        if (snapshot is null)
        {
            return;
        }

        wizardSerialAutoStopRequested = true;
        _ = CompleteWizardSerialMonitoringAsync(snapshot);
    }

    private async Task CompleteWizardSerialMonitoringAsync(DeviceSnapshot snapshot)
    {
        var deviceId = string.IsNullOrWhiteSpace(snapshot.DeviceId) ? "-" : snapshot.DeviceId;
        WizardStatusText.Text = $"Provisionamento concluido. Device online: {deviceId}. Os logs seriais foram encerrados automaticamente.";
        WizardSummaryNoteText.Text = "Provisionamento concluido. O device ficou online no app e o monitor serial do wizard foi encerrado.";
        PairingCodeText.Severity = InfoBarSeverity.Success;
        PairingCodeText.Message = $"Onboarding concluido. Device online: {deviceId}.";
        AddLocalLog($"Wizard serial encerrou automaticamente apos {deviceId} ficar online.");
        await ShutdownWizardSerialMonitorAsync(clearLines: false).ConfigureAwait(true);
    }

    private async Task BeginWizardSerialMonitoringAsync(SerialPortDescriptor preferredPort)
    {
        var serialMonitor = SerialMonitorService;
        if (serialMonitor is null)
        {
            WizardStatusText.Text = "Monitor serial indisponivel para capturar o boot.";
            SetWizardDiagnosticsExpanded(true);
            return;
        }

        wizardPreferredPort = preferredPort;
        wizardTargetDeviceId = null;
        wizardSerialAutoStopRequested = false;
        wizardSerialCaptureGeneration++;
        wizardSerialParsedLineCount = 0;
        wizardCurrentSerialLines = Array.Empty<SerialMonitorLine>();
        CancelWizardSerialQuietWindow();

        try
        {
            await serialMonitor.DisconnectAsync().ConfigureAwait(true);
            serialMonitor.Clear();
            await serialMonitor.RefreshPortsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            WizardStatusText.Text = $"Falha ao preparar o monitor serial: {ex.Message}";
            SetWizardDiagnosticsExpanded(true);
            AddLocalLog($"Falha ao preparar o monitor serial do wizard: {ex.Message}");
            return;
        }

        var reconnectPort = ResolveWizardReconnectPort(preferredPort, serialMonitor.GetStateSnapshot().AvailablePorts);
        if (reconnectPort is null)
        {
            WizardStatusText.Text = "Flash concluido, mas a porta serial nao reapareceu. Abra 'Ver mais' e tente 'Recapturar boot'.";
            SetWizardDiagnosticsExpanded(true);
            ApplyWizardSerialMonitorSnapshot(serialMonitor.GetStateSnapshot());
            AddLocalLog("Wizard serial nao encontrou uma porta COM equivalente apos o flash.");
            return;
        }

        serialMonitor.SetSelectedPort(reconnectPort.PortName);
        await serialMonitor.ConnectAsync(reconnectPort.PortName).ConfigureAwait(true);

        var snapshot = serialMonitor.GetStateSnapshot();
        ApplyWizardSerialMonitorSnapshot(snapshot);

        if (snapshot.ConnectionState != SerialMonitorConnectionState.Connected)
        {
            WizardStatusText.Text = $"Flash concluido, mas o monitor serial nao reabriu a porta. {snapshot.StatusText}";
            SetWizardDiagnosticsExpanded(true);
            AddLocalLog($"Wizard serial falhou ao reabrir {reconnectPort.PortName}: {snapshot.StatusText}");
            return;
        }

        WizardStatusText.Text = $"Flash concluido. O monitor serial assumiu {reconnectPort.PortName} a 115200 para acompanhar o boot. Use 'Ver mais' se o AP nao aparecer.";
        ArmWizardSerialQuietWindow(snapshot.LineCount);
    }

    private void ArmWizardSerialQuietWindow(int baselineLineCount)
    {
        CancelWizardSerialQuietWindow();

        var serialMonitor = SerialMonitorService;
        if (serialMonitor is null)
        {
            return;
        }

        var generation = wizardSerialCaptureGeneration;
        var cts = new CancellationTokenSource();
        wizardSerialQuietWindowCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(WizardSerialQuietWindow, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (cts.IsCancellationRequested || generation != wizardSerialCaptureGeneration)
                {
                    return;
                }

                var snapshot = serialMonitor.GetStateSnapshot();
                if (snapshot.ConnectionState != SerialMonitorConnectionState.Connected
                    || snapshot.LineCount > baselineLineCount)
                {
                    return;
                }

                SetWizardDiagnosticsExpanded(true);
                WizardStatusText.Text = "Monitor serial conectado, mas sem logs de boot apos o flash. Use 'Recapturar boot' para repetir o reset com o monitor ja anexado.";
                AddLocalLog("Wizard serial ficou sem linhas de boot dentro da janela diagnostica.");
            });
        }, CancellationToken.None);
    }

    private void CancelWizardSerialQuietWindow()
    {
        wizardSerialQuietWindowCts?.Cancel();
        wizardSerialQuietWindowCts?.Dispose();
        wizardSerialQuietWindowCts = null;
    }

    private async Task RecaptureWizardBootAsync()
    {
        var serialMonitor = SerialMonitorService;
        if (serialMonitor is null)
        {
            WizardStatusText.Text = "Monitor serial indisponivel para recapturar o boot.";
            return;
        }

        var preferredPort = wizardPreferredPort ?? (WizardPortComboBox.SelectedItem as SerialPortDescriptor);
        if (preferredPort is null)
        {
            WizardStatusText.Text = "Selecione uma porta COM valida antes de recapturar o boot.";
            return;
        }

        SetWizardDiagnosticsExpanded(true);

        if (serialMonitor.GetStateSnapshot().ConnectionState != SerialMonitorConnectionState.Connected)
        {
            await BeginWizardSerialMonitoringAsync(preferredPort).ConfigureAwait(true);
        }

        var snapshot = serialMonitor.GetStateSnapshot();
        if (snapshot.ConnectionState != SerialMonitorConnectionState.Connected)
        {
            return;
        }

        serialMonitor.Clear();
        wizardSerialParsedLineCount = 0;
        wizardCurrentSerialLines = Array.Empty<SerialMonitorLine>();
        wizardTargetDeviceId = null;
        ApplyWizardSerialMonitorSnapshot(serialMonitor.GetStateSnapshot());
        WizardStatusText.Text = $"Monitor serial pronto em {snapshot.SelectedPortName}. Reproduzindo o boot a 115200...";

        try
        {
            await serialMonitor.ResetAttachedDeviceAsync().ConfigureAwait(true);
            ArmWizardSerialQuietWindow(baselineLineCount: 0);
            AddLocalLog($"Wizard serial recapturou o boot em {snapshot.SelectedPortName}.");
        }
        catch (Exception ex)
        {
            SetWizardDiagnosticsExpanded(true);
            WizardStatusText.Text = $"Falha ao recapturar o boot: {ex.Message}";
            AddLocalLog($"Falha ao recapturar boot no wizard: {ex.Message}");
        }
    }

    private void CopyWizardSerialLogsToClipboard()
    {
        var serialMonitor = SerialMonitorService;
        if (serialMonitor is null)
        {
            WizardStatusText.Text = "Monitor serial indisponivel para copiar os logs.";
            return;
        }

        var text = serialMonitor.ExportAllText();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
        data.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
        AddLocalLog("Wizard serial copiou a sessao completa para a area de transferencia.");
    }

    private void SetWizardDiagnosticsExpanded(bool expanded)
    {
        wizardDiagnosticsExpanded = expanded;

        if (WizardDiagnosticsPanel is not null)
        {
            WizardDiagnosticsPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        }

        if (WizardDetailsToggleButton is not null)
        {
            WizardDetailsToggleButton.Content = BuildButtonWithGlyph(expanded ? "\uE70D" : "\uE712", expanded ? "Ocultar logs" : "Ver mais");
        }
    }

    private static SerialPortDescriptor? ResolveWizardReconnectPort(
        SerialPortDescriptor preferredPort,
        IReadOnlyList<SerialPortDescriptor> availablePorts)
    {
        foreach (var candidate in availablePorts)
        {
            if (string.Equals(candidate.PortName, preferredPort.PortName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredPort.PnpDeviceId))
        {
            foreach (var candidate in availablePorts)
            {
                if (string.Equals(candidate.PnpDeviceId, preferredPort.PnpDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredPort.VidPid))
        {
            var vidPidMatches = availablePorts
                .Where(candidate => string.Equals(candidate.VidPid, preferredPort.VidPid, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (vidPidMatches.Length == 1)
            {
                return vidPidMatches[0];
            }
        }

        var fallbackPortName = global::App.WinUI.Infrastructure.Serial.SerialMonitorService.ResolveSelectedPortName(preferredPort.PortName, availablePorts);
        if (string.IsNullOrWhiteSpace(fallbackPortName))
        {
            return null;
        }

        return availablePorts.FirstOrDefault(item =>
            string.Equals(item.PortName, fallbackPortName, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveWizardSerialPlaceholder(SerialMonitorSessionState state)
    {
        if (state.ConnectionState == SerialMonitorConnectionState.Connected)
        {
            return "Conectado. Aguardando logs seriais de boot a 115200...";
        }

        if (state.ConnectionState == SerialMonitorConnectionState.Connecting)
        {
            return "Abrindo a porta serial para acompanhar o boot...";
        }

        if (state.ConnectionState == SerialMonitorConnectionState.Error)
        {
            return state.ErrorText ?? "Falha ao abrir ou ler a porta serial do wizard.";
        }

        return "O wizard vai assumir a porta serial apos o flash para monitorar o boot do ESP32-S3. Use 'Copiar logs' para exportar a sessao inteira.";
    }

    private static Border BuildWizardSerialMonitorRow(SerialMonitorLine line)
    {
        return new Border
        {
            Padding = new Thickness(12, 6, 12, 6),
            BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = line.Text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 231, 231, 231)),
            },
        };
    }
}
