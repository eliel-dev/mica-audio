namespace Device.Protocol.Models;

public sealed class DeviceTelemetryMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public int? Rssi { get; init; }

    public int? UptimeSeconds { get; init; }

    public int? LoopLoadPercent { get; init; }

    public long? FreeHeapBytes { get; init; }

    public long? LargestHeapBlockBytes { get; init; }

    public bool? PsramAvailable { get; init; }

    public long? FreePsramBytes { get; init; }

    public long? LargestPsramBlockBytes { get; init; }

    public bool? WifiConnected { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? IpAddress { get; init; }

    public string? ActiveAppId { get; init; }

    public string? ActiveAppName { get; init; }

    public string? BoardModel { get; init; }

    public string? PanelType { get; init; }
}
