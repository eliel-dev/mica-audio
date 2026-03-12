using App.WinUI.Services.Devices;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class DevicesPage
{
    private void UpdateDeviceLogs(string? deviceId, IReadOnlyList<string> entries, string placeholder)
    {
        if (DeviceLogsTextBox is null)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(deviceId)
            ? "Logs do dispositivo"
            : $"Logs do dispositivo Â· {deviceId}";
        var count = entries.Count;
        var normalizedPlaceholder = count == 0 ? placeholder : string.Empty;
        var tail = count > 0 ? entries[^1] : string.Empty;
        var deviceUnchanged = string.Equals(lastRenderedDeviceLogsDeviceId, deviceId, StringComparison.OrdinalIgnoreCase);

        if (deviceUnchanged
            && lastRenderedDeviceLogCount == count
            && string.Equals(lastRenderedDeviceLogTail, tail, StringComparison.Ordinal)
            && string.Equals(lastRenderedDeviceLogsHeader, header, StringComparison.Ordinal)
            && string.Equals(lastRenderedDeviceLogsPlaceholder, normalizedPlaceholder, StringComparison.Ordinal))
        {
            return;
        }

        DeviceLogsTextBox.Header = header;
        DeviceLogsTextBox.Text = count == 0
            ? normalizedPlaceholder
            : string.Join("\r\n", entries) + "\r\n";
        ScrollDeviceLogsToOffset(scrollToEnd: count > 0);

        lastRenderedDeviceLogsDeviceId = deviceId;
        lastRenderedDeviceLogCount = count;
        lastRenderedDeviceLogTail = tail;
        lastRenderedDeviceLogsHeader = header;
        lastRenderedDeviceLogsPlaceholder = normalizedPlaceholder;
    }

    // DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#consumo-na-devicespage-entrega-3
    private void ApplyDashboard(string? selectionDeviceId, bool hasSelection, DeviceSnapshot? snapshot, DeviceMetricsPresentation metrics)
    {
        try
        {
            if (ShouldUseOfflineDashboardFallback(hasSelection, snapshot))
            {
                var offlineSignature = BuildOfflineDashboardSignature(selectionDeviceId, snapshot);
                if (!string.Equals(lastRenderedDashboardSignature, offlineSignature, StringComparison.Ordinal))
                {
                    LogSelectionBreadcrumb("dashboard-offline-fallback", selectionDeviceId, snapshot, metrics);
                    ApplyOfflineDashboardFallback(hasSelection, snapshot, metrics, OfflineDashboardFallbackText);
                    lastRenderedDashboardSignature = offlineSignature;
                }

                return;
            }

            if (ShouldUsePendingTelemetryFallback(hasSelection, snapshot, metrics))
            {
                var pendingSignature = BuildPendingTelemetryDashboardSignature(selectionDeviceId, snapshot, metrics);
                if (!string.Equals(lastRenderedDashboardSignature, pendingSignature, StringComparison.Ordinal))
                {
                    LogSelectionBreadcrumb("dashboard-awaiting-telemetry", selectionDeviceId, snapshot, metrics);
                    ApplySafeDashboardFallback(hasSelection, snapshot, metrics, PendingTelemetryFallbackText);
                    lastRenderedDashboardSignature = pendingSignature;
                }

                return;
            }

            var placeholder = ResolveDashboardPlaceholder(hasSelection, metrics);
            var brightnessValueLabel = BuildBrightnessValueLabel(snapshot);
            var brightnessStatusLabel = BuildBrightnessStatusLabel(snapshot);
            var heartbeatLabel = BuildHeartbeatLabel(snapshot);

            var signature = BuildDashboardSignature(
                selectionDeviceId,
                hasSelection,
                snapshot,
                metrics,
                placeholder,
                brightnessValueLabel,
                brightnessStatusLabel,
                heartbeatLabel);

            if (string.Equals(lastRenderedDashboardSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            LogSelectionBreadcrumb("dashboard-online-safe", selectionDeviceId, snapshot, metrics);

            DashboardBrightnessValueText.Text = brightnessValueLabel;
            DashboardBrightnessStatusText.Text = brightnessStatusLabel;
            DashboardTelemetryHeartbeatText.Text = heartbeatLabel;

            var sliderValue = snapshot?.BrightnessCap is int brightnessCap
                ? Math.Clamp(brightnessCap, SafeBrightnessMin, SafeBrightnessMax)
                : SafeBrightnessMax;
            suppressBrightnessSliderEvents = true;
            DashboardBrightnessSlider.Value = sliderValue;
            suppressBrightnessSliderEvents = false;
            brightnessCommitPending = false;

            var loopPercent = metrics.LoopLoadPercent is int rawLoop ? Math.Clamp(rawLoop, 0, 100) : (int?)null;
            DashboardLoopLoadText.Text = loopPercent.HasValue
                ? $"{loopPercent.Value} %"
                : "-";
            DashboardLoopLoadBar.Value = SafeProgress(metrics.LoopLoadProgress);

            var heapPercent = TryComputePercent(snapshot?.FreeHeapBytes, HeapTotalBytesBaseline, out var resolvedHeapPercent)
                ? resolvedHeapPercent
                : (int?)null;
            DashboardHeapText.Text = heapPercent.HasValue ? $"{heapPercent.Value} %" : "-";
            DashboardHeapFragmentationText.Text = BuildHeapSubLabel(snapshot);
            DashboardHeapFragmentationBar.Value = SafeProgress(metrics.HeapFragmentationProgress);
            DashboardHeapFragmentationBar.Visibility = metrics.HeapFragmentationProgress.HasValue ? Visibility.Visible : Visibility.Collapsed;

            var psramPercent = snapshot?.PsramAvailable == true && TryComputePercent(snapshot.FreePsramBytes, PsramTotalBytesBaseline, out var resolvedPsramPercent)
                ? resolvedPsramPercent
                : (int?)null;
            DashboardPsramText.Text = psramPercent.HasValue ? $"{psramPercent.Value} %" : "-";
            DashboardPsramFragmentationText.Text = BuildPsramSubLabel(snapshot);
            DashboardPsramFragmentationBar.Value = SafeProgress(metrics.PsramFragmentationProgress);
            DashboardPsramFragmentationBar.Visibility = metrics.PsramFragmentationProgress.HasValue ? Visibility.Visible : Visibility.Collapsed;

            DashboardNetworkText.Text = metrics.NetworkLabel;
            DashboardUptimeText.Text = metrics.UptimeLabel;

            DashboardMetricsGrid.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(placeholder))
            {
                DashboardPlaceholderText.Visibility = Visibility.Collapsed;
            }
            else
            {
                DashboardPlaceholderText.Text = placeholder;
                DashboardPlaceholderText.Visibility = Visibility.Visible;
            }

            ApplyTileTone(DashboardLoopTile);
            ApplyTileTone(DashboardHeapTile);
            ApplyTileTone(DashboardPsramTile);

            ApplyDashboardStatusSection(hasSelection, snapshot, metrics);
            lastRenderedDashboardSignature = signature;
        }
        catch (Exception ex)
        {
            LogRenderException("dashboard", selectionDeviceId, snapshot, ex);
            ApplyOfflineDashboardFallback(hasSelection, snapshot, DeviceMetricsFormatter.Build(snapshot), OfflineDashboardFallbackText);
            lastRenderedDashboardSignature = BuildOfflineDashboardSignature(selectionDeviceId, snapshot);
        }
    }

    private static string BuildDashboardSignature(
        string? selectionDeviceId,
        bool hasSelection,
        DeviceSnapshot? snapshot,
        DeviceMetricsPresentation metrics,
        string placeholder,
        string brightnessValueLabel,
        string brightnessStatusLabel,
        string heartbeatLabel)
    {
        var loopProgressScaled = (int)Math.Round(Math.Clamp(metrics.LoopLoadProgress, 0d, 1d) * 1000d);
        var heapFragmentationScaled = metrics.HeapFragmentationProgress.HasValue
            ? (int)Math.Round(Math.Clamp(metrics.HeapFragmentationProgress.Value, 0d, 1d) * 1000d)
            : -1;
        var psramFragmentationScaled = metrics.PsramFragmentationProgress.HasValue
            ? (int)Math.Round(Math.Clamp(metrics.PsramFragmentationProgress.Value, 0d, 1d) * 1000d)
            : -1;
        var portalStateLabel = BuildProvisioningPortalLabel(snapshot);
        var auxLedLabel = BuildAuxLedAvailabilityLabel(snapshot);
        var streamSuccessRateLabel = BuildStreamSuccessRateLabel(snapshot);

        return string.Concat(
            hasSelection ? "1" : "0", "|",
            selectionDeviceId ?? "-", "|",
            snapshot?.Status.ToString() ?? "-", "|",
            metrics.StatusLabel, "|",
            metrics.UptimeLabel, "|",
            metrics.HeapLabel, "|",
            metrics.PsramLabel, "|",
            metrics.NetworkLabel, "|",
            metrics.LoopLoadPercent?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            loopProgressScaled.ToString(CultureInfo.InvariantCulture), "|",
            heapFragmentationScaled.ToString(CultureInfo.InvariantCulture), "|",
            psramFragmentationScaled.ToString(CultureInfo.InvariantCulture), "|",
            placeholder, "|",
            brightnessValueLabel, "|",
            brightnessStatusLabel, "|",
            heartbeatLabel, "|",
            snapshot?.LastWifiEvent ?? "-", "|",
            snapshot?.LastKnownRssi?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.ProvisioningPortalActive?.ToString() ?? "-", "|",
            portalStateLabel, "|",
            auxLedLabel, "|",
            streamSuccessRateLabel, "|",
            snapshot?.StreamFramesReceived?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamFramesApplied?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamSequenceGapCount?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamInvalidFrameCount?.ToString(CultureInfo.InvariantCulture) ?? "-", "|",
            snapshot?.StreamLastSequence?.ToString(CultureInfo.InvariantCulture) ?? "-");
    }

    private static bool ShouldUseOfflineDashboardFallback(bool hasSelection, DeviceSnapshot? snapshot)
    {
        return hasSelection && snapshot?.Status != DeviceStatus.Online;
    }

    private static bool ShouldUsePendingTelemetryFallback(bool hasSelection, DeviceSnapshot? snapshot, DeviceMetricsPresentation metrics)
    {
        return hasSelection
            && snapshot?.Status == DeviceStatus.Online
            && (!snapshot.LastTelemetryUtc.HasValue || !metrics.HasMetrics);
    }

    private static string BuildOfflineDashboardSignature(string? selectionDeviceId, DeviceSnapshot? snapshot)
    {
        return string.Concat(
            "offline|",
            selectionDeviceId ?? "-",
            "|",
            snapshot?.Status.ToString() ?? "-",
            "|",
            snapshot?.LastTelemetryUtc?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            snapshot?.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            snapshot?.LastWifiEvent ?? "-");
    }

    private static string BuildPendingTelemetryDashboardSignature(
        string? selectionDeviceId,
        DeviceSnapshot? snapshot,
        DeviceMetricsPresentation metrics)
    {
        return string.Concat(
            "pending-telemetry|",
            selectionDeviceId ?? "-",
            "|",
            snapshot?.LastTelemetryUtc?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            snapshot?.LastAuthUtc?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            snapshot?.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            metrics.HasMetrics ? "1" : "0");
    }

    private void ApplyOfflineDashboardFallback(bool hasSelection, DeviceSnapshot? snapshot, DeviceMetricsPresentation metrics, string placeholder)
    {
        ApplySafeDashboardFallback(hasSelection, snapshot, metrics, placeholder);
    }

    private void ApplySafeDashboardFallback(bool hasSelection, DeviceSnapshot? snapshot, DeviceMetricsPresentation metrics, string placeholder)
    {
        DashboardBrightnessValueText.Text = BuildBrightnessValueLabel(snapshot);
        DashboardBrightnessStatusText.Text = BuildBrightnessStatusLabel(snapshot);
        DashboardTelemetryHeartbeatText.Text = BuildHeartbeatLabel(snapshot);

        var sliderValue = snapshot?.BrightnessCap is int brightnessCap
            ? Math.Clamp(brightnessCap, SafeBrightnessMin, SafeBrightnessMax)
            : SafeBrightnessMax;
        suppressBrightnessSliderEvents = true;
        DashboardBrightnessSlider.Value = sliderValue;
        suppressBrightnessSliderEvents = false;
        brightnessCommitPending = false;

        DashboardLoopLoadText.Text = "-";
        DashboardHeapText.Text = "-";
        DashboardPsramText.Text = "-";
        DashboardHeapFragmentationText.Text = "Frag. de memoria -";
        DashboardPsramFragmentationText.Text = "PSRAM indisponivel";
        DashboardLoopLoadBar.Value = 0d;
        DashboardHeapFragmentationBar.Value = 0d;
        DashboardPsramFragmentationBar.Value = 0d;
        DashboardHeapFragmentationBar.Visibility = Visibility.Collapsed;
        DashboardPsramFragmentationBar.Visibility = Visibility.Collapsed;

        DashboardMetricsGrid.Visibility = Visibility.Collapsed;
        DashboardPlaceholderText.Text = placeholder;
        DashboardPlaceholderText.Visibility = Visibility.Visible;
        ApplyDashboardStatusSection(hasSelection, snapshot, metrics);
    }

    private void ApplyDashboardStatusSection(bool hasSelection, DeviceSnapshot? snapshot, DeviceMetricsPresentation metrics)
    {
        DashboardStatusSectionBorder.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        if (!hasSelection)
        {
            DashboardNetworkText.Text = "Wi-Fi: -";
            DashboardUptimeText.Text = "Uptime: -";
            DashboardPortalStateText.Text = "Modo configuracao: -";
            DashboardLastEventText.Text = "Ultimo evento de rede: -";
            DashboardAuxLedText.Text = "LED auxiliar: -";
            ApplyStreamStatus(snapshot: null);
            return;
        }

        DashboardNetworkText.Text = snapshot is null ? "Wi-Fi: -" : metrics.NetworkLabel;
        DashboardUptimeText.Text = snapshot is null ? "Uptime: -" : metrics.UptimeLabel;
        DashboardPortalStateText.Text = $"Modo configuracao: {BuildProvisioningPortalLabel(snapshot)}";
        DashboardLastEventText.Text = $"Ultimo evento de rede: {BuildCompactStatusLabel(snapshot?.LastWifiEvent)}";
        DashboardAuxLedText.Text = $"LED auxiliar: {BuildAuxLedAvailabilityLabel(snapshot)}";
        ApplyStreamStatus(snapshot);
    }

    private void ApplyStreamStatus(DeviceSnapshot? snapshot)
    {
        var primaryBrush = ResolveBrush("AppTextPrimaryBrush", Color.FromArgb(255, 255, 255, 255));
        StreamFramesReceivedText.Foreground = primaryBrush;
        StreamFramesAppliedText.Foreground = primaryBrush;
        StreamSuccessRateText.Foreground = primaryBrush;
        StreamGapCountText.Foreground = primaryBrush;
        StreamInvalidCountText.Foreground = primaryBrush;
        StreamLastSequenceText.Foreground = primaryBrush;

        if (snapshot is null)
        {
            StreamFramesReceivedText.Text = "-";
            StreamFramesAppliedText.Text = "-";
            StreamSuccessRateText.Text = "-";
            StreamGapCountText.Text = "-";
            StreamInvalidCountText.Text = "-";
            StreamLastSequenceText.Text = "-";
            return;
        }

        StreamFramesReceivedText.Text = (snapshot.StreamFramesReceived ?? 0).ToString("N0", CultureInfo.InvariantCulture);
        StreamFramesAppliedText.Text = (snapshot.StreamFramesApplied ?? 0).ToString("N0", CultureInfo.InvariantCulture);
        StreamSuccessRateText.Text = BuildStreamSuccessRateLabel(snapshot);
        StreamGapCountText.Text = (snapshot.StreamSequenceGapCount ?? 0).ToString(CultureInfo.InvariantCulture);
        StreamInvalidCountText.Text = (snapshot.StreamInvalidFrameCount ?? 0).ToString(CultureInfo.InvariantCulture);
        StreamLastSequenceText.Text = snapshot.StreamLastSequence?.ToString("N0", CultureInfo.InvariantCulture) ?? "-";
    }

    private static string BuildProvisioningPortalLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "-";
        }

        return snapshot.ProvisioningPortalActive switch
        {
            true => "Ativo",
            false => "Inativo",
            _ => "-",
        };
    }

    private static string BuildAuxLedAvailabilityLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "-";
        }

        return snapshot.AuxLedAvailable switch
        {
            true => "Disponivel",
            false => "Nao disponivel",
            _ => "-",
        };
    }

    private static string BuildCompactStatusLabel(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private static string BuildStreamSuccessRateLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot?.StreamFramesReceived is not uint framesReceived || framesReceived == 0)
        {
            return "-";
        }

        var framesApplied = snapshot.StreamFramesApplied ?? 0;
        var successRate = Math.Clamp((int)Math.Round((framesApplied / (double)framesReceived) * 100d), 0, 100);
        return $"{successRate}%";
    }

    private static bool TryComputePercent(long? value, int baseline, out int percent)
    {
        percent = 0;
        if (!value.HasValue || value.Value < 0 || baseline <= 0)
        {
            return false;
        }

        var ratio = value.Value / (double)baseline;
        if (!double.IsFinite(ratio))
        {
            return false;
        }

        var scaled = ratio * 100d;
        if (!double.IsFinite(scaled))
        {
            return false;
        }

        percent = Math.Clamp((int)Math.Round(scaled), 0, 100);
        return true;
    }

    private static double SafeProgress(double? value)
    {
        if (!value.HasValue || !double.IsFinite(value.Value))
        {
            return 0d;
        }

        return Math.Clamp(value.Value, 0d, 1d);
    }

    private static void LogRenderException(string stage, string? deviceId, DeviceSnapshot? snapshot, Exception ex)
    {
        AddLocalLog(
            $"Falha no render {stage} (device={deviceId ?? "-"}, status={snapshot?.Status.ToString() ?? "-"}, " +
            $"wifiState={snapshot?.WifiState ?? "-"}, telemetry={snapshot?.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-"}): {ex.Message}");
    }

    private static void LogSelectionBreadcrumb(
        string stage,
        string? deviceId,
        DeviceSnapshot? snapshot,
        DeviceMetricsPresentation? metrics)
    {
        AddLocalLog(
            $"Breadcrumb {stage} (device={deviceId ?? "-"}, status={snapshot?.Status.ToString() ?? "-"}, " +
            $"hasMetrics={metrics?.HasMetrics.ToString() ?? "-"}, lastTelemetry={snapshot?.LastTelemetryUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "-"})");
    }

    private static string BuildHeapSubLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot?.FreeHeapBytes is not long freeHeap || snapshot.LargestHeapBlockBytes is not long largest)
        {
            return "Frag. de memoria -";
        }

        if (freeHeap <= 0 || largest < 0)
        {
            return "Frag. de memoria -";
        }

        var fragmentation = Math.Clamp((int)Math.Round((1d - (largest / (double)freeHeap)) * 100d), 0, 100);
        return $"Frag. de memoria {fragmentation}%";
    }

    private static string BuildPsramSubLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot?.PsramAvailable != true)
        {
            return "PSRAM indisponivel";
        }

        return "Base 8 MB";
    }

    private static string ResolveDashboardPlaceholder(bool hasSelection, DeviceMetricsPresentation metrics)
    {
        if (!hasSelection)
        {
            return "Selecione um dispositivo para ver o status";
        }

        if (metrics.IsOfflineSnapshot)
        {
            return "Offline: exibindo ultimo snapshot conhecido";
        }

        if (!metrics.HasMetrics)
        {
            return metrics.PlaceholderMessage;
        }

        return string.Empty;
    }

    private static string BuildBrightnessValueLabel(DeviceSnapshot? snapshot)
    {
        var cap = snapshot?.BrightnessCap is int capValue
            ? Math.Clamp(capValue, SafeBrightnessMin, SafeBrightnessMax)
            : SafeBrightnessMax;
        return $"{cap}/160";
    }

    private static string BuildBrightnessStatusLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Brilho atual no painel: -";
        }

        var applied = snapshot.BrightnessApplied is int appliedValue
            ? Math.Clamp(appliedValue, 0, 255).ToString(CultureInfo.InvariantCulture)
            : "-";
        return $"Brilho atual no painel: {applied}";
    }

    private static string BuildHeartbeatLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Sinal de vida: - | LED de teste: - | Intensidade do LED: -";
        }

        var sequence = snapshot.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var ledState = snapshot.TestLedEnabled switch
        {
            true => "habilitado",
            false => "desabilitado",
            _ => snapshot.TestLedAvailable == false ? "indisponivel" : "-",
        };
        var duty = snapshot.TestLedDuty is int dutyValue
            ? Math.Clamp(dutyValue, 0, 255).ToString(CultureInfo.InvariantCulture)
            : "-";
        return $"Sinal de vida: #{sequence} | LED de teste: {ledState} | Intensidade do LED: {duty}";
    }

    private static string BuildSelectedSignalLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Sinal -";
        }

        return snapshot.LastKnownRssi is int rssi
            ? $"Sinal {rssi} dBm"
            : "Sinal indisponivel";
    }

    private static void ApplyTileTone(Border tileBorder)
    {
        tileBorder.BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81));
        tileBorder.Background = ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42));
    }
}
