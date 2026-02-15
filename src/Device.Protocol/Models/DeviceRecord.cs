namespace Device.Protocol.Models;

public sealed class DeviceRecord
{
    public string DeviceId { get; init; } = string.Empty;

    public string Name { get; init; } = "Matrix Portal";

    public string Profile { get; init; } = "stable";

    public string Token { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenUtc { get; init; } = DateTimeOffset.MinValue;

    public string? FirmwareVersion { get; init; }

    public string? LastKnownIp { get; init; }

    public int? LastKnownRssi { get; init; }
}
