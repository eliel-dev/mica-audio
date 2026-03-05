using System.Globalization;
using Device.Protocol.Models;

namespace App.Headless.Services;

internal readonly record struct LifecycleThresholds(TimeSpan Fresh, TimeSpan Stale, TimeSpan Dormant)
{
    public static LifecycleThresholds Default => new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromHours(24));
}

internal readonly record struct LifecyclePresentation(
    string PrimaryStateLabel,
    string SecondaryStateLabel,
    string DetailLabel,
    string LastSeenLabel,
    bool CanRunCommands,
    bool IsNeverSeen,
    bool IsConfigUncertain);

internal static class DevicesPageFormatting
{
    public const int SafeBrightnessMin = 30;
    public const int SafeBrightnessMax = 160;

    public static LifecyclePresentation BuildLifecycle(DeviceSnapshot snapshot, LifecycleThresholds thresholds, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Status == DeviceStatus.Pairing)
        {
            return new LifecyclePresentation(
                "Registrado",
                "Aguardando provisionamento",
                "Pareamento iniciado, aguardando primeira sessao",
                "Ultimo contato: desconhecido",
                false,
                true,
                false);
        }

        if (snapshot.IsRegistered && snapshot.FirstSeenUtc is null)
        {
            return new LifecyclePresentation(
                "Registrado",
                "Nunca conectado",
                "Aguardando primeiro provisionamento",
                "Ultimo contato: desconhecido",
                false,
                true,
                false);
        }

        if (snapshot.Status == DeviceStatus.Online)
        {
            return new LifecyclePresentation(
                "Online",
                "Configurado",
                "Telemetria ativa",
                "Ultimo contato: agora",
                true,
                false,
                false);
        }

        var lastSeen = NormalizeTimestamp(snapshot.LastSeenUtc);
        if (lastSeen is null)
        {
            return new LifecyclePresentation(
                "Offline",
                "Registro salvo",
                "Sem historico confiavel de contato",
                "Ultimo contato: desconhecido",
                false,
                false,
                false);
        }

        var elapsed = nowUtc - lastSeen.Value;
        var isConfigUncertain = snapshot.ConfigState == DeviceConfigState.DriftSuspected || elapsed >= thresholds.Dormant;

