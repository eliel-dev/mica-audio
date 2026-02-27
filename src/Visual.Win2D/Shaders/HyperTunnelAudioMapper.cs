using MicaAudio.Core.Audio;

namespace Visual.Win2D.Shaders;

// DOCS: docs/wiki/modules/visual-win2d.md#hyper-tunnel-shader-gpu
internal static class HyperTunnelAudioMapper
{
    public static HyperTunnelAudioState Map(in SpectrumFrame frame)
    {
        var bands = frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return new HyperTunnelAudioState(0f, 0f, 0f, 0f, 0f, 0f);
        }

        var bass = ComputeBandEnergy(bands, 0f, 0.20f);
        var mid = ComputeBandEnergy(bands, 0.20f, 0.65f);
        var high = ComputeBandEnergy(bands, 0.65f, 1f);

        var band32 = SampleNormalizedBand(bands, 0.25f);
        var band128 = SampleNormalizedBand(bands, 1f);

        return new HyperTunnelAudioState(
            Bass: bass,
            Mid: mid,
            High: high,
            Level: Math.Clamp(frame.Level, 0f, 1.5f),
            Band32: band32,
            Band128: band128);
    }

    private static float ComputeBandEnergy(float[] bands, float startRatio, float endRatio)
    {
        if (bands.Length == 0)
        {
            return 0f;
        }

        var start = Math.Clamp((int)(bands.Length * startRatio), 0, bands.Length - 1);
        var endExclusive = Math.Clamp((int)MathF.Ceiling(bands.Length * endRatio), start + 1, bands.Length);

        var sum = 0f;
        var count = 0;
        for (var i = start; i < endExclusive; i++)
        {
            sum += Math.Clamp(bands[i], 0f, 1f);
            count++;
        }

        return count > 0 ? (sum / count) : 0f;
    }

    private static float SampleNormalizedBand(float[] bands, float normalizedPosition)
    {
        if (bands.Length == 0)
        {
            return 0f;
        }

        var clampedPosition = Math.Clamp(normalizedPosition, 0f, 1f);
        var index = Math.Clamp((int)MathF.Round(clampedPosition * (bands.Length - 1)), 0, bands.Length - 1);

        return Math.Clamp(bands[index], 0f, 1f);
    }
}

internal readonly record struct HyperTunnelAudioState(
    float Bass,
    float Mid,
    float High,
    float Level,
    float Band32,
    float Band128);
