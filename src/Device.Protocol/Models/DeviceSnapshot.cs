namespace Device.Protocol.Models;

public sealed class DeviceSnapshot
{
    public string DeviceId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Profile { get; init; } = "stable";

    public DeviceStatus Status { get; init; }

    public DateTimeOffset LastSeenUtc { get; init; }

    public string? LastKnownIp { get; init; }

    public int? LastKnownRssi { get; init; }

    public string? FirmwareVersion { get; init; }

    public bool IsConnected => Status == DeviceStatus.Online;
}
