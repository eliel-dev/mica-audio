using System.Numerics;
using Analyzer.Dsp.Math;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#responsabilidades
internal sealed class SpectrumPowerProcessor
{
    private readonly SpectrumFftBackendKind backendKind;
    private readonly ComplexFftPlan? complexPlan;
    private readonly RealFftFloatPlan? realPlan;
    private readonly WeightingFilter weightingFilter;
    private readonly float[] weightingPowerMultipliers;
    private readonly float fftSmoothing;
    private readonly float levelCompression;
    private readonly float[] realScratch;
    private readonly float[] imaginaryScratch;
    private readonly float[] smoothedSpectrum;

    private bool hasSmoothedSpectrum;

    public SpectrumPowerProcessor(AnalyzerConfig config)
        : this(config, SpectrumFftBackendKind.Complex64)
    {
    }

    internal SpectrumPowerProcessor(AnalyzerConfig config, SpectrumFftBackendKind backendKind)
    {
        ArgumentNullException.ThrowIfNull(config);

        this.backendKind = backendKind;
        complexPlan = backendKind == SpectrumFftBackendKind.Complex64 ? ComplexFftPlan.ForSize(config.FftSize) : null;
        realPlan = backendKind == SpectrumFftBackendKind.RealFloat ? RealFftFloatPlan.ForSize(config.FftSize) : null;
        weightingFilter = config.WeightingFilter;
        weightingPowerMultipliers = WeightingCurve.BuildPowerMultipliers(config.FftSize, config.SampleRate, config.WeightingFilter);
        fftSmoothing = global::System.Math.Clamp(config.FftSmoothing, 0f, 0.99f);
        levelCompression = config.LevelCompression;
        realScratch = backendKind == SpectrumFftBackendKind.RealFloat ? new float[config.FftSize] : Array.Empty<float>();
        imaginaryScratch = backendKind == SpectrumFftBackendKind.RealFloat ? new float[config.FftSize] : Array.Empty<float>();
        smoothedSpectrum = new float[(config.FftSize / 2) + 1];
    }

    public float[] BuildPowerSpectrum(Complex[] fftBuffer)
    {
        var output = new float[(fftBuffer.Length / 2) + 1];
        BuildPowerSpectrum(fftBuffer, output);
        return output;
    }

    public void BuildPowerSpectrum(Span<Complex> fftBuffer, Span<float> powerSpectrum)
    {
        if (backendKind != SpectrumFftBackendKind.Complex64)
        {
            throw new InvalidOperationException("Complex FFT buffers are not supported by the selected float backend.");
        }

        complexPlan!.Forward(fftBuffer);
        FftUtility.PowerSpectrum(fftBuffer, powerSpectrum);
        ApplyFftSmoothing(powerSpectrum);
        ApplyWeighting(powerSpectrum);
    }

    public void BuildPowerSpectrum(ReadOnlySpan<float> fftInput, Span<float> powerSpectrum)
    {
        if (backendKind != SpectrumFftBackendKind.RealFloat)
        {
            throw new InvalidOperationException("Float FFT input is only supported by the float backend.");
        }

        realPlan!.TransformRealToPowerSpectrum(fftInput, realScratch, imaginaryScratch, powerSpectrum);
        ApplyFftSmoothing(powerSpectrum);
        ApplyWeighting(powerSpectrum);
    }

    public float ComputeLevel(float[] powerSpectrum)
        => ComputeLevel(powerSpectrum.AsSpan());

    public float ComputeLevel(ReadOnlySpan<float> powerSpectrum)
    {
        var sum = 0f;
        for (var i = 1; i < powerSpectrum.Length; i++)
        {
            sum += powerSpectrum[i];
        }

        var rms = MathF.Sqrt(sum / global::System.Math.Max(1, powerSpectrum.Length - 1));
        var compressed = MathF.Pow(rms * levelCompression * 6f, 0.5f);
        return global::System.Math.Clamp(compressed, 0f, 1f);
    }

    private void ApplyFftSmoothing(Span<float> powerSpectrum)
    {
        if (fftSmoothing <= 0f)
        {
            return;
        }

        if (!hasSmoothedSpectrum)
        {
            powerSpectrum.CopyTo(smoothedSpectrum);
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

    private void ApplyWeighting(Span<float> powerSpectrum)
    {
        if (weightingFilter == WeightingFilter.Off)
        {
            return;
        }

        var max = global::System.Math.Min(powerSpectrum.Length, weightingPowerMultipliers.Length);
        for (var i = 1; i < max; i++)
        {
            powerSpectrum[i] *= weightingPowerMultipliers[i];
        }
    }
}
