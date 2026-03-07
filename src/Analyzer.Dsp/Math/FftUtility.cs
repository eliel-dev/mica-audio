using System.Numerics;

namespace Analyzer.Dsp.Math;

// DOCS: docs/wiki/modules/analyzer-dsp.md#otimizacao-2026-03---fase-dsp-1-buffers-planos-e-output-only
internal static class FftUtility
{
    public static float[] BuildHannWindow(int size)
    {
        var window = new float[size];
        for (var i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos((2f * MathF.PI * i) / (size - 1)));
        }

        return window;
    }

    public static void Forward(Complex[] buffer)
    {
        ComplexFftPlan.ForSize(buffer.Length).Forward(buffer);
    }

    public static float[] PowerSpectrum(Complex[] fftBuffer)
    {
        var half = fftBuffer.Length / 2;
        var spectrum = new float[half + 1];
        PowerSpectrum(fftBuffer, spectrum);
        return spectrum;
    }

    public static void PowerSpectrum(ReadOnlySpan<Complex> fftBuffer, Span<float> destination)
    {
        var expectedLength = (fftBuffer.Length / 2) + 1;
        if (destination.Length != expectedLength)
        {
            throw new ArgumentException("Power spectrum destination length does not match FFT size.", nameof(destination));
        }

        var normalization = 1f / (fftBuffer.Length * fftBuffer.Length);
        for (var i = 0; i < destination.Length; i++)
        {
            var value = fftBuffer[i];
            destination[i] = (float)(((value.Real * value.Real) + (value.Imaginary * value.Imaginary)) * normalization);
        }
    }
}
