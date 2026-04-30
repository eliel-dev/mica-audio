using System.Globalization;

namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal enum MonitoringSourceState
{
    Unavailable,
    Connected,
    Stale,
    Error,
}

internal enum MonitoringDataOrigin
{
    Unavailable,
    Hwinfo,
    WindowsFallback,
}

internal enum HwinfoReadingKind
{
    None = 0,
    Temp = 1,
    Volt = 2,
    Fan = 3,
    Current = 4,
    Power = 5,
    Clock = 6,
    Usage = 7,
    Other = 8,
}

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal sealed class MonitoringSnapshot
{
    public static MonitoringSnapshot Empty { get; } = new(
        MonitoringSourceState.Unavailable,
        "HWiNFO64 indisponivel",
        "Abra o HWiNFO64, habilite Shared Memory Support e mantenha os sensores ativos.",
        capturedAtUtc: null,
        sensorCount: 0,
        readingCount: 0,
        kpis: [],
        readings: []);

    public MonitoringSnapshot(
        MonitoringSourceState state,
        string stateText,
        string hintText,
        DateTimeOffset? capturedAtUtc,
        int sensorCount,
        int readingCount,
        IReadOnlyList<MonitoringKpi> kpis,
        IReadOnlyList<MonitoringReading> readings)
    {
        State = state;
        StateText = stateText;
        HintText = hintText;
        CapturedAtUtc = capturedAtUtc;
        SensorCount = sensorCount;
        ReadingCount = readingCount;
        Kpis = kpis;
        Readings = readings;
    }

    public MonitoringSourceState State { get; }

    public string StateText { get; }

    public string HintText { get; }

    public DateTimeOffset? CapturedAtUtc { get; }

    public int SensorCount { get; }

    public int ReadingCount { get; }

    public IReadOnlyList<MonitoringKpi> Kpis { get; }

    public IReadOnlyList<MonitoringReading> Readings { get; }
}

internal sealed class MonitoringKpi
{
    public MonitoringKpi(
        string id,
        string title,
        string contextText,
        IReadOnlyList<MonitoringKpiMetric> metrics,
        MonitoringKpiCapacity? capacity = null)
    {
        Id = id;
        Title = title;
        ContextText = contextText;
        Metrics = metrics;
        Capacity = capacity;
        IsAvailable = (capacity?.IsAvailable ?? false) || metrics.Any(static metric => metric.IsAvailable);
    }

    public string Id { get; }

    public string Title { get; }

    public string ContextText { get; }

    public IReadOnlyList<MonitoringKpiMetric> Metrics { get; }

    public MonitoringKpiCapacity? Capacity { get; }

    public bool IsAvailable { get; }
}

internal sealed class MonitoringKpiCapacity
{
    public MonitoringKpiCapacity(
        string usedText,
        string availableText,
        string totalText,
        string barText,
        double? progress,
        bool isAvailable,
        MonitoringDataOrigin dataOrigin)
    {
        UsedText = usedText;
        AvailableText = availableText;
        TotalText = totalText;
        BarText = barText;
        Progress = progress;
        IsAvailable = isAvailable;
        DataOrigin = dataOrigin;
    }

    public string UsedText { get; }

    public string AvailableText { get; }

    public string TotalText { get; }

    public string BarText { get; }

    public double? Progress { get; }

    public bool IsAvailable { get; }

    public MonitoringDataOrigin DataOrigin { get; }

    public static MonitoringKpiCapacity Unavailable()
        => new("-", "-", "-", "Indisponivel", progress: null, isAvailable: false, MonitoringDataOrigin.Unavailable);
}

internal sealed class MonitoringKpiMetric
{
    public MonitoringKpiMetric(
        string label,
        string hardwareName,
        string valueText,
        double? progress,
        bool isAvailable,
        MonitoringDataOrigin dataOrigin)
    {
        Label = label;
        HardwareName = hardwareName;
        ValueText = valueText;
        Progress = progress;
        IsAvailable = isAvailable;
        DataOrigin = dataOrigin;
    }

    public string Label { get; }

    public string HardwareName { get; }

