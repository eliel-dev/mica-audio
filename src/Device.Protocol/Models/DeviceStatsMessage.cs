namespace Device.Protocol.Models;

// DOCS: docs/wiki/reference/device-observability-dashboard.md#contrato-stats
public sealed class DeviceStatsMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public string? ChipModel { get; init; }

    public int? ChipRevision { get; init; }

    public int? ChipCores { get; init; }

    public int? CpuFreqMHz { get; init; }

    public string? SdkVersion { get; init; }

    public long? HeapTotalBytes { get; init; }

    public long? PsramTotalBytes { get; init; }

    public long? FlashTotalBytes { get; init; }

    public long? SketchSizeBytes { get; init; }

    public long? FreeSketchBytes { get; init; }
}
