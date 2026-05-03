namespace Device.Protocol.Models;

// DOCS: docs/wiki/reference/device-observability-dashboard.md
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
public sealed class DeviceTelemetryResponse
{
    public string DeviceId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool Online { get; init; }

    public string? ActiveAppName { get; init; }

    public string? WifiState { get; init; }

    public int? SignalDbm { get; init; }

    public int? UptimeSeconds { get; init; }

    public uint? TelemetrySequence { get; init; }

    public bool? TestLedAvailable { get; init; }

    public bool? TestLedEnabled { get; init; }

    public int? TestLedDuty { get; init; }

    public string? FirmwareVersion { get; init; }

    public int? BrightnessCap { get; init; }

    public int? BrightnessRequested { get; init; }

    public int? BrightnessApplied { get; init; }

    public int? LoopHealthyPercent { get; init; }

    public int? LoopLoadPercent { get; init; }

    public double? ChipTemperatureCelsius { get; init; }

    public long? HeapFreeBytes { get; init; }

    public long? HeapLargestBlockBytes { get; init; }

    public long? HeapTotalBytes { get; init; }

    public int? HeapFreePercent { get; init; }

    public int? HeapFragmentationPercent { get; init; }

    public bool? PsramAvailable { get; init; }

    public long? PsramFreeBytes { get; init; }

    public long? PsramLargestBlockBytes { get; init; }

    public long? PsramTotalBytes { get; init; }

    public int? PsramFreePercent { get; init; }

    public uint? StreamFramesReceived { get; init; }

    public uint? StreamFramesApplied { get; init; }

    public uint? StreamSequenceGapCount { get; init; }

    public uint? StreamInvalidFrameCount { get; init; }

    public uint? StreamLastSequence { get; init; }

    public int? Hub75Fps { get; init; }

    public string? LatestFirmwareVersion { get; init; }

    public bool FirmwareUpdateAvailable { get; init; }

    public bool FirmwareUpdateSupported { get; init; }
}
