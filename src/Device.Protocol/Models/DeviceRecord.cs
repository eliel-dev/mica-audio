namespace Device.Protocol.Models;

// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#persistencia-local
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public sealed class DeviceRecord
{
    public string DeviceId { get; init; } = string.Empty;

    public string Name { get; init; } = "Matrix Portal";

    public string Profile { get; init; } = "dma_exp";

    public string Token { get; init; } = string.Empty;

    public bool IsRegistered { get; init; } = true;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenUtc { get; init; } = DateTimeOffset.MinValue;

    public DateTimeOffset? FirstSeenUtc { get; init; }

    public DateTimeOffset? LastTelemetryUtc { get; init; }

    public DateTimeOffset? LastAuthUtc { get; init; }

    public DeviceConfigState ConfigState { get; init; } = DeviceConfigState.Unknown;

    public string? FirmwareVersion { get; init; }

    public string? LastKnownIp { get; init; }

    public int? LastKnownRssi { get; init; }

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

    public bool? AnimatedWebpBatchSupported { get; init; }

    public bool? VisualUdpSupported { get; init; }

    public int? VisualUdpPort { get; init; }

    public string? VisualUdpMode { get; init; }

    public string? ChipModel { get; init; }

    public int? ChipRevision { get; init; }

    public int? ChipCores { get; init; }

    public int? CpuFreqMHz { get; init; }

    public string? SdkVersion { get; init; }

    public long? HeapTotalBytes { get; init; }

    public long? PsramTotalBytes { get; init; }

    public long? FlashTotalBytes { get; init; }

    public long? SketchSizeBytes { get; init; }

    public long? FreeSketchBytes { get; init; }
}
