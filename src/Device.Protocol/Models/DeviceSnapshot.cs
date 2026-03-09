namespace Device.Protocol.Models;

public sealed class DeviceSnapshot
{
    public string DeviceId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Profile { get; init; } = "dma_exp";

    public DeviceStatus Status { get; init; }

    public DeviceControlPlaneState ControlPlaneState { get; init; }

    public bool IsRegistered { get; init; }

    public DateTimeOffset LastSeenUtc { get; init; }

    public DateTimeOffset? FirstSeenUtc { get; init; }

    public DateTimeOffset? LastTelemetryUtc { get; init; }

    public DateTimeOffset? LastAuthUtc { get; init; }

    public DeviceConfigState ConfigState { get; init; }

    public string? LastKnownIp { get; init; }

    public int? LastKnownRssi { get; init; }

    public int? UptimeSeconds { get; init; }

    public int? LoopLoadPercent { get; init; }

    public long? FreeHeapBytes { get; init; }

    public long? LargestHeapBlockBytes { get; init; }

    public bool? PsramAvailable { get; init; }

    public long? FreePsramBytes { get; init; }

    public long? LargestPsramBlockBytes { get; init; }

    public bool? WifiConnected { get; init; }

    public string? WifiState { get; init; }

    public bool? ProvisioningPortalActive { get; init; }

    public bool? AuxLedAvailable { get; init; }

    public bool? TestLedAvailable { get; init; }

    public string? LastWifiEvent { get; init; }

    public uint? StreamLastSequence { get; init; }

    public uint? StreamFramesReceived { get; init; }

    public uint? StreamFramesApplied { get; init; }

    public uint? StreamSequenceGapCount { get; init; }

    public uint? StreamInvalidFrameCount { get; init; }

    public string? FirmwareVersion { get; init; }

    public uint? TelemetrySequence { get; init; }

    public int? BrightnessCap { get; init; }

    public int? BrightnessRequested { get; init; }

    public int? BrightnessApplied { get; init; }

    public bool? TestLedEnabled { get; init; }

    public int? TestLedDuty { get; init; }

    public string? ActiveAppId { get; init; }

    public string? ActiveAppName { get; init; }

    public string? BoardModel { get; init; }

    public string? PanelType { get; init; }

    public bool IsConnected => ControlPlaneState == DeviceControlPlaneState.MqttOnline;
}
