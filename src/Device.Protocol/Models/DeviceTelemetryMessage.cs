namespace Device.Protocol.Models;

public sealed class DeviceTelemetryMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public int? Rssi { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? IpAddress { get; init; }

    public string? ActiveAppId { get; init; }

    public string? ActiveAppName { get; init; }
}
