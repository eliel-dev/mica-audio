using Device.Protocol.Models;
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
            NetworkLabel: networkLabel,
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
            || snapshot.LastKnownRssi.HasValue;
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
        if (snapshot.WifiConnected == true)
        {
            if (snapshot.LastKnownRssi.HasValue)
            {
                return $"Wi-Fi: conectado | RSSI {snapshot.LastKnownRssi.Value} dBm";
            }

            return "Wi-Fi: conectado";
        }

        if (snapshot.WifiConnected == false)
        {
            return "Wi-Fi: sem conexao";
        }

        if (snapshot.LastKnownRssi.HasValue)
        {
            return $"Wi-Fi: RSSI {snapshot.LastKnownRssi.Value} dBm";
        }

        return "Wi-Fi: -";
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
