using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Tests;

public class SmootherAndAnalyzerTests
{
    [Fact]
    public void EnvelopeSmoother_ShouldDecayGradually()
    {
        var smoother = new EnvelopeSmoother(1, rise: 1f, fall: 0.2f);

        var buffer = new float[1];
        smoother.Process([1f], buffer);
        smoother.Process([0f], buffer);
        var next = buffer;

        Assert.InRange(next[0], 0.15f, 0.85f);
    }

    [Fact]
    public void SpectrumAnalyzer_ShouldEmitDisplayAnd64Bands()
    {
        var analyzer = new SpectrumAnalyzer(new AnalyzerConfig
        {
            FftSize = 2048,
            HopSize = 512,
            DisplayBandCount = 128,
            OutputBandCount = 64,
            SampleRate = 48_000,
        });

        var samples = new float[2048];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = MathF.Sin(2f * MathF.PI * 440f * i / 48_000f);
        }

        var frame = analyzer.Process(new PcmFrame(samples, 123));

        Assert.NotNull(frame);
        Assert.Equal(128, frame!.BandsDisplay.Length);
        Assert.Equal(64, frame.Bands64.Length);
        Assert.InRange(frame.Level, 0f, 1f);
    }

    [Fact]
    public void SpectrumAnalyzer_ShouldPreserveHeadroomBetweenSignalLevels()
    {
        var lowFrame = AnalyzeSine(0.20f);
        var highFrame = AnalyzeSine(0.85f);

        Assert.NotNull(lowFrame);
        Assert.NotNull(highFrame);

        var lowPeak = lowFrame!.BandsDisplay.Max();
        var highPeak = highFrame!.BandsDisplay.Max();

        Assert.True(highPeak > lowPeak + 0.02f);
    }

    private static SpectrumFrame? AnalyzeSine(float amplitude)
    {
        var analyzer = new SpectrumAnalyzer(new AnalyzerConfig
        {
            FftSize = 2048,
            HopSize = 512,
            DisplayBandCount = 128,
            OutputBandCount = 64,
            SampleRate = 48_000,
            MinDecibels = -100f,
            MaxDecibels = -5f,
            InputGain = 1f,
        });

        SpectrumFrame? latest = null;
        var samples = new float[2048];
        for (var repeat = 0; repeat < 4; repeat++)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = amplitude * MathF.Sin(2f * MathF.PI * 440f * i / 48_000f);
            }

            latest = analyzer.Process(new PcmFrame(samples, repeat));
        }

        return latest;
    }

    [Fact]
    public void SpectrumAnalyzer_DifferentFrequencyScales_ShouldProduceDifferentBandProfiles()
    {
        var sampleRate = 48_000;
        var samples = new float[4096];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (float)sampleRate;
            samples[i] =
                (0.20f * MathF.Sin(2f * MathF.PI * 90f * t)) +
                (0.18f * MathF.Sin(2f * MathF.PI * 440f * t)) +
                (0.15f * MathF.Sin(2f * MathF.PI * 1200f * t)) +
                (0.12f * MathF.Sin(2f * MathF.PI * 3000f * t)) +
                (0.10f * MathF.Sin(2f * MathF.PI * 8000f * t));
        }

        var log = AnalyzeWithScale(samples, FrequencyScale.Logarithmic);
        var mel = AnalyzeWithScale(samples, FrequencyScale.Mel);
        var bark = AnalyzeWithScale(samples, FrequencyScale.Bark);

        Assert.NotNull(log);
        Assert.NotNull(mel);
        Assert.NotNull(bark);

        var logVsMel = AverageAbsoluteDifference(log!.BandsDisplay, mel!.BandsDisplay);
        var logVsBark = AverageAbsoluteDifference(log.BandsDisplay, bark!.BandsDisplay);

        Assert.True(logVsMel > 0.005f);
        Assert.True(logVsBark > 0.005f);
    }

    private static SpectrumFrame? AnalyzeWithScale(float[] samples, FrequencyScale frequencyScale)
    {
        var analyzer = new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = 2048,
            HopSize = 256,
            DisplayBandCount = 38,
            OutputBandCount = 64,
            MinHz = 30f,
            MaxHz = 16_000f,
            ScaleMode = ScaleMode.Db,
            FrequencyScale = frequencyScale,
        });

        SpectrumFrame? frame = null;
        for (var i = 0; i < 6; i++)
        {
            frame = analyzer.Process(new PcmFrame(samples, i));
        }

        return frame;
    }

    [Fact]
    public void SpectrumAnalyzer_FftSmoothing_ShouldKeepMoreEnergyAfterSignalStops()
    {
        var noSmoothing = CreateSimpleAnalyzer(fftSmoothing: 0f, WeightingFilter.Off);
        var withSmoothing = CreateSimpleAnalyzer(fftSmoothing: 0.8f, WeightingFilter.Off);

        var tone = CreateSineBuffer(440f, 0.8f, 4096, 48_000);
        var silence = new float[4096];

        _ = noSmoothing.Process(new PcmFrame(tone, 1));
        var noSmoothAfterStop = noSmoothing.Process(new PcmFrame(silence, 2));

        _ = withSmoothing.Process(new PcmFrame(tone, 1));
        var smoothAfterStop = withSmoothing.Process(new PcmFrame(silence, 2));

        Assert.NotNull(noSmoothAfterStop);
        Assert.NotNull(smoothAfterStop);

        var noSmoothPeak = noSmoothAfterStop!.BandsDisplay.Max();
        var smoothPeak = smoothAfterStop!.BandsDisplay.Max();

        Assert.True(smoothPeak > noSmoothPeak + 0.02f);
    }

    [Fact]
    public void SpectrumAnalyzer_AWeighting_ShouldAttenuateLowFrequencyMoreThanHighFrequency()
    {
        var offLow = AnalyzeTonePeak(100f, WeightingFilter.Off);
        var aLow = AnalyzeTonePeak(100f, WeightingFilter.A);
        var offHigh = AnalyzeTonePeak(4000f, WeightingFilter.Off);
        var aHigh = AnalyzeTonePeak(4000f, WeightingFilter.A);

        Assert.True(offLow > 0f);
        Assert.True(offHigh > 0f);

        var lowRatio = aLow / offLow;
        var highRatio = aHigh / offHigh;

        Assert.True(lowRatio < highRatio);
        Assert.True(lowRatio < 0.5f);
    }

    [Fact]
    public void SpectrumAnalyzer_BWeighting_ShouldAttenuateLowFrequencyMoreThanMidFrequency()
    {
        var offLow = AnalyzeTonePeak(40f, WeightingFilter.Off);
        var bLow = AnalyzeTonePeak(40f, WeightingFilter.B);
        var offMid = AnalyzeTonePeak(1000f, WeightingFilter.Off);
        var bMid = AnalyzeTonePeak(1000f, WeightingFilter.B);

        Assert.True(offLow > 0f);
        Assert.True(offMid > 0f);

        var lowRatio = bLow / offLow;
        var midRatio = bMid / offMid;
        Assert.True(lowRatio < midRatio);
    }

    [Fact]
    public void SpectrumAnalyzer_FixedHop256_ShouldHaveSteadyCadenceFor2k()
    {
        const int chunkSize = 256;
        const int totalChunks = 80;
        var analyzer2k = new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = 2048,
            HopSize = 256,
            DisplayBandCount = 38,
            OutputBandCount = 64,
        });

        var first2k = -1;
        var steady2k = 0;

        for (var i = 0; i < totalChunks; i++)
        {
            var chunk = CreateSineBuffer(440f, 0.8f, chunkSize, 48_000);

            var frame2k = analyzer2k.Process(new PcmFrame(chunk, i));
            if (frame2k is not null)
            {
                if (first2k < 0)
                {
                    first2k = i;
                }

                if (i >= 60)
                {
                    steady2k++;
                }
            }
        }

        Assert.Equal(7, first2k);
        Assert.Equal(20, steady2k);
    }

    [Fact]
    public void SpectrumAnalyzer_MinMaxDbSensitivity_ShouldChangeAmplitudeWithNeutralInputGain()
    {
        var lowSensitivity = new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = 2048,
            HopSize = 256,
            DisplayBandCount = 38,
            OutputBandCount = 64,
            MinHz = 20f,
            MaxHz = 1000f,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = FrequencyScale.Bark,
            FftSmoothing = 0.75f,
            WeightingFilter = WeightingFilter.B,
            UseLinearAmplitude = true,
            MinDecibels = -110f,
            MaxDecibels = -10f,
            InputGain = 1f,
        });
        var highSensitivity = new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = 2048,
            HopSize = 256,
            DisplayBandCount = 38,
            OutputBandCount = 64,
            MinHz = 20f,
            MaxHz = 1000f,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = FrequencyScale.Bark,
            FftSmoothing = 0.75f,
            WeightingFilter = WeightingFilter.B,
            UseLinearAmplitude = true,
            MinDecibels = -90f,
            MaxDecibels = -30f,
            InputGain = 1f,
        });

        SpectrumFrame? low = null;
        SpectrumFrame? high = null;
        for (var i = 0; i < 12; i++)
        {
            var tone = CreateSineBuffer(160f, 0.50f, 256, 48_000);
            low = lowSensitivity.Process(new PcmFrame(tone, i));
            high = highSensitivity.Process(new PcmFrame(tone, i));
        }

        Assert.NotNull(low);
        Assert.NotNull(high);
        Assert.True(high!.BandsDisplay.Max() > low!.BandsDisplay.Max());
    }

    [Fact]
    public void SpectrumAnalyzer_LinearBoost_ShouldIncreaseOutputInLinearScale()
    {
        var spectrum = new float[] { 0f, 1e-5f };
        var ranges = new[] { new BandRange(1, 2, 1f, 2f) };

        var withoutBoost = LogBandMapper.AggregateBandsPeak(
            spectrum,
            ranges,
            dbFloor: -100f,
            dbCeiling: -30f,
            useDb: false,
            useLinearAmplitude: true,
            linearBoost: 1f);
        var withBoost = LogBandMapper.AggregateBandsPeak(
            spectrum,
            ranges,
            dbFloor: -100f,
            dbCeiling: -30f,
            useDb: false,
            useLinearAmplitude: true,
            linearBoost: 2.4f);

        Assert.True(withBoost[0] > withoutBoost[0]);
    }

    private static float AnalyzeTonePeak(float frequencyHz, WeightingFilter weightingFilter)
    {
        var analyzer = CreateSimpleAnalyzer(fftSmoothing: 0f, weightingFilter);
        var tone = CreateSineBuffer(frequencyHz, 0.8f, 4096, 48_000);
        var frame = analyzer.Process(new PcmFrame(tone, 1));
        Assert.NotNull(frame);
        return frame!.BandsDisplay.Max();
    }

    private static SpectrumAnalyzer CreateSimpleAnalyzer(float fftSmoothing, WeightingFilter weightingFilter)
    {
        return new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = 2048,
            HopSize = 2048,
            DisplayBandCount = 64,
            OutputBandCount = 64,
            MinHz = 30f,
            MaxHz = 16_000f,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = FrequencyScale.Logarithmic,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            DisplaySmoothingRise = 1f,
            DisplaySmoothingFall = 1f,
            DisplayMotionDamping = 1f,
            OutputSmoothingRise = 1f,
            OutputSmoothingFall = 1f,
            OutputMotionDamping = 1f,
        });
    }

    private static float[] CreateSineBuffer(float frequencyHz, float amplitude, int sampleCount, int sampleRate)
    {
        var buffer = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            buffer[i] = amplitude * MathF.Sin(2f * MathF.PI * frequencyHz * i / sampleRate);
        }

        return buffer;
    }

    private static float AverageAbsoluteDifference(float[] left, float[] right)
    {
        var length = global::System.Math.Min(left.Length, right.Length);
        if (length == 0)
        {
            return 0f;
        }

        var sum = 0f;
        for (var i = 0; i < length; i++)
        {
            sum += MathF.Abs(left[i] - right[i]);
        }

        return sum / length;
    }
}
