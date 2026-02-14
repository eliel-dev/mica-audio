using MicaAudio.Core.Audio;

namespace Analyzer.Dsp.Analysis;

internal static class WeightingCurve
{
    public static float[] BuildPowerMultipliers(int fftSize, int sampleRate, WeightingFilter filter)
    {
        var half = fftSize / 2;
        var multipliers = new float[half + 1];
        Array.Fill(multipliers, 1f);

        if (filter == WeightingFilter.Off)
        {
            return multipliers;
        }

        for (var bin = 1; bin <= half; bin++)
        {
            var frequency = (double)bin * sampleRate / fftSize;
            var weightingDb = ComputeWeightingDb(frequency, filter);

            // audioMotion adds the weighting in dB domain. We convert to power multiplier.
            var amplitudeMultiplier = global::System.Math.Pow(10d, weightingDb / 20d);
            var powerMultiplier = amplitudeMultiplier * amplitudeMultiplier;
            multipliers[bin] = (float)global::System.Math.Clamp(powerMultiplier, 1e-12d, 1e12d);
        }

        return multipliers;
    }

    private static double ComputeWeightingDb(double frequency, WeightingFilter filter)
    {
        if (frequency <= 0d)
        {
            return 0d;
        }

        var f2 = frequency * frequency;
        const double c1 = 424.36d;
        const double c2 = 148_693_636d;

        return filter switch
        {
            WeightingFilter.A => 2d + ToDb(
                c2 * f2 * f2 /
                ((f2 + c1) * global::System.Math.Sqrt((f2 + 11_599.29d) * (f2 + 544_496.41d)) * (f2 + c2))),

            WeightingFilter.B => 0.17d + ToDb(
                c2 * f2 * frequency /
                ((f2 + c1) * global::System.Math.Sqrt(f2 + 25_122.25d) * (f2 + c2))),

            WeightingFilter.C => 0.06d + ToDb(
                c2 * f2 /
                ((f2 + c1) * (f2 + c2))),

            WeightingFilter.D => ComputeDWeightingDb(frequency, f2),

            WeightingFilter.Filter468 => Compute468WeightingDb(frequency, f2),

            _ => 0d,
        };
    }

    private static double ComputeDWeightingDb(double frequency, double f2)
    {
        var r = (((1_037_918.48d - f2) * (1_037_918.48d - f2)) + (1_080_768.16d * f2)) /
                (((9_837_328d - f2) * (9_837_328d - f2)) + (11_723_776d * f2));

        return ToDb(
            (frequency / 6.8966888496476e-5d) *
            global::System.Math.Sqrt(r / ((f2 + 79_919.29d) * (f2 + 1_345_600d))));
    }

    private static double Compute468WeightingDb(double frequency, double f2)
    {
        var f3 = f2 * frequency;
        var f4 = f2 * f2;
        var f5 = f4 * frequency;
        var f6 = f3 * f3;

        var n = (-4.737338981378384e-24d * f6) +
                (2.043828333606125e-15d * f4) -
                (1.363894795463638e-7d * f2) +
                1d;

        var o = (1.306612257412824e-19d * f5) -
                (2.118150887518656e-11d * f3) +
                (5.559488023498642e-4d * frequency);

        return 18.2d + ToDb(
            1.246332637532143e-4d * frequency /
            global::System.Math.Sqrt((n * n) + (o * o)));
    }

    private static double ToDb(double amplitudeLikeValue)
        => 20d * global::System.Math.Log10(global::System.Math.Max(amplitudeLikeValue, 1e-30d));
}
