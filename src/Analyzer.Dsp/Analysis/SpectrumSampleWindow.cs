namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#fluxo-de-execucao
internal sealed class SpectrumSampleWindow
{
    private float[] sampleBuffer;
    private int sampleCount;

    public SpectrumSampleWindow(int fftSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fftSize, 1);
        sampleBuffer = new float[fftSize * 2];
    }

    public int SampleCount => sampleCount;

    public void Append(ReadOnlySpan<float> samples)
    {
        EnsureCapacity(sampleCount + samples.Length);
        samples.CopyTo(sampleBuffer.AsSpan(sampleCount));
        sampleCount += samples.Length;
    }

    public void CopyWindowTo(Span<float> destination, float inputGain, ReadOnlySpan<float> hannWindow)
    {
        if (destination.Length > sampleCount)
        {
            throw new ArgumentException("Destination window exceeds buffered samples.", nameof(destination));
        }

        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = sampleBuffer[i] * inputGain * hannWindow[i];
        }
    }

    public void Slide(int hopSize)
    {
        var shift = global::System.Math.Min(hopSize, sampleCount);
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
}
