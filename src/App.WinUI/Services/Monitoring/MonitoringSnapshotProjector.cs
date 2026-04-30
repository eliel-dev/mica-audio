namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal static class MonitoringSnapshotProjector
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(10);

    public static MonitoringSnapshot CreateUnavailableSnapshot(string stateText, string hintText)
        => new(
            MonitoringSourceState.Unavailable,
            stateText,
            hintText,
            capturedAtUtc: null,
            sensorCount: 0,
            readingCount: 0,
            kpis: MonitoringKpiSelector.Select([], MonitoringMemoryFallbackSnapshot.Empty),
            readings: []);

    public static MonitoringSnapshot CreateErrorSnapshot(string stateText, string hintText)
        => new(
            MonitoringSourceState.Error,
            stateText,
            hintText,
            capturedAtUtc: null,
            sensorCount: 0,
            readingCount: 0,
            kpis: MonitoringKpiSelector.Select([], MonitoringMemoryFallbackSnapshot.Empty),
            readings: []);

    public static MonitoringSnapshot Project(HwinfoSharedMemoryPayload payload, DateTimeOffset nowUtc, MonitoringMemoryFallbackSnapshot? fallbackSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var state = ResolveState(payload.Header, nowUtc);
        var readings = BuildReadings(payload);
        return new MonitoringSnapshot(
            state,
            BuildStateText(state),
            BuildHintText(state),
            payload.Header.PollTimeUtc,
            payload.Sensors.Count,
            readings.Count,
            MonitoringKpiSelector.Select(readings, fallbackSnapshot ?? MonitoringMemoryFallbackSnapshot.Empty),
            readings);
    }

    private static MonitoringSourceState ResolveState(HwinfoSharedMemoryHeader header, DateTimeOffset nowUtc)
    {
        if (string.Equals(header.Signature, HwinfoSharedMemoryBinaryParser.ActiveSignature, StringComparison.Ordinal))
        {
            if (header.PollTimeUtc.HasValue && (nowUtc - header.PollTimeUtc.Value) > StaleThreshold)
            {
                return MonitoringSourceState.Stale;
            }

            return MonitoringSourceState.Connected;
        }

        if (string.Equals(header.Signature, HwinfoSharedMemoryBinaryParser.DeadSignature, StringComparison.Ordinal))
        {
            return MonitoringSourceState.Unavailable;
        }

        return MonitoringSourceState.Error;
    }

    private static List<MonitoringReading> BuildReadings(HwinfoSharedMemoryPayload payload)
    {
        var readings = new List<MonitoringReading>(payload.Readings.Count);
        foreach (var reading in payload.Readings)
        {
            if (reading.SensorIndex >= payload.Sensors.Count)
            {
                continue;
            }

            var sensor = payload.Sensors[(int)reading.SensorIndex];
            readings.Add(new MonitoringReading(
                sensor.SensorId,
                sensor.SensorInstance,
                sensor.OriginalName,
                sensor.DisplayName,
                reading.ReadingId,
                reading.OriginalLabel,
                reading.DisplayLabel,
                reading.Unit,
                reading.Kind,
                reading.CurrentValue,
                reading.MinValue,
                reading.MaxValue,
                reading.AvgValue));
        }

        readings.Sort(static (left, right) =>
        {
            var sensorOrder = string.Compare(left.EffectiveSensorName, right.EffectiveSensorName, StringComparison.OrdinalIgnoreCase);
            if (sensorOrder != 0)
            {
                return sensorOrder;
            }

            var labelOrder = string.Compare(left.EffectiveLabel, right.EffectiveLabel, StringComparison.OrdinalIgnoreCase);
            if (labelOrder != 0)
            {
                return labelOrder;
            }

            return left.ReadingId.CompareTo(right.ReadingId);
        });
        return readings;
    }

    internal static string BuildStateText(MonitoringSourceState state)
        => state switch
        {
            MonitoringSourceState.Connected => "Conectado ao HWiNFO64",
            MonitoringSourceState.Stale => "Snapshot do HWiNFO64 esta desatualizado",
            MonitoringSourceState.Error => "Falha ao ler o HWiNFO64",
            _ => "HWiNFO64 indisponivel",
        };

    internal static string BuildHintText(MonitoringSourceState state)
        => state switch
        {
            MonitoringSourceState.Connected => "Shared Memory ativo. O dashboard esta lendo os sensores locais do PC.",
            MonitoringSourceState.Stale => "O Shared Memory esta acessivel, mas o poll do HWiNFO64 nao atualiza ha alguns segundos.",
            MonitoringSourceState.Error => "Verifique se o HWiNFO64 esta com Shared Memory Support habilitado e sem conflito de permissao.",
            _ => "Abra o HWiNFO64, habilite Shared Memory Support e mantenha a janela de sensores ativa.",
        };
}
