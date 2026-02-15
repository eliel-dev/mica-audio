namespace Device.Protocol.Models;

public sealed class DeviceCommandAckRequest
{
    public string DeviceId { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string? Message { get; init; }

    public int? ProgressPercent { get; init; }

    public string? Stage { get; init; }

    public string? ErrorCode { get; init; }
}
