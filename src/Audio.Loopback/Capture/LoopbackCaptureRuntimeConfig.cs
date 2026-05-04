namespace Audio.Loopback.Capture;

// DOCS: docs/wiki/modules/audio-loopback.md#responsabilidades
internal sealed class LoopbackCaptureRuntimeConfig
{
    private LoopbackCaptureRuntimeConfig(int channelCapacity, int bufferMilliseconds, int targetSampleRate)
    {
        ChannelCapacity = channelCapacity;
        BufferMilliseconds = bufferMilliseconds;
        TargetSampleRate = targetSampleRate;
    }

    public int ChannelCapacity { get; }

    public int BufferMilliseconds { get; }

    public int TargetSampleRate { get; }

    public static LoopbackCaptureRuntimeConfig From(CaptureConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new LoopbackCaptureRuntimeConfig(
            channelCapacity: global::System.Math.Max(2, config.ChannelCapacity),
            bufferMilliseconds: global::System.Math.Clamp(config.BufferMilliseconds, 8, 20),
            targetSampleRate: config.TargetSampleRate);
    }
}
