using System.Numerics;
using Analyzer.Dsp.Math;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#responsabilidades
internal sealed class SpectrumPowerProcessor
{
    private readonly WeightingFilter weightingFilter;
    private readonly float[] weightingPowerMultipliers;
    private readonly float fftSmoothing;
    private readonly float levelCompression;
    private readonly float[] smoothedSpectrum;

    private bool hasSmoothedSpectrum;

    public SpectrumPowerProcessor(AnalyzerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        weightingFilter = config.WeightingFilter;
        weightingPowerMultipliers = WeightingCurve.BuildPowerMultipliers(config.FftSize, config.SampleRate, config.WeightingFilter);
        fftSmoothing = global::System.Math.Clamp(config.FftSmoothing, 0f, 0.99f);
        levelCompression = config.LevelCompression;
        smoothedSpectrum = new float[(config.FftSize / 2) + 1];
    }

    public float[] BuildPowerSpectrum(Complex[] fftBuffer)
    {
        FftUtility.Forward(fftBuffer);
        var powerSpectrum = FftUtility.PowerSpectrum(fftBuffer);
        ApplyFftSmoothing(powerSpectrum);
        ApplyWeighting(powerSpectrum);
        return powerSpectrum;
    }

    public float ComputeLevel(float[] powerSpectrum)
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
