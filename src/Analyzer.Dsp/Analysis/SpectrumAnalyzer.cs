using System.Numerics;
using Analyzer.Dsp.Math;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#modulo-analyzerdsp
public sealed class SpectrumAnalyzer : IAnalyzer
{
    private readonly AnalyzerConfig config;
    private readonly float[] hannWindow;
    private readonly BandRange[] displayRanges;
    private readonly float[]? displayBarX;
    private readonly float[]? displayBarWidth;
    private readonly BandRange[] outputRanges;
    private readonly EnvelopeSmoother displaySmoother;
    private readonly EnvelopeSmoother outputSmoother;
    private readonly float[] weightingPowerMultipliers;
    private readonly float fftSmoothing;
    private readonly float[] smoothedSpectrum;

    private float[] sampleBuffer;
    private int sampleCount;
    private bool hasSmoothedSpectrum;

    public SpectrumAnalyzer(AnalyzerConfig config)
    {
        Validate(config);
        this.config = config;

        hannWindow = FftUtility.BuildHannWindow(config.FftSize);
        if (config.DisplayMode == DisplayMode.AudioMotionMode0 && config.DisplayViewportWidthPx > 1f)
        {
            var mode0 = LogBandMapper.CreateMode0Ranges(
                config.FftSize,
                config.SampleRate,
                config.MinHz,
                config.MaxHz,
                config.FrequencyScale,
                config.DisplayViewportWidthPx,
                config.BarSpace);

            displayRanges = mode0.Ranges;
            displayBarX = mode0.BarX;
            displayBarWidth = mode0.BarWidth;
        }
        else
        {
            displayRanges = LogBandMapper.CreateRanges(
                config.DisplayBandCount,
                config.FftSize,
                config.SampleRate,
                config.MinHz,
                config.MaxHz,
                config.FrequencyScale);
            displayBarX = null;
            displayBarWidth = null;
        }

        outputRanges = LogBandMapper.CreateRanges(
            config.OutputBandCount,
            config.FftSize,
            config.SampleRate,
            config.MinHz,
            config.MaxHz,
            config.FrequencyScale);

        displaySmoother = new EnvelopeSmoother(displayRanges.Length, config.DisplaySmoothingRise, config.DisplaySmoothingFall, config.DisplayMotionDamping);
        outputSmoother = new EnvelopeSmoother(config.OutputBandCount, config.OutputSmoothingRise, config.OutputSmoothingFall, config.OutputMotionDamping);
        weightingPowerMultipliers = WeightingCurve.BuildPowerMultipliers(config.FftSize, config.SampleRate, config.WeightingFilter);
        fftSmoothing = global::System.Math.Clamp(config.FftSmoothing, 0f, 0.99f);
        smoothedSpectrum = new float[(config.FftSize / 2) + 1];

        sampleBuffer = new float[config.FftSize * 2];
    }

    public SpectrumFrame? Process(in PcmFrame frame)
    {
        // DOCS: docs/wiki/modules/analyzer-dsp.md#fluxo-de-execucao
        if (frame.SamplesMono.Length == 0)
        {
            return null;
        }

        AppendSamples(frame.SamplesMono);

        SpectrumFrame? latest = null;
        while (sampleCount >= config.FftSize)
        {
            latest = AnalyzeCurrentWindow(frame.TimestampQpc);
            SlideWindow();
        }

        return latest;
    }

    private SpectrumFrame AnalyzeCurrentWindow(long timestampQpc)
    {
        var fftBuffer = new Complex[config.FftSize];
        for (var i = 0; i < config.FftSize; i++)
        {
            var sample = sampleBuffer[i] * config.InputGain;
            fftBuffer[i] = new Complex(sample * hannWindow[i], 0d);
        }

        FftUtility.Forward(fftBuffer);
        var powerSpectrum = FftUtility.PowerSpectrum(fftBuffer);
        ApplyFftSmoothing(powerSpectrum);
        ApplyWeighting(powerSpectrum);

        var useDb = config.ScaleMode == ScaleMode.Db;
        var minDecibels = config.MinDecibels;
        var maxDecibels = config.MaxDecibels;
        var displayRaw = LogBandMapper.AggregateBandsPeak(
            powerSpectrum,
            displayRanges,
            minDecibels,
            maxDecibels,
            useDb,
            config.UseLinearAmplitude,
            config.LinearBoost);
        var outputRaw = LogBandMapper.AggregateBandsRms(
            powerSpectrum,
            outputRanges,
            minDecibels,
            maxDecibels,
            useDb,
            config.UseLinearAmplitude,
            config.LinearBoost);

        var displaySmooth = displaySmoother.Process(displayRaw);
        var outputSmooth = outputSmoother.Process(outputRaw);
        var level = ComputeLevel(powerSpectrum);

        return new SpectrumFrame(displaySmooth, outputSmooth, level, timestampQpc, displayBarX, displayBarWidth);
    }

