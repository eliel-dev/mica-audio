using System.Numerics;
using Analyzer.Dsp.Math;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#modulo-analyzerdsp
public sealed class SpectrumAnalyzer : IAnalyzer
{
    private readonly AnalyzerConfig config;
    private readonly SpectrumFftBackendKind fftBackendKind;
    private readonly float[] hannWindow;
    private readonly SpectrumBandLayout bandLayout;
    private readonly EnvelopeSmoother? displaySmoother;
    private readonly EnvelopeSmoother outputSmoother;
    private readonly SpectrumPowerProcessor powerProcessor;
    private readonly SpectrumSampleWindow sampleWindow;
    private readonly Complex[] complexFftBuffer;
    private readonly float[] realFftBuffer;
    private readonly float[] powerSpectrum;
    private readonly float[] displayRaw;
    private readonly float[] outputRaw;

    public SpectrumAnalyzer(AnalyzerConfig config)
        : this(config, SpectrumFftBackendKind.Complex64)
    {
    }

    internal SpectrumAnalyzer(AnalyzerConfig config, SpectrumFftBackendKind fftBackendKind)
    {
        Validate(config);
        this.config = config;
        this.fftBackendKind = fftBackendKind;

        hannWindow = FftUtility.BuildHannWindow(config.FftSize);
        bandLayout = new SpectrumBandLayout(config);
        displaySmoother = config.OutputMode == AnalyzerOutputMode.OutputOnly
            ? null
            : new EnvelopeSmoother(bandLayout.DisplayAggregationRanges.Length, config.DisplaySmoothingRise, config.DisplaySmoothingFall, config.DisplayMotionDamping);
        outputSmoother = new EnvelopeSmoother(config.OutputBandCount, config.OutputSmoothingRise, config.OutputSmoothingFall, config.OutputMotionDamping);
        powerProcessor = new SpectrumPowerProcessor(config, fftBackendKind);
        sampleWindow = new SpectrumSampleWindow(config.FftSize);
        complexFftBuffer = fftBackendKind == SpectrumFftBackendKind.Complex64 ? new Complex[config.FftSize] : Array.Empty<Complex>();
        realFftBuffer = fftBackendKind == SpectrumFftBackendKind.RealFloat ? new float[config.FftSize] : Array.Empty<float>();
        powerSpectrum = new float[(config.FftSize / 2) + 1];
        displayRaw = new float[bandLayout.DisplayAggregationRanges.Length];
        outputRaw = new float[bandLayout.OutputAggregationRanges.Length];
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
            sampleWindow.Advance(config.HopSize);
        }

        return latest;
    }

    private SpectrumFrame AnalyzeCurrentWindow(long timestampQpc)
    {
        if (fftBackendKind == SpectrumFftBackendKind.RealFloat)
        {
            sampleWindow.CopyWindowTo(realFftBuffer, config.InputGain, hannWindow);
            powerProcessor.BuildPowerSpectrum(realFftBuffer, powerSpectrum);
        }
        else
        {
            sampleWindow.CopyWindowTo(complexFftBuffer, config.InputGain, hannWindow);
            powerProcessor.BuildPowerSpectrum(complexFftBuffer, powerSpectrum);
        }

        var useDb = config.ScaleMode == ScaleMode.Db;
        var minDecibels = config.MinDecibels;
        var maxDecibels = config.MaxDecibels;
        LogBandMapper.AggregateBandsRms(
            powerSpectrum,
            bandLayout.OutputAggregationRanges,
            minDecibels,
            maxDecibels,
            useDb,
            config.UseLinearAmplitude,
            config.LinearBoost,
            outputRaw);

        var outputSmooth = GC.AllocateUninitializedArray<float>(outputRaw.Length);
        outputSmoother.Process(outputRaw, outputSmooth);
        var level = powerProcessor.ComputeLevel(powerSpectrum);

        if (config.OutputMode == AnalyzerOutputMode.OutputOnly)
        {
            return new SpectrumFrame(Array.Empty<float>(), outputSmooth, level, timestampQpc, null, null);
        }

        LogBandMapper.AggregateBandsPeak(
            powerSpectrum,
            bandLayout.DisplayAggregationRanges,
            minDecibels,
            maxDecibels,
            useDb,
            config.UseLinearAmplitude,
            config.LinearBoost,
            displayRaw);

        var displaySmooth = GC.AllocateUninitializedArray<float>(displayRaw.Length);
        displaySmoother!.Process(displayRaw, displaySmooth);

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
