using Device.Protocol.Models;
using System.Collections.Generic;
using System.Globalization;

// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
namespace App.WinUI.Services.Devices;

internal static class DeviceMetricsFormatter
{
    public static DeviceMetricsPresentation Build(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new DeviceMetricsPresentation(
                StatusLabel: "Sem metricas",
                UptimeLabel: "Uptime: -",
                HeapLabel: "Heap livre: - | Maior bloco: -",
                PsramLabel: "PSRAM: desconhecida",
                NetworkLabel: "Wi-Fi: -",
                LoopLoadPercent: null,
                LoopLoadProgress: 0d,
                HeapFragmentationProgress: null,
                PsramFragmentationProgress: null,
                HasMetrics: false,
                IsOfflineSnapshot: false,
                IsPsramAvailable: false,
                PlaceholderMessage: "Selecione um dispositivo para ver metricas");
        }

        var hasMetrics = HasAnyMetrics(snapshot);
        var loopLoadPercent = snapshot.LoopLoadPercent;
        var loopLoadProgress = loopLoadPercent.HasValue
            ? Math.Clamp(loopLoadPercent.Value, 0, 100) / 100d
            : 0d;

        var statusLabel = ResolveStatusLabel(snapshot, hasMetrics);
        var uptimeLabel = $"Uptime: {FormatUptime(snapshot.UptimeSeconds)}";
        var heapLabel = $"Heap livre: {FormatBytes(snapshot.FreeHeapBytes)} | Maior bloco: {FormatBytes(snapshot.LargestHeapBlockBytes)}";
        var psramLabel = BuildPsramLabel(snapshot);
        var networkLabel = BuildNetworkLabel(snapshot);
        var streamLabel = BuildStreamLabel(snapshot);
        var networkAndStreamLabel = string.IsNullOrWhiteSpace(streamLabel)
            ? networkLabel
            : string.Concat(networkLabel, " | ", streamLabel);
        var heapFragmentationProgress = ComputeFragmentationProgress(snapshot.FreeHeapBytes, snapshot.LargestHeapBlockBytes);
        var psramFragmentationProgress = snapshot.PsramAvailable == true
            ? ComputeFragmentationProgress(snapshot.FreePsramBytes, snapshot.LargestPsramBlockBytes)
            : null;
        var placeholderMessage = hasMetrics ? string.Empty : "Nenhuma metrica recebida ainda";