    private float ComputeLevel(float[] powerSpectrum)
    {
        var sum = 0f;
        for (var i = 1; i < powerSpectrum.Length; i++)
        {
            sum += powerSpectrum[i];
        }

        var rms = MathF.Sqrt(sum / global::System.Math.Max(1, powerSpectrum.Length - 1));
        var compressed = MathF.Pow(rms * config.LevelCompression * 6f, 0.5f);
        return global::System.Math.Clamp(compressed, 0f, 1f);
    }

    private void ApplyFftSmoothing(float[] powerSpectrum)
    {
        if (fftSmoothing <= 0f)
        {
            return;
        }

        if (!hasSmoothedSpectrum)
        {
            Array.Copy(powerSpectrum, smoothedSpectrum, powerSpectrum.Length);
            hasSmoothedSpectrum = true;
            return;
        }

        var previousWeight = fftSmoothing;
        var currentWeight = 1f - previousWeight;
        for (var i = 0; i < powerSpectrum.Length; i++)
        {
            var smoothed = (smoothedSpectrum[i] * previousWeight) + (powerSpectrum[i] * currentWeight);
            smoothedSpectrum[i] = smoothed;
            powerSpectrum[i] = smoothed;
        }
    }

    private void ApplyWeighting(float[] powerSpectrum)
    {
        if (config.WeightingFilter == WeightingFilter.Off)
        {
            return;
        }

        var max = global::System.Math.Min(powerSpectrum.Length, weightingPowerMultipliers.Length);
        for (var i = 1; i < max; i++)
        {
            powerSpectrum[i] *= weightingPowerMultipliers[i];
        }
    }

    private void AppendSamples(float[] samples)
    {
        EnsureCapacity(sampleCount + samples.Length);
        Array.Copy(samples, 0, sampleBuffer, sampleCount, samples.Length);
        sampleCount += samples.Length;
    }

    private void SlideWindow()
    {
        var shift = global::System.Math.Min(config.HopSize, sampleCount);
        Array.Copy(sampleBuffer, shift, sampleBuffer, 0, sampleCount - shift);
        sampleCount -= shift;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= sampleBuffer.Length)
        {
            return;
        }

        var nextSize = sampleBuffer.Length;
        while (nextSize < required)
        {
            nextSize *= 2;
        }

        var resized = new float[nextSize];
        Array.Copy(sampleBuffer, resized, sampleCount);
        sampleBuffer = resized;
    }

    private static void Validate(AnalyzerConfig cfg)
    {
        if (cfg.FftSize < 512 || (cfg.FftSize & (cfg.FftSize - 1)) != 0)
        {
            throw new ArgumentException("FFT size must be a power of two and >= 512.", nameof(cfg));
        }

        if (cfg.HopSize <= 0 || cfg.HopSize > cfg.FftSize)
        {
            throw new ArgumentException("Hop size must be > 0 and <= FFT size.", nameof(cfg));
        }

        if (cfg.FftSmoothing < 0f || cfg.FftSmoothing >= 1f)
        {
            throw new ArgumentException("FftSmoothing must be >= 0 and < 1.", nameof(cfg));
        }

        if (cfg.DisplayBandCount is < 8 or > 256)
        {
            throw new ArgumentException("DisplayBandCount must be between 8 and 256.", nameof(cfg));
        }

        if (cfg.MaxDecibels <= cfg.MinDecibels)
        {
            throw new ArgumentException("MaxDecibels must be greater than MinDecibels.", nameof(cfg));
        }

        if (cfg.LinearBoost <= 0f || cfg.LinearBoost > 4f)
        {
            throw new ArgumentException("LinearBoost must be > 0 and <= 4.", nameof(cfg));
        }

        if (cfg.OutputBandCount != 64)
        {
            throw new ArgumentException("OutputBandCount must be 64.", nameof(cfg));
        }
    }
}
