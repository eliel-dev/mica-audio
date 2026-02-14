namespace Audio.Loopback.Capture;

public sealed class CaptureConfig
{
    public int TargetSampleRate { get; init; } = 48_000;

    public int TargetChannels { get; init; } = 1;

    public int ChannelCapacity { get; init; } = 8;

    // Event-driven WASAPI latency hint. Values outside [8, 20] are clamped.
    public int BufferMilliseconds { get; init; } = 12;
}