        return new DeviceMetricsPresentation(
            StatusLabel: statusLabel,
            UptimeLabel: uptimeLabel,
            HeapLabel: heapLabel,
            PsramLabel: psramLabel,
            NetworkLabel: networkAndStreamLabel,
            LoopLoadPercent: loopLoadPercent,
            LoopLoadProgress: loopLoadProgress,
            HeapFragmentationProgress: heapFragmentationProgress,
            PsramFragmentationProgress: psramFragmentationProgress,
            HasMetrics: hasMetrics,
            IsOfflineSnapshot: snapshot.Status != DeviceStatus.Online,
            IsPsramAvailable: snapshot.PsramAvailable == true,
            PlaceholderMessage: placeholderMessage);
    }

    private static string ResolveStatusLabel(DeviceSnapshot snapshot, bool hasMetrics)
    {
        if (snapshot.Status != DeviceStatus.Online)
        {
            return "Offline (ultimo snapshot)";
        }

        return hasMetrics ? "Online" : "Sem metricas";
    }

    private static bool HasAnyMetrics(DeviceSnapshot snapshot)
    {
        return snapshot.UptimeSeconds.HasValue
            || snapshot.LoopLoadPercent.HasValue
            || snapshot.FreeHeapBytes.HasValue
            || snapshot.LargestHeapBlockBytes.HasValue
            || snapshot.PsramAvailable.HasValue
            || snapshot.FreePsramBytes.HasValue
            || snapshot.LargestPsramBlockBytes.HasValue
            || snapshot.WifiConnected.HasValue
            || !string.IsNullOrWhiteSpace(snapshot.WifiState)
            || snapshot.ProvisioningPortalActive.HasValue
            || snapshot.AuxLedAvailable.HasValue
            || snapshot.TestLedAvailable.HasValue
            || !string.IsNullOrWhiteSpace(snapshot.LastWifiEvent)
            || snapshot.LastKnownRssi.HasValue
            || snapshot.StreamFramesReceived.HasValue
            || snapshot.StreamFramesApplied.HasValue
            || snapshot.StreamSequenceGapCount.HasValue
            || snapshot.StreamInvalidFrameCount.HasValue;
    }

    private static string BuildPsramLabel(DeviceSnapshot snapshot)
    {
        if (snapshot.PsramAvailable == true)
        {
            return $"PSRAM livre: {FormatBytes(snapshot.FreePsramBytes)} | Maior bloco: {FormatBytes(snapshot.LargestPsramBlockBytes)}";
        }

        if (snapshot.PsramAvailable == false)
        {
            return "PSRAM: indisponivel neste build";
        }

        return "PSRAM: desconhecida";
    }

    private static string BuildNetworkLabel(DeviceSnapshot snapshot)
    {
        var wifiStateLabel = !string.IsNullOrWhiteSpace(snapshot.WifiState)
            ? snapshot.WifiState
            : null;
        var portalLabel = snapshot.ProvisioningPortalActive switch
        {
            true => "portal ativo",
            false => "portal inativo",
            _ => null,
        };
        var effectiveTestLedAvailable = snapshot.TestLedAvailable ?? snapshot.AuxLedAvailable;
        var testLedLabel = effectiveTestLedAvailable switch
        {
            true => "LED teste disponivel",
            false => "LED teste indisponivel",
            _ => null,
        };

        if (snapshot.Status != DeviceStatus.Online)
        {
            var offlineLabel = snapshot.WifiConnected == false
                ? "Wi-Fi: sem conexao"
                : "Wi-Fi: indisponivel (offline)";
            return AppendConnectivityDetails(offlineLabel, wifiStateLabel, portalLabel, testLedLabel);
        }

        string baseLabel;
        if (snapshot.WifiConnected == true)
        {
            if (snapshot.LastKnownRssi.HasValue)
            {
                baseLabel = $"Wi-Fi: conectado | RSSI {snapshot.LastKnownRssi.Value} dBm";
                return AppendConnectivityDetails(baseLabel, wifiStateLabel, portalLabel, testLedLabel);
            }

            baseLabel = "Wi-Fi: conectado";
            return AppendConnectivityDetails(baseLabel, wifiStateLabel, portalLabel, testLedLabel);
        }

        if (snapshot.WifiConnected == false)
        {
            baseLabel = "Wi-Fi: sem conexao";
            return AppendConnectivityDetails(baseLabel, wifiStateLabel, portalLabel, testLedLabel);
        }

        if (snapshot.LastKnownRssi.HasValue)
        {
            baseLabel = $"Wi-Fi: RSSI {snapshot.LastKnownRssi.Value} dBm";
            return AppendConnectivityDetails(baseLabel, wifiStateLabel, portalLabel, testLedLabel);
        }

        baseLabel = "Wi-Fi: -";
        return AppendConnectivityDetails(baseLabel, wifiStateLabel, portalLabel, testLedLabel);
    }

    private static string AppendConnectivityDetails(string baseLabel, string? wifiStateLabel, string? portalLabel, string? auxLedLabel)
    {
        var details = new List<string>(capacity: 3);
        if (!string.IsNullOrWhiteSpace(wifiStateLabel))
        {
            details.Add($"state {wifiStateLabel}");
        }

        if (!string.IsNullOrWhiteSpace(portalLabel))
        {
            details.Add(portalLabel!);
        }

        if (!string.IsNullOrWhiteSpace(auxLedLabel))
        {
            details.Add(auxLedLabel!);
        }

        if (details.Count == 0)
        {
            return baseLabel;
        }

        return string.Concat(baseLabel, " | ", string.Join(" | ", details));
    }

    private static string BuildStreamLabel(DeviceSnapshot snapshot)
    {
        if (!snapshot.StreamFramesReceived.HasValue
            && !snapshot.StreamFramesApplied.HasValue
            && !snapshot.StreamSequenceGapCount.HasValue
            && !snapshot.StreamInvalidFrameCount.HasValue)
        {
            return string.Empty;
        }

        var rx = snapshot.StreamFramesReceived?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var applied = snapshot.StreamFramesApplied?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var gaps = snapshot.StreamSequenceGapCount?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var invalid = snapshot.StreamInvalidFrameCount?.ToString(CultureInfo.InvariantCulture) ?? "-";

        return $"Stream RX/APL {rx}/{applied} | GAP {gaps} | INV {invalid}";
    }

    private static string FormatUptime(int? uptimeSeconds)
    {
        if (!uptimeSeconds.HasValue || uptimeSeconds.Value < 0)
        {
            return "-";
        }

        var uptime = TimeSpan.FromSeconds(uptimeSeconds.Value);
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours:00}h {uptime.Minutes:00}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes:00}m";
        }

        if (uptime.TotalMinutes >= 1)
        {
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds:00}s";
        }

        return $"{uptime.Seconds}s";
    }

    private static string FormatBytes(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value < 0)
        {
            return "-";
        }

        const double KiB = 1024d;
        const double MiB = 1024d * 1024d;

        if (bytes.Value < 1024)
        {
            return $"{bytes.Value} B";
        }

        if (bytes.Value < MiB)
        {
            return (bytes.Value / KiB).ToString("0", CultureInfo.InvariantCulture) + " KB";
        }

        return (bytes.Value / MiB).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    private static double? ComputeFragmentationProgress(long? freeBytes, long? largestBlockBytes)
    {
        if (!freeBytes.HasValue || freeBytes.Value <= 0)
        {
            return null;
        }

        if (!largestBlockBytes.HasValue)
        {
            return null;
        }

        if (largestBlockBytes.Value < 0 || largestBlockBytes.Value > freeBytes.Value)
        {
            return null;
        }

        return Math.Clamp(largestBlockBytes.Value / (double)freeBytes.Value, 0d, 1d);
    }
}
