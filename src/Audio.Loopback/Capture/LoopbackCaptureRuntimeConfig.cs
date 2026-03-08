namespace Audio.Loopback.Capture;

// DOCS: docs/wiki/modules/audio-loopback.md#responsabilidades
internal sealed class LoopbackCaptureRuntimeConfig
{
    private LoopbackCaptureRuntimeConfig(CaptureConfig source, int channelCapacity, int bufferMilliseconds)
    {
        Source = source;
        ChannelCapacity = channelCapacity;
        BufferMilliseconds = bufferMilliseconds;
        TargetSampleRate = source.TargetSampleRate;
        TargetChannels = source.TargetChannels;
    }

    public CaptureConfig Source { get; }

    public int ChannelCapacity { get; }

    public int BufferMilliseconds { get; }

    public int TargetSampleRate { get; }

    public int TargetChannels { get; }

    public static LoopbackCaptureRuntimeConfig From(CaptureConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new LoopbackCaptureRuntimeConfig(
            config,
            channelCapacity: global::System.Math.Max(2, config.ChannelCapacity),
            bufferMilliseconds: global::System.Math.Clamp(config.BufferMilliseconds, 8, 20));
    }
}
