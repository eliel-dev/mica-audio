using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Tests;

public class FftPlanParityTests
{
    private const float MeanTolerance = 0.01f;
    private const float MaxTolerance = 0.03f;
    private const float LevelTolerance = 0.02f;
    private static readonly float[] Frequencies = [40f, 160f, 440f, 1000f, 4000f];
    private static readonly FrequencyScale[] Scales = [FrequencyScale.Logarithmic, FrequencyScale.Mel, FrequencyScale.Bark];

    [Fact]
    public void FloatBackend_ShouldMatchComplexBackend_ForPureTonesAndMultiTone()
    {
        var signals = new List<float[]>();
        signals.AddRange(Frequencies.Select(CreateToneBuffer));
        signals.Add(CreateMultiToneBuffer());
        signals.Add(new float[2048]);

        foreach (var scale in Scales)
        {
            foreach (var signal in signals)
            {
                var complexFrame = Analyze(signal, scale, SpectrumFftBackendKind.Complex64);
                var floatFrame = Analyze(signal, scale, SpectrumFftBackendKind.RealFloat);
                AssertFramesWithinTolerance(complexFrame!, floatFrame!);
            }
        }
    }

    [Fact]
    public void FloatBackend_ShouldMatchComplexBackend_AfterSignalStops()
    {
        foreach (var scale in Scales)
        {
            var complexAnalyzer = CreateAnalyzer(scale, SpectrumFftBackendKind.Complex64, fftSmoothing: 0.75f);
            var floatAnalyzer = CreateAnalyzer(scale, SpectrumFftBackendKind.RealFloat, fftSmoothing: 0.75f);
            var tone = CreateToneBuffer(440f);
            var silence = new float[2048];

            _ = complexAnalyzer.Process(new PcmFrame(tone, 1));
            _ = floatAnalyzer.Process(new PcmFrame(tone, 1));

            var complexFrame = complexAnalyzer.Process(new PcmFrame(silence, 2));
            var floatFrame = floatAnalyzer.Process(new PcmFrame(silence, 2));

            AssertFramesWithinTolerance(complexFrame!, floatFrame!);
        }
    }

    private static SpectrumFrame? Analyze(float[] samples, FrequencyScale scale, SpectrumFftBackendKind backendKind)
    {
        var analyzer = CreateAnalyzer(scale, backendKind, fftSmoothing: 0f);
        return analyzer.Process(new PcmFrame(samples, 1));
    }

    private static SpectrumAnalyzer CreateAnalyzer(FrequencyScale scale, SpectrumFftBackendKind backendKind, float fftSmoothing)
    {
        return new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = 2048,
            HopSize = 2048,
            DisplayBandCount = 38,
            OutputBandCount = 64,
            MinHz = 20f,
            MaxHz = 4000f,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = scale,
            FftSmoothing = fftSmoothing,
            WeightingFilter = WeightingFilter.B,
            DisplaySmoothingRise = 1f,
            DisplaySmoothingFall = 1f,
            DisplayMotionDamping = 1f,
            OutputSmoothingRise = 1f,
            OutputSmoothingFall = 1f,
            OutputMotionDamping = 1f,
        }, backendKind);
    }

    private static float[] CreateToneBuffer(float frequencyHz)
    {
        var samples = new float[2048];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.80f * MathF.Sin(2f * MathF.PI * frequencyHz * i / 48_000f);
        }

        return samples;
    }

    private static float[] CreateMultiToneBuffer()
    {
        var samples = new float[2048];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / 48_000f;
            samples[i] =
                (0.25f * MathF.Sin(2f * MathF.PI * 40f * t)) +
                (0.20f * MathF.Sin(2f * MathF.PI * 160f * t)) +
                (0.18f * MathF.Sin(2f * MathF.PI * 440f * t)) +
                (0.15f * MathF.Sin(2f * MathF.PI * 1000f * t)) +
                (0.12f * MathF.Sin(2f * MathF.PI * 4000f * t));
        }

        return samples;
    }

    private static void AssertFramesWithinTolerance(SpectrumFrame expected, SpectrumFrame actual)
    {
        Assert.Equal(expected.BandsDisplay.Length, actual.BandsDisplay.Length);
        Assert.Equal(expected.Bands64.Length, actual.Bands64.Length);

        Assert.InRange(ComputeMeanDifference(expected.BandsDisplay, actual.BandsDisplay), 0f, MeanTolerance);
        Assert.InRange(ComputeMeanDifference(expected.Bands64, actual.Bands64), 0f, MeanTolerance);
        Assert.InRange(ComputeMaxDifference(expected.BandsDisplay, actual.BandsDisplay), 0f, MaxTolerance);
        Assert.InRange(ComputeMaxDifference(expected.Bands64, actual.Bands64), 0f, MaxTolerance);
        Assert.InRange(MathF.Abs(expected.Level - actual.Level), 0f, LevelTolerance);
    }

    private static float ComputeMeanDifference(float[] left, float[] right)
    {
        if (left.Length == 0)
        {
            return 0f;
        }

        var sum = 0f;
        for (var i = 0; i < left.Length; i++)
        {
            sum += MathF.Abs(left[i] - right[i]);
        }

        return sum / left.Length;
    }

    private static float ComputeMaxDifference(float[] left, float[] right)
    {
        var max = 0f;
        for (var i = 0; i < left.Length; i++)
        {
            max = MathF.Max(max, MathF.Abs(left[i] - right[i]));
        }

        return max;
    }
}
