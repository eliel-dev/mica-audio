using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;

namespace MicaAudio.Core.Led;

// DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
internal static class LedPayloadFactory
{
    public static LedPayload CreateSpectrumPayload(SpectrumFrame spectrum, string presetId, byte binsFlags = 0)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        return new LedPayload
        {
            Bins128 = ResampleSpectrumBins(spectrum),
            Level = spectrum.Level,
            PresetId = presetId,
            BinsFlags = binsFlags,
        };
    }

    public static LedPayload CreateBinsPayload(float[] bins128, string presetId, float level, byte binsFlags = 0)
    {
        ArgumentNullException.ThrowIfNull(bins128);

        return new LedPayload
        {
            Bins128 = bins128,
            Level = level,
            PresetId = presetId,
            BinsFlags = binsFlags,
        };
    }

    public static LedPayload CreateFramePayload(RgbaColor[] frame128x64, string presetId, float level = 1f)
    {
        ArgumentNullException.ThrowIfNull(frame128x64);

        return new LedPayload
        {
            Frame128x64 = frame128x64,
            Level = level,
            PresetId = presetId,
        };
    }

    private static float[] ResampleSpectrumBins(SpectrumFrame spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        if (spectrum.BandsDisplay.Length == LedDefaults.MatrixWidth)
        {
            return spectrum.BandsDisplay.ToArray();
        }

        var source = spectrum.BandsDisplay.Length > 0 ? spectrum.BandsDisplay : spectrum.Bands64;
        if (source.Length == LedDefaults.MatrixWidth)
        {
            return source.ToArray();
        }

        var bins = new float[LedDefaults.MatrixWidth];
        if (source.Length == 0)
        {
            return bins;
        }

        for (var index = 0; index < bins.Length; index++)
        {
            var t = bins.Length == 1 ? 0f : index / (float)(bins.Length - 1);
            var scaled = t * (source.Length - 1);
            var left = (int)MathF.Floor(scaled);
            var right = Math.Min(source.Length - 1, left + 1);
            var blend = scaled - left;
            bins[index] = (source[left] * (1f - blend)) + (source[right] * blend);
        }

        return bins;
    }
}
