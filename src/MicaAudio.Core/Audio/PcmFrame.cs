namespace MicaAudio.Core.Audio;

public sealed class PcmFrame
{
    public PcmFrame(float[] samplesMono, long timestampQpc)
    {
        SamplesMono = samplesMono;
        TimestampQpc = timestampQpc;
    }

    public float[] SamplesMono { get; }

    public long TimestampQpc { get; }
}
