namespace MicaAudio.Core.Audio;

public sealed class SpectrumFrame
{
    public SpectrumFrame(float[] bandsDisplay, float[] bands64, float level, long timestampQpc)
        : this(bandsDisplay, bands64, level, timestampQpc, null, null)
    {
    }

    public SpectrumFrame(
        float[] bandsDisplay,
        float[] bands64,
        float level,
        long timestampQpc,
        float[]? displayBarX,
        float[]? displayBarWidth)
    {
        BandsDisplay = bandsDisplay;
        Bands64 = bands64;
        Level = level;
        TimestampQpc = timestampQpc;
        DisplayBarX = displayBarX;
        DisplayBarWidth = displayBarWidth;
    }

    public float[] BandsDisplay { get; }

    public float[] Bands64 { get; }

    public float Level { get; }

    public long TimestampQpc { get; }

    // Optional normalized geometry for mode-specific renderers (0..1 space).
    public float[]? DisplayBarX { get; }

    public float[]? DisplayBarWidth { get; }
}
