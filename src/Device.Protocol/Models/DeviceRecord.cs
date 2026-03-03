namespace Device.Protocol.Models;

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

    public int? LoopLoadPercent { get; init; }

    public long? FreeHeapBytes { get; init; }

    public long? LargestHeapBlockBytes { get; init; }

    public bool? PsramAvailable { get; init; }

    public long? FreePsramBytes { get; init; }

    public long? LargestPsramBlockBytes { get; init; }

    public bool? WifiConnected { get; init; }

    public uint? StreamLastSequence { get; init; }

    public uint? StreamFramesReceived { get; init; }

    public uint? StreamFramesApplied { get; init; }

    public uint? StreamSequenceGapCount { get; init; }

    public uint? StreamInvalidFrameCount { get; init; }

    public string? ActiveAppId { get; init; }

    public string? ActiveAppName { get; init; }

    public string? BoardModel { get; init; }

    public string? PanelType { get; init; }
}
