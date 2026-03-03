namespace Device.Protocol.Models;

public sealed class DeviceSnapshot
{
    public string DeviceId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Profile { get; init; } = "dma_exp";

    public DeviceStatus Status { get; init; }

    public bool IsRegistered { get; init; }

    public DateTimeOffset LastSeenUtc { get; init; }

    public DateTimeOffset? FirstSeenUtc { get; init; }

    public DateTimeOffset? LastTelemetryUtc { get; init; }

    public DateTimeOffset? LastAuthUtc { get; init; }

    public DeviceConfigState ConfigState { get; init; }

    public string? LastKnownIp { get; init; }

    public int? LastKnownRssi { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? ActiveAppId { get; init; }

    public string? ActiveAppName { get; init; }

    public string? BoardModel { get; init; }

    public string? PanelType { get; init; }

    public bool IsConnected => Status == DeviceStatus.Online;
}