    public string ValueText { get; }

    public double? Progress { get; }

    public bool IsAvailable { get; }

    public MonitoringDataOrigin DataOrigin { get; }

    public static MonitoringKpiMetric Unavailable(string label, string hardwareName)
        => new(label, hardwareName, "Indisponivel", progress: null, isAvailable: false, MonitoringDataOrigin.Unavailable);
}

internal sealed class MonitoringReading
{
    public MonitoringReading(
        uint sensorId,
        uint sensorInstance,
        string originalSensorName,
        string displaySensorName,
        uint readingId,
        string originalLabel,
        string displayLabel,
        string unit,
        HwinfoReadingKind kind,
        double? currentValue,
        double? minValue,
        double? maxValue,
        double? avgValue)
    {
        SensorId = sensorId;
        SensorInstance = sensorInstance;
        OriginalSensorName = originalSensorName;
        DisplaySensorName = displaySensorName;
        EffectiveSensorName = string.IsNullOrWhiteSpace(displaySensorName) ? originalSensorName : displaySensorName;
        ReadingId = readingId;
        OriginalLabel = originalLabel;
        DisplayLabel = displayLabel;
        EffectiveLabel = string.IsNullOrWhiteSpace(displayLabel) ? originalLabel : displayLabel;
        Unit = unit;
        Kind = kind;
        CurrentValue = currentValue;
        MinValue = minValue;
        MaxValue = maxValue;
        AvgValue = avgValue;
        CurrentText = FormatValue(currentValue, unit);
        MinText = FormatValue(minValue, unit);
        MaxText = FormatValue(maxValue, unit);
        AvgText = FormatValue(avgValue, unit);
        SearchText = MonitoringTextNormalization.NormalizeLookup(string.Join(
            "|",
            OriginalSensorName,
            DisplaySensorName,
            EffectiveSensorName,
            OriginalLabel,
            DisplayLabel,
            EffectiveLabel,
            unit));
    }

    public uint SensorId { get; }

    public uint SensorInstance { get; }

    public string OriginalSensorName { get; }

    public string DisplaySensorName { get; }

    public string EffectiveSensorName { get; }

    public string SensorName => EffectiveSensorName;

    public uint ReadingId { get; }

    public string OriginalLabel { get; }

    public string DisplayLabel { get; }

    public string EffectiveLabel { get; }

    public string Label => EffectiveLabel;

    public string Unit { get; }

    public HwinfoReadingKind Kind { get; }

    public double? CurrentValue { get; }

    public double? MinValue { get; }

    public double? MaxValue { get; }

    public double? AvgValue { get; }

    public string CurrentText { get; }

    public string MinText { get; }

    public string MaxText { get; }

    public string AvgText { get; }

    public string SearchText { get; }

    internal static string FormatValue(double? value, string unit)
    {
        if (!value.HasValue)
        {
            return "-";
        }

        if (string.Equals(unit, "%", StringComparison.Ordinal))
        {
            return value.Value.ToString("0.#", CultureInfo.InvariantCulture) + unit;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            return value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return value.Value.ToString("0.##", CultureInfo.InvariantCulture) + " " + unit;
    }
}

internal sealed class MonitoringMemoryFallbackSnapshot
{
    public static MonitoringMemoryFallbackSnapshot Empty { get; } = new(systemMemory: null, gpuMemory: null);

    public MonitoringMemoryFallbackSnapshot(MonitoringSystemMemoryFallback? systemMemory, MonitoringGpuMemoryFallback? gpuMemory)
    {
        SystemMemory = systemMemory;
        GpuMemory = gpuMemory;
    }

    public MonitoringSystemMemoryFallback? SystemMemory { get; }

    public MonitoringGpuMemoryFallback? GpuMemory { get; }
}

internal sealed class MonitoringSystemMemoryFallback
{
    public MonitoringSystemMemoryFallback(ulong usedBytes, ulong availableBytes, ulong totalBytes)
    {
        UsedBytes = usedBytes;
        AvailableBytes = availableBytes;
        TotalBytes = totalBytes;
    }

