namespace Device.Protocol.Models;

// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#campos-do-payload-de-telemetria-ws
// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#shadow-retained-de-sessao
// DOCS: docs/handoffs/2026-04-17-firmware-control-worker-hardening.md
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public sealed class DeviceTelemetryMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public int? Rssi { get; init; }

    public int? UptimeSeconds { get; init; }

    public int? LoopHealthyPercent { get; init; }

    public int? LoopLoadPercent { get; init; }

    public double? ChipTemperatureCelsius { get; init; }

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

    public uint? Hub75PresentFrames { get; init; }

    public uint? StreamSequenceGapCount { get; init; }

    public uint? StreamInvalidFrameCount { get; init; }

    public string? ResetReason { get; init; }

    public uint? ControlQueueDepth { get; init; }

    public string? ControlWorkerState { get; init; }

    public string? PanelsWorkerState { get; init; }

    public string? LastSlowCommand { get; init; }

    public long? LastSlowCommandDurationMs { get; init; }

    public string? FirmwareVersion { get; init; }

    public uint? TelemetrySequence { get; init; }

    public int? BrightnessCap { get; init; }

    public int? BrightnessRequested { get; init; }

    public int? BrightnessApplied { get; init; }

    public bool? TestLedEnabled { get; init; }

    public int? TestLedDuty { get; init; }

    public string? IpAddress { get; init; }

    public string? ActiveAppId { get; init; }

    public string? ActiveAppName { get; init; }

    public string? BoardModel { get; init; }

    public string? PanelType { get; init; }

    public bool? AnimatedWebpBatchSupported { get; init; }

    public bool? VisualUdpSupported { get; init; }

    public int? VisualUdpPort { get; init; }

    public string? VisualUdpMode { get; init; }

    public string? SessionMode { get; init; }

    public string? SessionActiveClientId { get; init; }

    public uint? SessionActiveOwnerEpoch { get; init; }

    public int? SessionOwnerLeaseRemainingMs { get; init; }

    public bool? SessionLockHeld { get; init; }

    public string? SessionLockClientId { get; init; }

    public string? SessionLockReason { get; init; }

    public int? SessionLockLeaseRemainingMs { get; init; }

    public string? SessionFallbackState { get; init; }
}
