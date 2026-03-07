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
    private readonly SpectrumBandLayout bandLayout;
    private readonly EnvelopeSmoother displaySmoother;
    private readonly EnvelopeSmoother outputSmoother;
    private readonly SpectrumPowerProcessor powerProcessor;

    private readonly SpectrumSampleWindow sampleWindow;

    public SpectrumAnalyzer(AnalyzerConfig config)
    {
        Validate(config);
        this.config = config;

        hannWindow = FftUtility.BuildHannWindow(config.FftSize);
        bandLayout = new SpectrumBandLayout(config);
        displaySmoother = new EnvelopeSmoother(bandLayout.DisplayRanges.Length, config.DisplaySmoothingRise, config.DisplaySmoothingFall, config.DisplayMotionDamping);
        outputSmoother = new EnvelopeSmoother(config.OutputBandCount, config.OutputSmoothingRise, config.OutputSmoothingFall, config.OutputMotionDamping);
        powerProcessor = new SpectrumPowerProcessor(config);
        sampleWindow = new SpectrumSampleWindow(config.FftSize);
    }

    public SpectrumFrame? Process(in PcmFrame frame)
    {
        // DOCS: docs/wiki/modules/analyzer-dsp.md#fluxo-de-execucao
        if (frame.SamplesMono.Length == 0)
        {
            return null;
        }

        sampleWindow.Append(frame.SamplesMono);

        SpectrumFrame? latest = null;
        while (sampleWindow.SampleCount >= config.FftSize)
        {
            latest = AnalyzeCurrentWindow(frame.TimestampQpc);
            sampleWindow.Slide(config.HopSize);
        }

        return latest;
    }

    private SpectrumFrame AnalyzeCurrentWindow(long timestampQpc)
    {
        var fftBuffer = new Complex[config.FftSize];
        var windowedSamples = new float[config.FftSize];
        sampleWindow.CopyWindowTo(windowedSamples, config.InputGain, hannWindow);

        for (var i = 0; i < config.FftSize; i++)
        {
            fftBuffer[i] = new Complex(windowedSamples[i], 0d);
        }

        var powerSpectrum = powerProcessor.BuildPowerSpectrum(fftBuffer);

        var useDb = config.ScaleMode == ScaleMode.Db;
        var minDecibels = config.MinDecibels;
        var maxDecibels = config.MaxDecibels;
        var displayRaw = LogBandMapper.AggregateBandsPeak(
            powerSpectrum,
            bandLayout.DisplayRanges,
            minDecibels,
            maxDecibels,
            useDb,
            config.UseLinearAmplitude,
            config.LinearBoost);
        var outputRaw = LogBandMapper.AggregateBandsRms(
            powerSpectrum,
            bandLayout.OutputRanges,
            minDecibels,
            maxDecibels,
            useDb,
            config.UseLinearAmplitude,
            config.LinearBoost);

        var displaySmooth = displaySmoother.Process(displayRaw);
        var outputSmooth = outputSmoother.Process(outputRaw);
        var level = powerProcessor.ComputeLevel(powerSpectrum);

        return new SpectrumFrame(displaySmooth, outputSmooth, level, timestampQpc, bandLayout.DisplayBarX, bandLayout.DisplayBarWidth);
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
