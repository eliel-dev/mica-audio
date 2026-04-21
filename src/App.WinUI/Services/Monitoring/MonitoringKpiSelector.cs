namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal static class MonitoringKpiSelector
{
    private const string MemoryCardDisplayUnit = "GB";

    public static IReadOnlyList<MonitoringKpi> Select(
        IReadOnlyList<MonitoringReading> readings,
        MonitoringMemoryFallbackSnapshot? fallbackSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var memoryFallback = fallbackSnapshot ?? MonitoringMemoryFallbackSnapshot.Empty;
        var hardware = MonitoringHardwareResolver.Resolve(readings);

        var cpuUsage = FindCpuUsage(readings, hardware.Cpu);
        var gpuUsage = FindGpuUsage(readings, hardware.Gpu);
        var cpuTemperature = FindCpuTemperature(readings, hardware.Cpu);
        var gpuTemperature = FindGpuTemperature(readings, hardware.Gpu);
        var ramUsed = FindSystemMemoryUsed(readings);
        var ramAvailable = FindSystemMemoryAvailable(readings);
        var ramTotal = FindSystemMemoryTotal(readings);
        var vramUsed = FindGpuMemoryUsed(readings, hardware.Gpu);
        var vramAvailable = FindGpuMemoryAvailable(readings, hardware.Gpu);
        var vramTotal = FindGpuMemoryTotal(readings, hardware.Gpu);
        var cpuPower = FindCpuPower(readings, hardware.Cpu);
        var gpuPower = FindGpuPower(readings, hardware.Gpu);
        var cpuClock = FindCpuClock(readings, hardware.Cpu);
        var gpuClock = FindGpuClock(readings, hardware.Gpu);

        var matchedGpuFallback = TryMatchGpuFallback(memoryFallback.GpuMemory, hardware.Gpu.HardwareDisplayName);

        return
        [
            BuildDualCard(
                "usage-total",
                "Uso total",
                BuildContextText(hardware.Cpu.HardwareDisplayName, hardware.Gpu.HardwareDisplayName),
                BuildMetricFromReading("CPU", hardware.Cpu.HardwareDisplayName, cpuUsage, showProgress: true),
                BuildMetricFromReading("GPU", hardware.Gpu.HardwareDisplayName, gpuUsage, showProgress: true)),
            BuildDualCard(
                "temperature-general",
                "Temperatura geral",
                BuildContextText(hardware.Cpu.HardwareDisplayName, hardware.Gpu.HardwareDisplayName),
                BuildMetricFromReading("CPU", hardware.Cpu.HardwareDisplayName, cpuTemperature, showProgress: true),
                BuildMetricFromReading("GPU", hardware.Gpu.HardwareDisplayName, gpuTemperature, showProgress: true)),
            BuildMemoryCard(
                "ram-usage",
                "Memoria RAM",
                "Memoria do sistema",
                "Memoria do sistema",
                ramUsed,
                ramAvailable,
                ramTotal,
                memoryFallback.SystemMemory),
            BuildMemoryCard(
                "gpu-vram",
                "VRAM GPU",
                hardware.Gpu.HardwareDisplayName,
                hardware.Gpu.HardwareDisplayName,
                vramUsed,
                vramAvailable,
                vramTotal,
                matchedGpuFallback),
            BuildDualCard(
                "power-consumption",
                "Consumo",
                BuildContextText(hardware.Cpu.HardwareDisplayName, hardware.Gpu.HardwareDisplayName),
                BuildMetricFromReading("CPU", hardware.Cpu.HardwareDisplayName, cpuPower, showProgress: false),
                BuildMetricFromReading("GPU", hardware.Gpu.HardwareDisplayName, gpuPower, showProgress: false)),
            BuildDualCard(
                "clock-frequency",
                "Frequencia",
                BuildContextText(hardware.Cpu.HardwareDisplayName, hardware.Gpu.HardwareDisplayName),
                BuildMetricFromReading("CPU", hardware.Cpu.HardwareDisplayName, cpuClock, showProgress: false),
                BuildMetricFromReading("GPU", hardware.Gpu.HardwareDisplayName, gpuClock, showProgress: false)),
        ];
    }

    private static MonitoringKpi BuildDualCard(string id, string title, string contextText, MonitoringKpiMetric primary, MonitoringKpiMetric secondary)
        => new(id, title, contextText, [primary, secondary]);

    private static MonitoringKpi BuildMemoryCard(
        string id,
        string title,
        string contextText,
        string hardwareName,
        MonitoringReading? used,
        MonitoringReading? available,
        MonitoringReading? total,
        object? fallback)
    {
        var usedMetric = BuildMemoryMetric("Usada", hardwareName, direct: used, counterpart: available, total, fallback, preferAvailable: false);
        var availableMetric = BuildMemoryMetric("Disponivel", hardwareName, direct: available, counterpart: used, total, fallback, preferAvailable: true);
        var capacity = BuildMemoryCapacity(used, available, total, fallback);
        return new MonitoringKpi(id, title, contextText, [usedMetric, availableMetric], capacity);
    }

    private static MonitoringKpiMetric BuildMetricFromReading(string label, string hardwareName, MonitoringReading? reading, bool showProgress)
    {
        if (reading is null)
        {
            return MonitoringKpiMetric.Unavailable(label, hardwareName);
        }

        return new MonitoringKpiMetric(
            label,
            hardwareName,
            reading.CurrentText,
            showProgress ? ResolveProgress(reading) : null,
            isAvailable: true,
            MonitoringDataOrigin.Hwinfo);
    }

    private static MonitoringKpiMetric BuildMemoryMetric(
        string label,
        string hardwareName,
        MonitoringReading? direct,
        MonitoringReading? counterpart,
        MonitoringReading? total,
        object? fallback,
        bool preferAvailable)
    {
        if (TryResolveHwinfoMemoryMetric(direct, counterpart, total, out var hwinfoValueText, out var hwinfoProgress))
        {
            return new MonitoringKpiMetric(label, hardwareName, hwinfoValueText, hwinfoProgress, isAvailable: true, MonitoringDataOrigin.Hwinfo);
        }

        if (TryResolveFallbackMemoryMetric(fallback, preferAvailable, out var fallbackValueText, out var fallbackProgress))
        {
            return new MonitoringKpiMetric(label, hardwareName, fallbackValueText, fallbackProgress, isAvailable: true, MonitoringDataOrigin.WindowsFallback);
        }

        return MonitoringKpiMetric.Unavailable(label, hardwareName);
    }

    private static MonitoringKpiCapacity BuildMemoryCapacity(
        MonitoringReading? used,
        MonitoringReading? available,
        MonitoringReading? total,
        object? fallback)
    {
        if (TryResolveHwinfoMemoryCapacity(used, available, total, out var hwinfoCapacity))
        {
            return hwinfoCapacity;
        }

        if (TryResolveFallbackMemoryCapacity(fallback, out var fallbackCapacity))
        {
            return fallbackCapacity;
        }

        return MonitoringKpiCapacity.Unavailable();
    }

    private static bool TryResolveHwinfoMemoryMetric(
        MonitoringReading? direct,
        MonitoringReading? counterpart,
        MonitoringReading? total,
        out string valueText,
        out double? progress)
    {
        if (direct?.CurrentValue is double directValue)
        {
            if (TryFormatMemoryCardValue(directValue, direct.Unit, out valueText))
            {
                progress = TryResolveRatio(directValue, direct.Unit, total, out var convertedRatio) ? convertedRatio : null;
                return true;
            }

            valueText = direct.CurrentText;
            progress = TryResolveRatio(directValue, direct.Unit, total, out var fallbackRatio) ? fallbackRatio : null;
            return true;
        }

        if (total?.CurrentValue is not double totalValue || counterpart?.CurrentValue is not double counterpartValue)
        {
            valueText = string.Empty;
            progress = null;
            return false;
        }

        if (!TryConvertMemoryValue(totalValue, total.Unit, MemoryCardDisplayUnit, out var convertedTotal) ||
            !TryConvertMemoryValue(counterpartValue, counterpart.Unit, MemoryCardDisplayUnit, out var convertedCounterpart))
        {
            valueText = string.Empty;
            progress = null;
            return false;
        }

        var resolvedValue = Math.Max(0d, convertedTotal - convertedCounterpart);
        valueText = MonitoringReading.FormatValue(resolvedValue, MemoryCardDisplayUnit);
        progress = convertedTotal > 0d ? ClampFraction(resolvedValue / convertedTotal) : null;
        return true;
    }

    private static bool TryResolveFallbackMemoryMetric(object? fallback, bool preferAvailable, out string valueText, out double? progress)
    {
        switch (fallback)
        {
            case MonitoringSystemMemoryFallback systemMemory:
                {
                    var bytes = preferAvailable ? systemMemory.AvailableBytes : systemMemory.UsedBytes;
                    valueText = FormatMemoryBytes(bytes);
                    progress = systemMemory.TotalBytes > 0UL ? ClampFraction(bytes / (double)systemMemory.TotalBytes) : null;
                    return true;
                }
            case MonitoringGpuMemoryFallback gpuMemory:
                {
                    var bytes = preferAvailable ? gpuMemory.AvailableBytes : gpuMemory.UsedBytes;
                    valueText = FormatMemoryBytes(bytes);
                    progress = gpuMemory.TotalBytes > 0UL ? ClampFraction(bytes / (double)gpuMemory.TotalBytes) : null;
                    return true;
                }
            default:
                valueText = string.Empty;
                progress = null;
                return false;
        }
    }

    private static bool TryResolveHwinfoMemoryCapacity(
        MonitoringReading? used,
        MonitoringReading? available,
        MonitoringReading? total,
        out MonitoringKpiCapacity capacity)
    {
        if (!TryResolveMemoryCapacityValues(used, available, total, out var usedValue, out var availableValue, out var totalValue))
        {
            capacity = MonitoringKpiCapacity.Unavailable();
            return false;
        }

        capacity = new MonitoringKpiCapacity(
            MonitoringReading.FormatValue(usedValue, MemoryCardDisplayUnit),
            MonitoringReading.FormatValue(availableValue, MemoryCardDisplayUnit),
            MonitoringReading.FormatValue(totalValue, MemoryCardDisplayUnit),
            BuildCapacityBarText(usedValue, totalValue),
            totalValue > 0d ? ClampFraction(usedValue / totalValue) : null,
            isAvailable: true,
            MonitoringDataOrigin.Hwinfo);
        return true;
    }

    private static bool TryResolveFallbackMemoryCapacity(object? fallback, out MonitoringKpiCapacity capacity)
    {
        switch (fallback)
        {
            case MonitoringSystemMemoryFallback systemMemory:
                {
                    capacity = BuildFallbackCapacity(systemMemory.UsedBytes, systemMemory.AvailableBytes, systemMemory.TotalBytes);
                    return true;
                }
            case MonitoringGpuMemoryFallback gpuMemory:
                {
                    capacity = BuildFallbackCapacity(gpuMemory.UsedBytes, gpuMemory.AvailableBytes, gpuMemory.TotalBytes);
                    return true;
                }
            default:
                capacity = MonitoringKpiCapacity.Unavailable();
                return false;
        }
    }

    private static MonitoringKpiCapacity BuildFallbackCapacity(ulong usedBytes, ulong availableBytes, ulong totalBytes)
    {
        var usedValue = usedBytes / (1024d * 1024d * 1024d);
        var availableValue = availableBytes / (1024d * 1024d * 1024d);
        var totalValue = totalBytes / (1024d * 1024d * 1024d);

        return new MonitoringKpiCapacity(
            MonitoringReading.FormatValue(usedValue, MemoryCardDisplayUnit),
            MonitoringReading.FormatValue(availableValue, MemoryCardDisplayUnit),
            MonitoringReading.FormatValue(totalValue, MemoryCardDisplayUnit),
            BuildCapacityBarText(usedValue, totalValue),
            totalValue > 0d ? ClampFraction(usedValue / totalValue) : null,
            isAvailable: true,
            MonitoringDataOrigin.WindowsFallback);
    }

    private static bool TryResolveMemoryCapacityValues(
        MonitoringReading? used,
        MonitoringReading? available,
        MonitoringReading? total,
        out double usedValue,
        out double availableValue,
        out double totalValue)
    {
        var hasUsed = TryGetMemoryCardValue(used, out usedValue);
        var hasAvailable = TryGetMemoryCardValue(available, out availableValue);
        var hasTotal = TryGetMemoryCardValue(total, out totalValue);

        if (!hasTotal && hasUsed && hasAvailable)
        {
            totalValue = usedValue + availableValue;
            hasTotal = true;
        }

        if (!hasUsed && hasTotal && hasAvailable)
        {
            usedValue = Math.Max(0d, totalValue - availableValue);
            hasUsed = true;
        }

        if (!hasAvailable && hasTotal && hasUsed)
        {
            availableValue = Math.Max(0d, totalValue - usedValue);
            hasAvailable = true;
        }

        return hasUsed && hasAvailable && hasTotal;
    }

    private static bool TryGetMemoryCardValue(MonitoringReading? reading, out double value)
    {
        if (reading?.CurrentValue is not double currentValue)
        {
            value = 0d;
            return false;
        }

        return TryConvertMemoryValue(currentValue, reading.Unit, MemoryCardDisplayUnit, out value);
    }

    private static string BuildCapacityBarText(double usedValue, double totalValue)
    {
        if (totalValue <= 0d)
        {
            return MonitoringReading.FormatValue(usedValue, MemoryCardDisplayUnit);
        }

        var percentText = (usedValue / totalValue * 100d).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        return percentText + " (" + MonitoringReading.FormatValue(usedValue, MemoryCardDisplayUnit) + ")";
    }

    private static MonitoringGpuMemoryFallback? TryMatchGpuFallback(MonitoringGpuMemoryFallback? fallback, string gpuHardwareName)
    {
        if (fallback is null)
        {
            return null;
        }

        var normalizedHardware = MonitoringTextNormalization.NormalizeLookup(gpuHardwareName);
        var normalizedAdapter = MonitoringTextNormalization.NormalizeLookup(fallback.AdapterName);
        if (string.IsNullOrWhiteSpace(normalizedHardware) ||
            string.IsNullOrWhiteSpace(normalizedAdapter) ||
            !normalizedAdapter.Contains(normalizedHardware, StringComparison.Ordinal))
        {
            return null;
        }

        return fallback;
    }

    private static string BuildContextText(string firstHardwareName, string secondHardwareName)
    {
        if (string.Equals(firstHardwareName, secondHardwareName, StringComparison.OrdinalIgnoreCase))
        {
            return firstHardwareName;
        }

        if (string.IsNullOrWhiteSpace(firstHardwareName))
        {
            return secondHardwareName;
        }

        if (string.IsNullOrWhiteSpace(secondHardwareName))
        {
            return firstHardwareName;
        }

        return firstHardwareName + " + " + secondHardwareName;
    }

    private static bool TryResolveRatio(double value, string valueUnit, MonitoringReading? total, out double ratio)
    {
        ratio = 0d;
        if (total?.CurrentValue is not double totalValue)
        {
            return false;
        }

        if (!TryConvertMemoryValue(value, valueUnit, MemoryCardDisplayUnit, out var convertedValue) ||
            !TryConvertMemoryValue(totalValue, total.Unit, MemoryCardDisplayUnit, out var convertedTotal) ||
            convertedTotal <= 0d)
        {
            return false;
        }

        ratio = ClampFraction(convertedValue / convertedTotal);
        return true;
    }

    private static string FormatMemoryBytes(ulong bytes)
    {
        const double kib = 1024d;
        const double gib = kib * 1024d * 1024d;
        return MonitoringReading.FormatValue(bytes / gib, MemoryCardDisplayUnit);
    }

    private static bool TryFormatMemoryCardValue(double value, string? unit, out string valueText)
    {
        if (!TryConvertMemoryValue(value, unit, MemoryCardDisplayUnit, out var convertedValue))
        {
            valueText = string.Empty;
            return false;
        }

        valueText = MonitoringReading.FormatValue(convertedValue, MemoryCardDisplayUnit);
        return true;
    }

    private static bool TryConvertMemoryValue(double value, string? fromUnit, string? toUnit, out double converted)
    {
        if (!TryGetMemoryScale(fromUnit, out var fromScale) || !TryGetMemoryScale(toUnit, out var toScale))
        {
            converted = 0d;
            return false;
        }

        converted = value * (fromScale / toScale);
        return true;
    }

    private static bool TryGetMemoryScale(string? unit, out double scale)
    {
        switch (MonitoringTextNormalization.NormalizeLookup(unit))
        {
            case "B":
            case "BYTES":
                scale = 1d;
                return true;
            case "KB":
            case "KIB":
                scale = 1024d;
                return true;
            case "MB":
            case "MIB":
                scale = 1024d * 1024d;
                return true;
            case "GB":
            case "GIB":
                scale = 1024d * 1024d * 1024d;
                return true;
            case "TB":
            case "TIB":
                scale = 1024d * 1024d * 1024d * 1024d;
                return true;
            default:
                scale = 0d;
                return false;
        }
    }

    private static double? ResolveProgress(MonitoringReading reading)
    {
        if (!reading.CurrentValue.HasValue)
        {
            return null;
        }

        if (string.Equals(reading.Unit, "%", StringComparison.Ordinal))
        {
            return ClampFraction(reading.CurrentValue.Value / 100d);
        }

        return reading.Kind switch
        {
            HwinfoReadingKind.Temp => ClampFraction(reading.CurrentValue.Value / 100d),
            HwinfoReadingKind.Usage => ClampFraction(reading.CurrentValue.Value / 100d),
            _ => null,
        };
    }

    private static double ClampFraction(double value)
        => Math.Clamp(value, 0d, 1d);

    private static MonitoringReading? FindCpuTemperature(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsTemperatureLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("CPU PACKAGE", 90), ("PACKAGE", 65), ("TCTL", 72), ("TDIE", 72), ("TEMPERATURA CPU", 62), ("DIE AVERAGE", 48));
            score -= ScoreTerms(reading, ("CORE 0", 48), ("CORE #0", 48), ("HOT SPOT", 40), ("CCD", 20));
            return score;
        });

    private static MonitoringReading? FindGpuTemperature(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsTemperatureLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("TEMPERATURA GPU", 90), ("GPU TEMPERATURE", 86), ("CORE TEMPERATURE", 70), ("EDGE", 52));
            score -= ScoreTerms(reading, ("MEMORIA", 28), ("MEMORY JUNCTION", 28), ("HOT SPOT", 14));
            return score;
        });

    private static MonitoringReading? FindCpuUsage(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsPercentLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("TOTAL CPU USAGE", 92), ("CPU TOTAL", 78), ("USO TOTAL CPU", 84), ("CARGA TOTAL CPU", 76), ("USAGE TOTAL", 54));
            score -= ScoreTerms(reading, ("C-STATE", 80), ("CORE 0", 36), ("THREAD", 16));
            return score;
        });

    private static MonitoringReading? FindGpuUsage(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsPercentLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("GPU USAGE", 90), ("GPU CORE LOAD", 78), ("USO DE MEMORIA GPU", 22), ("CARGA GPU", 74), ("CARGA DO NUCLEO DA GPU", 70), ("GPU D3D USAGE", 68));
            score -= ScoreTerms(reading, ("CONTROLADOR DE MEMORIA", 28), ("MECANISMO DE VIDEO", 28), ("BARRAMENTO", 24));
            return score;
        });

    private static MonitoringReading? FindSystemMemoryUsed(IReadOnlyList<MonitoringReading> readings)
        => FindBest(readings, reading =>
        {
            if (!IsSystemMemoryLike(reading))
            {
                return int.MinValue;
            }

            var signal = ScoreTerms(reading,
                ("PHYSICAL MEMORY USED", 92),
                ("MEMORY USED", 74),
                ("RAM USED", 74),
                ("USED MEMORY", 58),
                ("MEMORIA FISICA UTILIZADA", 96),
                ("MEMORIA FISICA USADA", 96),
                ("MEMORIA RAM UTILIZADA", 88),
                ("MEMORIA UTILIZADA", 64));
            return signal <= 0 ? int.MinValue : 10 + signal;
        });

    private static MonitoringReading? FindSystemMemoryAvailable(IReadOnlyList<MonitoringReading> readings)
        => FindBest(readings, reading =>
        {
            if (!IsSystemMemoryLike(reading))
            {
                return int.MinValue;
            }

            var signal = ScoreTerms(reading,
                ("PHYSICAL MEMORY AVAILABLE", 96),
                ("MEMORY AVAILABLE", 74),
                ("AVAILABLE MEMORY", 66),
                ("FREE MEMORY", 54),
                ("MEMORIA FISICA DISPONIVEL", 96),
                ("MEMORIA RAM DISPONIVEL", 88),
                ("MEMORIA DISPONIVEL", 64));
            return signal <= 0 ? int.MinValue : 10 + signal;
        });

    private static MonitoringReading? FindSystemMemoryTotal(IReadOnlyList<MonitoringReading> readings)
        => FindBest(readings, reading =>
        {
            if (!IsSystemMemoryLike(reading))
            {
                return int.MinValue;
            }

            var signal = ScoreTerms(reading,
                ("PHYSICAL MEMORY TOTAL", 96),
                ("MEMORY TOTAL", 72),
                ("TOTAL MEMORY", 62),
                ("MEMORIA FISICA TOTAL", 96),
                ("MEMORIA TOTAL", 72),
                ("RAM TOTAL", 62));
            return signal <= 0 ? int.MinValue : 10 + signal;
        });

    private static MonitoringReading? FindGpuMemoryUsed(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsGpuMemoryLike(reading))
            {
                return int.MinValue;
            }

            var signal = ScoreTerms(reading,
                ("VRAM USED", 92),
                ("GPU MEMORY USED", 80),
                ("VIDEO MEMORY USED", 80),
                ("DEDICATED MEMORY USED", 72),
                ("MEMORY ALLOCATED", 44),
                ("MEMORIA GPU ALOCADA", 98),
                ("MEMORIA DE VIDEO USADA", 90),
                ("MEMORIA DEDICADA GPU", 54));
            return signal <= 0 ? int.MinValue : BaseScore(reading, context) + signal;
        });

    private static MonitoringReading? FindGpuMemoryAvailable(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsGpuMemoryLike(reading))
            {
                return int.MinValue;
            }

            var signal = ScoreTerms(reading,
                ("VRAM AVAILABLE", 92),
                ("GPU MEMORY AVAILABLE", 84),
                ("VIDEO MEMORY AVAILABLE", 84),
                ("DEDICATED MEMORY AVAILABLE", 76),
                ("MEMORIA GPU DISPONIVEL", 98),
                ("MEMORIA DE VIDEO DISPONIVEL", 90),
                ("AVAILABLE", 22));
            return signal <= 0 ? int.MinValue : BaseScore(reading, context) + signal;
        });

    private static MonitoringReading? FindGpuMemoryTotal(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsGpuMemoryLike(reading))
            {
                return int.MinValue;
            }

            var signal = ScoreTerms(reading,
                ("VRAM TOTAL", 90),
                ("GPU MEMORY TOTAL", 82),
                ("VIDEO MEMORY TOTAL", 82),
                ("DEDICATED MEMORY TOTAL", 74),
                ("MEMORIA GPU TOTAL", 90),
                ("MEMORIA DE VIDEO TOTAL", 90));
            return signal <= 0 ? int.MinValue : BaseScore(reading, context) + signal;
        });

    private static MonitoringReading? FindCpuPower(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsPowerLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("CPU PACKAGE POWER", 92), ("TOTAL CPU POWER", 78), ("PACKAGE POWER", 64), ("POTENCIA CPU", 76), ("POTENCIA TOTAL CPU", 82));
            return score;
        });

    private static MonitoringReading? FindGpuPower(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsPowerLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("TOTAL BOARD POWER", 90), ("GPU POWER", 82), ("BOARD POWER", 70), ("POTENCIA GPU", 80), ("POTENCIA TOTAL GPU", 82));
            return score;
        });

    private static MonitoringReading? FindCpuClock(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsClockLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("CORE EFFECTIVE CLOCK (AVERAGE)", 98), ("AVERAGE EFFECTIVE CLOCK", 90), ("CLOCK MEDIO", 74), ("FREQUENCIA MEDIA", 74), ("CPU CLOCK", 42));
            score -= ScoreTerms(reading, ("BUS CLOCK", 34), ("CORE 0", 44));
            return score;
        });

    private static MonitoringReading? FindGpuClock(IReadOnlyList<MonitoringReading> readings, MonitoringHardwareContext context)
        => FindBest(readings, reading =>
        {
            if (!MatchesHardware(reading, context) || !IsClockLike(reading))
            {
                return int.MinValue;
            }

            var score = BaseScore(reading, context);
            score += ScoreTerms(reading, ("GPU CLOCK", 92), ("GRAPHICS CLOCK", 82), ("RELOGIO GPU", 84), ("FREQUENCIA GPU", 70), ("CORE CLOCK", 56));
            score -= ScoreTerms(reading, ("MEMORIA GPU", 32), ("VIDEO GPU", 26), ("PCIE", 22));
            return score;
        });

    private static MonitoringReading? FindBest(IReadOnlyList<MonitoringReading> readings, Func<MonitoringReading, int> score)
    {
        MonitoringReading? best = null;
        var bestScore = int.MinValue;
        foreach (var reading in readings)
        {
            var currentScore = score(reading);
            if (currentScore <= bestScore)
            {
                continue;
            }

            best = reading;
            bestScore = currentScore;
        }

        return bestScore == int.MinValue ? null : best;
    }

    private static int BaseScore(MonitoringReading reading, MonitoringHardwareContext context)
    {
        var score = 10;
        if (reading.CurrentValue.HasValue)
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(context.SectionDisplayName) &&
            MonitoringTextNormalization.ContainsAny(reading.EffectiveSensorName, context.SectionDisplayName))
        {
            score += 8;
        }

        return score;
    }

    private static int ScoreTerms(MonitoringReading reading, params (string Term, int Weight)[] weightedTerms)
    {
        var score = 0;
        foreach (var (term, weight) in weightedTerms)
        {
            if (MonitoringTextNormalization.ContainsAny(reading.OriginalLabel, term) ||
                MonitoringTextNormalization.ContainsAny(reading.DisplayLabel, term) ||
                MonitoringTextNormalization.ContainsAny(reading.EffectiveLabel, term))
            {
                score += weight;
            }
        }

        return score;
    }

    private static bool IsTemperatureLike(MonitoringReading reading)
        => reading.Kind == HwinfoReadingKind.Temp || MonitoringTextNormalization.ContainsAny(reading.Unit, "C", "°C");

    private static bool IsPowerLike(MonitoringReading reading)
        => reading.Kind == HwinfoReadingKind.Power || MonitoringTextNormalization.ContainsAny(reading.Unit, "W");

    private static bool IsClockLike(MonitoringReading reading)
        => reading.Kind == HwinfoReadingKind.Clock || MonitoringTextNormalization.ContainsAny(reading.Unit, "MHZ", "GHZ");

    private static bool IsPercentLike(MonitoringReading reading)
        => reading.Kind == HwinfoReadingKind.Usage || string.Equals(reading.Unit, "%", StringComparison.Ordinal);

    private static bool IsSystemMemoryLike(MonitoringReading reading)
        => MonitoringTextNormalization.ContainsAny(reading.OriginalSensorName, "SYSTEM", "WINDOWS", "MEMORIA", "MEMORY", "RAM") ||
           MonitoringTextNormalization.ContainsAny(reading.DisplaySensorName, "SYSTEM", "WINDOWS", "MEMORIA", "MEMORY", "RAM") ||
           MonitoringTextNormalization.ContainsAny(reading.OriginalLabel, "MEMORIA", "MEMORY", "RAM") ||
           MonitoringTextNormalization.ContainsAny(reading.DisplayLabel, "MEMORIA", "MEMORY", "RAM");

    private static bool IsGpuMemoryLike(MonitoringReading reading)
        => TryGetMemoryScale(reading.Unit, out _) ||
           MonitoringTextNormalization.ContainsAny(reading.OriginalLabel, "MEMORIA GPU", "GPU MEMORY", "VRAM", "VIDEO MEMORY", "DEDICATED") ||
           MonitoringTextNormalization.ContainsAny(reading.DisplayLabel, "MEMORIA GPU", "GPU MEMORY", "VRAM", "VIDEO MEMORY", "DEDICATED");

    private static bool MatchesHardware(MonitoringReading reading, MonitoringHardwareContext context)
        => MonitoringHardwareResolver.BelongsToHardware(reading, context);
}
