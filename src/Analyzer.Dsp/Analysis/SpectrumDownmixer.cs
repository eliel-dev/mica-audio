namespace Analyzer.Dsp.Analysis;

public static class SpectrumDownmixer
{
    public static float[] DownmixTo64(float[] displayBands, float[]? direct64 = null)
    {
        if (direct64 is { Length: 64 })
        {
            var copy = new float[64];
            Array.Copy(direct64, copy, 64);
            return copy;
        }

        if (displayBands.Length == 128)
        {
            var output = new float[64];
            for (var i = 0; i < output.Length; i++)
            {
                var a = displayBands[i * 2];
                var b = displayBands[(i * 2) + 1];
                output[i] = MathF.Sqrt(((a * a) + (b * b)) * 0.5f);
            }

            return output;
        }

        throw new ArgumentException("Display band count must be 128 when direct 64-band data is not provided.", nameof(displayBands));
    }
}
