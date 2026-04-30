namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal static class MonitoringHardwareResolver
{
    public static MonitoringHardwareMap Resolve(IReadOnlyList<MonitoringReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        return new MonitoringHardwareMap(
            ResolveContext(readings, DeviceKind.Cpu),
            ResolveContext(readings, DeviceKind.Gpu));
    }

    public static bool BelongsToHardware(MonitoringReading reading, MonitoringHardwareContext? context)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (context is null || string.IsNullOrWhiteSpace(context.HardwareDisplayName))
        {
            return true;
        }

        return MatchesHardware(reading.OriginalSensorName, context.HardwareDisplayName) ||
               MatchesHardware(reading.DisplaySensorName, context.HardwareDisplayName) ||
               MatchesHardware(reading.EffectiveSensorName, context.HardwareDisplayName);
    }

    private static MonitoringHardwareContext ResolveContext(IReadOnlyList<MonitoringReading> readings, DeviceKind deviceKind)
    {
        MonitoringHardwareContext? best = null;
        var bestScore = int.MinValue;

        foreach (var reading in readings)
        {
            foreach (var sensorName in EnumerateSensorNames(reading))
            {
                if (TryExtract(sensorName, deviceKind, out var candidate, out var score) && score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
        }

        return best ?? MonitoringHardwareContext.Fallback(deviceKind == DeviceKind.Cpu ? "CPU" : "GPU");
    }

    private static IEnumerable<string> EnumerateSensorNames(MonitoringReading reading)
    {
        yield return reading.OriginalSensorName;
        if (!string.Equals(reading.DisplaySensorName, reading.OriginalSensorName, StringComparison.Ordinal))
        {
            yield return reading.DisplaySensorName;
        }

        if (!string.Equals(reading.EffectiveSensorName, reading.DisplaySensorName, StringComparison.Ordinal))
        {
            yield return reading.EffectiveSensorName;
        }
    }

    private static bool TryExtract(string? sensorName, DeviceKind deviceKind, out MonitoringHardwareContext context, out int score)
    {
        context = MonitoringHardwareContext.Fallback(deviceKind == DeviceKind.Cpu ? "CPU" : "GPU");
        score = int.MinValue;

        if (string.IsNullOrWhiteSpace(sensorName))
        {
            return false;
        }

        var segments = sensorName
            .Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            segments = [sensorName.Trim()];
        }

        var bestSegment = string.Empty;
        var bestSegmentScore = int.MinValue;
        for (var index = 0; index < segments.Length; index++)
        {
            var currentSegment = segments[index];
            var currentScore = ScoreSegment(currentSegment, deviceKind);
            if (currentScore <= bestSegmentScore)
            {
                continue;
            }

            bestSegment = currentSegment;
            bestSegmentScore = currentScore;
        }

        if (bestSegmentScore < 30)
        {
            return false;
        }

        var sectionName = string.Empty;
        for (var index = 0; index < segments.Length; index++)
        {
            if (!string.Equals(segments[index], bestSegment, StringComparison.Ordinal))
            {
                continue;
            }

            if (index < segments.Length - 1)
            {
                sectionName = string.Join(" : ", segments[(index + 1)..]);
            }

            break;
        }

        context = new MonitoringHardwareContext(bestSegment.Trim(), sectionName.Trim(), sensorName.Trim());
        score = bestSegmentScore;
        return true;
    }

    private static bool MatchesHardware(string? sensorName, string hardwareName)
    {
        if (string.IsNullOrWhiteSpace(sensorName) || string.IsNullOrWhiteSpace(hardwareName))
        {
            return false;
        }

        var normalizedSensor = MonitoringTextNormalization.NormalizeLookup(sensorName);
        var normalizedHardware = MonitoringTextNormalization.NormalizeLookup(hardwareName);
        return normalizedSensor.Contains(normalizedHardware, StringComparison.Ordinal);
    }

    private static int ScoreSegment(string segment, DeviceKind deviceKind)
    {
        var score = 0;
        if (deviceKind == DeviceKind.Cpu)
        {
            if (MonitoringTextNormalization.ContainsAny(segment, "RYZEN", "THREADRIPPER", "EPYC", "INTEL CORE", "CORE ULTRA", "XEON", "PENTIUM", "CELERON"))
            {
                score += 120;
            }

            if (MonitoringTextNormalization.ContainsAny(segment, "CPU"))
            {
                score += 30;
            }
        }
        else
        {
            if (MonitoringTextNormalization.ContainsAny(segment, "GEFORCE", "RTX", "GTX", "RADEON", "ARC", "QUADRO", "TESLA"))
            {
                score += 120;
            }

            if (MonitoringTextNormalization.ContainsAny(segment, "GPU"))
            {
                score += 30;
            }
        }

        if (MonitoringTextNormalization.ContainsAny(segment, "[#", "C-STATE", "DTS", "ENHANCED", "UTILIZATION", "OCUPACAO", "CLOCK", "SENSOR", "MEMORIA"))
        {
            score -= 40;
        }

        if (segment.Length >= 6)
        {
            score += 4;
        }

        return score;
    }

    private enum DeviceKind
    {
        Cpu,
        Gpu,
    }
}

internal sealed class MonitoringHardwareMap
{
    public MonitoringHardwareMap(MonitoringHardwareContext cpu, MonitoringHardwareContext gpu)
    {
        Cpu = cpu;
        Gpu = gpu;
    }

    public MonitoringHardwareContext Cpu { get; }

    public MonitoringHardwareContext Gpu { get; }
}

internal sealed class MonitoringHardwareContext
{
    public MonitoringHardwareContext(string hardwareDisplayName, string sectionDisplayName, string sourceSensorName)
    {
        HardwareDisplayName = hardwareDisplayName;
        SectionDisplayName = sectionDisplayName;
        SourceSensorName = sourceSensorName;
    }

    public string HardwareDisplayName { get; }

    public string SectionDisplayName { get; }

    public string SourceSensorName { get; }

    public static MonitoringHardwareContext Fallback(string fallback)
        => new(fallback, string.Empty, fallback);
}
