namespace Device.Protocol.Models;

// DOCS: docs/wiki/reference/device-observability-dashboard.md#contrato-logs
public sealed class DeviceLogMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public uint Sequence { get; init; }

    public string Level { get; init; } = "info";

    public string Category { get; init; } = "device";

    public string? EventCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public int? UptimeSeconds { get; init; }

    public uint? TelemetrySequence { get; init; }
}