        return new LifecyclePresentation(
            "Offline",
            isConfigUncertain ? "Configuracao incerta" : "Configurado",
            isConfigUncertain ? "Registro salvo, sem contato ha muito tempo" : "Sem telemetria recente",
            BuildLastSeenLabel(elapsed),
            false,
            false,
            isConfigUncertain);
    }

    public static string BuildStatusLine(DeviceSnapshot snapshot, LifecyclePresentation presence)
    {
        var profile = string.IsNullOrWhiteSpace(snapshot.Profile) ? "-" : snapshot.Profile;
        return $"{presence.PrimaryStateLabel} | {presence.SecondaryStateLabel} | {presence.LastSeenLabel} | Perfil {profile}";
    }

    public static string BuildRegistrationLine(DeviceSnapshot snapshot, LifecyclePresentation presence)
    {
        if (snapshot.Status == DeviceStatus.Pairing)
        {
            return "Vinculo: pareamento iniciado; aguardando a primeira sessao do dispositivo";
        }

        if (presence.IsNeverSeen)
        {
            return "Vinculo: registrado localmente; aguardando primeiro provisionamento";
        }

        if (snapshot.Status == DeviceStatus.Online)
        {
            return "Vinculo: configurado e em comunicacao";
        }

        if (presence.IsConfigUncertain)
        {
            return "Vinculo: registro salvo localmente; a configuracao no ESP pode ter mudado";
        }

        return "Vinculo: registro salvo localmente; sem telemetria no momento";
    }

    public static string BuildSelectedAppLabel(DeviceStatus status, string? appName)
    {
        return status == DeviceStatus.Online
            ? $"App ativo: {ResolveDisplayValue(appName)}"
            : $"Ultimo app conhecido: {ResolveDisplayValue(appName)}";
    }

    public static string BuildSelectedSignalLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Sinal -";
        }

        return snapshot.LastKnownRssi is int rssi
            ? $"Sinal {rssi} dBm"
            : "Sinal indisponivel";
    }

    public static int ResolveBrightnessCap(DeviceSnapshot? snapshot)
    {
        return snapshot?.BrightnessCap is int capValue
            ? Math.Clamp(capValue, SafeBrightnessMin, SafeBrightnessMax)
            : SafeBrightnessMax;
    }

    public static string BuildBrightnessValueLabel(DeviceSnapshot? snapshot)
    {
        var cap = ResolveBrightnessCap(snapshot);
        return $"{cap}/160";
    }

    public static string BuildBrightnessStatusLabel(DeviceSnapshot? snapshot)
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

    public static string BuildHeartbeatLabel(DeviceSnapshot? snapshot)
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

    public static UiMetricsViewModel BuildMetrics(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new UiMetricsViewModel
            {
                StatusLabel = "Sem metricas",
                UptimeLabel = "Uptime: -",
                HeapLabel = "Heap livre: - | Maior bloco: -",
                PsramLabel = "PSRAM: desconhecida",
                NetworkLabel = "Wi-Fi: -",
                HasMetrics = false,
                IsOfflineSnapshot = false,
                IsPsramAvailable = false,
                PlaceholderMessage = "Selecione um dispositivo para ver metricas",
            };
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

        return new UiMetricsViewModel
        {
            StatusLabel = statusLabel,
            UptimeLabel = uptimeLabel,
            HeapLabel = heapLabel,
            PsramLabel = psramLabel,
            NetworkLabel = networkAndStreamLabel,
            LoopLoadPercent = loopLoadPercent,
            LoopLoadProgress = loopLoadProgress,
            HeapFragmentationProgress = heapFragmentationProgress,
            PsramFragmentationProgress = psramFragmentationProgress,
            HasMetrics = hasMetrics,
            IsOfflineSnapshot = snapshot.Status != DeviceStatus.Online,
            IsPsramAvailable = snapshot.PsramAvailable == true,
            PlaceholderMessage = placeholderMessage,
        };
    }

    public static UiConnectivityViewModel BuildConnectivity(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new UiConnectivityViewModel();
        }

        var wifiLabel = snapshot.WifiState?.ToLowerInvariant() switch
        {
            "connected" => "Conectado",
            "disconnected" => "Desconectado",
            "portal" => "Modo configuracao",
            "connecting" => "Conectando",
            _ => snapshot.WifiState ?? "-",
        };

        return new UiConnectivityViewModel
        {
            WifiStateLabel = $"Rede Wi-Fi: {wifiLabel}",
            PortalStateLabel = $"Modo configuracao: {(snapshot.ProvisioningPortalActive == true ? "Ativo" : "-")}",
            UptimeLabel = $"Ligado ha: {FormatUptimeForEspDash(snapshot.UptimeSeconds)}",
            LastEventLabel = $"Ultimo evento de rede: {ResolveDisplayValue(snapshot.LastWifiEvent)}",
            AuxLedLabel = $"LED auxiliar: {(snapshot.AuxLedAvailable == true ? "Disponivel" : snapshot.AuxLedAvailable == false ? "Nao disponivel" : "-")}",
        };
    }

    public static UiEspDashViewModel BuildEspDash(DeviceSnapshot? snapshot, IReadOnlyList<int> loopSeries, IReadOnlyList<int> heapSeries)
    {
        if (snapshot is null)
        {
            return new UiEspDashViewModel
            {
                LoopSeries = loopSeries,
                HeapSeries = heapSeries,
            };
        }

        var fps = 0;
        if (snapshot.UptimeSeconds is int uptimeSeconds && uptimeSeconds > 0 && snapshot.StreamFramesReceived is uint streamFrames)
        {
            fps = Math.Clamp((int)Math.Round(streamFrames / (double)uptimeSeconds), 0, 240);
        }

        var loopPercent = snapshot.LoopLoadPercent is int loopLoad ? Math.Clamp(loopLoad, 0, 100) : 0;
        var heapKb = snapshot.FreeHeapBytes is long freeHeap ? Math.Clamp((int)Math.Round(freeHeap / 1024d), 0, 4096) : 0;

        var successRate = "-";
        if (snapshot.StreamFramesReceived is uint rx && rx > 0 && snapshot.StreamFramesApplied is uint applied)
        {
            var success = Math.Clamp((int)Math.Round((applied / (double)rx) * 100d), 0, 100);
            successRate = $"{success}%";
        }

        return new UiEspDashViewModel
        {
            UptimeValue = FormatUptimeForEspDash(snapshot.UptimeSeconds),
            FpsValue = $"{fps} fps",
            HeapValue = $"{heapKb} KB",
            HeapSubValue = BuildHeapFragmentationSummary(snapshot),
            CpuPercent = loopPercent,
            SuccessRate = successRate,
            StreamFramesReceived = snapshot.StreamFramesReceived ?? 0,
            StreamFramesApplied = snapshot.StreamFramesApplied ?? 0,
            StreamGapCount = snapshot.StreamSequenceGapCount ?? 0,
            StreamInvalidCount = snapshot.StreamInvalidFrameCount ?? 0,
            StreamLastSequence = snapshot.StreamLastSequence ?? 0,
            LoopSeries = loopSeries,
            HeapSeries = heapSeries,
        };
    }

    public static string BuildHeapFragmentationSummary(DeviceSnapshot? snapshot)
    {
        if (snapshot?.FreeHeapBytes is not long freeHeap || snapshot.LargestHeapBlockBytes is not long largest || freeHeap <= 0)
        {
            return "Fragmentacao -";
        }

        var fragmentation = Math.Clamp((int)Math.Round((1d - (largest / (double)freeHeap)) * 100d), 0, 100);
        return $"Fragmentacao {fragmentation}%";
    }

    public static string FormatUptimeForEspDash(int? uptimeSeconds)
    {
        if (!uptimeSeconds.HasValue || uptimeSeconds.Value < 0)
        {
            return "-";
        }

        var uptime = TimeSpan.FromSeconds(uptimeSeconds.Value);
        return $"{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
    }

    private static string BuildLastSeenLabel(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Ultimo contato: ha menos de 1 min";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"Ultimo contato: ha {Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes))}m";
        }

        if (elapsed < TimeSpan.FromHours(24))
        {
            return $"Ultimo contato: ha {Math.Max(1, (int)Math.Floor(elapsed.TotalHours))}h";
        }

        return $"Ultimo contato: ha {Math.Max(1, (int)Math.Floor(elapsed.TotalDays))}d";
    }

    private static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? timestamp)
    {
        if (timestamp is null || timestamp.Value == default || timestamp.Value == DateTimeOffset.MinValue)
        {
            return null;
        }

        return timestamp;
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

        if (snapshot.WifiConnected == true)
        {
            var baseLabel = snapshot.LastKnownRssi.HasValue
                ? $"Wi-Fi: conectado | RSSI {snapshot.LastKnownRssi.Value} dBm"
                : "Wi-Fi: conectado";
            return AppendConnectivityDetails(baseLabel, wifiStateLabel, portalLabel, testLedLabel);
        }

        if (snapshot.WifiConnected == false)
        {
            return AppendConnectivityDetails("Wi-Fi: sem conexao", wifiStateLabel, portalLabel, testLedLabel);
        }

        if (snapshot.LastKnownRssi.HasValue)
        {
            return AppendConnectivityDetails($"Wi-Fi: RSSI {snapshot.LastKnownRssi.Value} dBm", wifiStateLabel, portalLabel, testLedLabel);
        }

        return AppendConnectivityDetails("Wi-Fi: -", wifiStateLabel, portalLabel, testLedLabel);
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
        if (!freeBytes.HasValue || freeBytes.Value <= 0 || !largestBlockBytes.HasValue)
        {
            return null;
        }

        if (largestBlockBytes.Value < 0 || largestBlockBytes.Value > freeBytes.Value)
        {
            return null;
        }

        return Math.Clamp(largestBlockBytes.Value / (double)freeBytes.Value, 0d, 1d);
    }

    private static string ResolveDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}
