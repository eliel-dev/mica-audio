namespace Device.Protocol.Models;

public sealed class DeviceOtaResultRequest
{
    public string DeviceId { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? ErrorCode { get; init; }
}