    public ulong UsedBytes { get; }

    public ulong AvailableBytes { get; }

    public ulong TotalBytes { get; }
}

internal sealed class MonitoringGpuMemoryFallback
{
    public MonitoringGpuMemoryFallback(string adapterName, ulong usedBytes, ulong totalBytes)
    {
        AdapterName = adapterName;
        UsedBytes = usedBytes;
        TotalBytes = totalBytes;
    }

    public string AdapterName { get; }

    public ulong UsedBytes { get; }

    public ulong TotalBytes { get; }

    public ulong AvailableBytes => UsedBytes >= TotalBytes ? 0UL : TotalBytes - UsedBytes;
}

internal sealed class HwinfoSharedMemoryPayload
{
    public HwinfoSharedMemoryPayload(
        HwinfoSharedMemoryHeader header,
        IReadOnlyList<HwinfoSensorEntry> sensors,
        IReadOnlyList<HwinfoReadingEntry> readings)
    {
        Header = header;
        Sensors = sensors;
        Readings = readings;
    }

    public HwinfoSharedMemoryHeader Header { get; }

    public IReadOnlyList<HwinfoSensorEntry> Sensors { get; }

    public IReadOnlyList<HwinfoReadingEntry> Readings { get; }
}

internal sealed class HwinfoSharedMemoryHeader
{
    public HwinfoSharedMemoryHeader(
        string signature,
        uint version,
        uint revision,
        DateTimeOffset? pollTimeUtc,
        uint sensorOffset,
        uint sensorElementSize,
        uint sensorCount,
        uint readingOffset,
        uint readingElementSize,
        uint readingCount)
    {
        Signature = signature;
        Version = version;
        Revision = revision;
        PollTimeUtc = pollTimeUtc;
        SensorOffset = sensorOffset;
        SensorElementSize = sensorElementSize;
        SensorCount = sensorCount;
        ReadingOffset = readingOffset;
        ReadingElementSize = readingElementSize;
        ReadingCount = readingCount;
    }

    public string Signature { get; }

    public uint Version { get; }

    public uint Revision { get; }

    public DateTimeOffset? PollTimeUtc { get; }

    public uint SensorOffset { get; }

    public uint SensorElementSize { get; }

    public uint SensorCount { get; }

    public uint ReadingOffset { get; }

    public uint ReadingElementSize { get; }

    public uint ReadingCount { get; }
}

internal sealed class HwinfoSensorEntry
{
    public HwinfoSensorEntry(uint sensorId, uint sensorInstance, string originalName, string userName)
    {
        SensorId = sensorId;
        SensorInstance = sensorInstance;
        OriginalName = originalName;
        UserName = userName;
        DisplayName = string.IsNullOrWhiteSpace(userName) ? originalName : userName;
    }

    public uint SensorId { get; }

    public uint SensorInstance { get; }

    public string OriginalName { get; }

    public string UserName { get; }

    public string DisplayName { get; }
}

internal sealed class HwinfoReadingEntry
{
    public HwinfoReadingEntry(
        HwinfoReadingKind kind,
        uint sensorIndex,
        uint readingId,
        string originalLabel,
        string userLabel,
        string unit,
        double? currentValue,
        double? minValue,
        double? maxValue,
        double? avgValue)
    {
        Kind = kind;
        SensorIndex = sensorIndex;
        ReadingId = readingId;
        OriginalLabel = originalLabel;
        UserLabel = userLabel;
        DisplayLabel = string.IsNullOrWhiteSpace(userLabel) ? originalLabel : userLabel;
        Unit = unit;
        CurrentValue = currentValue;
        MinValue = minValue;
        MaxValue = maxValue;
        AvgValue = avgValue;
    }

    public HwinfoReadingKind Kind { get; }

    public uint SensorIndex { get; }

    public uint ReadingId { get; }

    public string OriginalLabel { get; }

    public string UserLabel { get; }

    public string DisplayLabel { get; }

    public string Unit { get; }

    public double? CurrentValue { get; }

    public double? MinValue { get; }

    public double? MaxValue { get; }

    public double? AvgValue { get; }
}
