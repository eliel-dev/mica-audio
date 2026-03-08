using System.Numerics;

namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#fluxo-de-execucao
internal sealed class SpectrumSampleWindow
{
    private float[] sampleBuffer;
    private int head;
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
        var writeIndex = (head + sampleCount) % sampleBuffer.Length;
        var firstSegment = global::System.Math.Min(sampleBuffer.Length - writeIndex, samples.Length);
        samples[..firstSegment].CopyTo(sampleBuffer.AsSpan(writeIndex, firstSegment));

        if (firstSegment < samples.Length)
        {
            samples[firstSegment..].CopyTo(sampleBuffer);
        }

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
            destination[i] = GetSampleAt(i) * inputGain * hannWindow[i];
        }
    }

    public void CopyWindowTo(Span<Complex> destination, float inputGain, ReadOnlySpan<float> hannWindow)
    {
        if (destination.Length > sampleCount)
        {
            throw new ArgumentException("Destination window exceeds buffered samples.", nameof(destination));
        }

        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = new Complex(GetSampleAt(i) * inputGain * hannWindow[i], 0d);
        }
    }

    public void Slide(int hopSize)
    {
        Advance(hopSize);
    }

    public void Advance(int hopSize)
    {
        var shift = global::System.Math.Min(hopSize, sampleCount);
        sampleCount -= shift;
        if (sampleCount == 0)
        {
            head = 0;
            return;
        }

        head = (head + shift) % sampleBuffer.Length;
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
        CopyLinearizedSamplesTo(resized);
        sampleBuffer = resized;
        head = 0;
    }

    private void CopyLinearizedSamplesTo(Span<float> destination)
    {
        if (sampleCount == 0)
        {
            return;
        }

        var firstSegment = global::System.Math.Min(sampleBuffer.Length - head, sampleCount);
        sampleBuffer.AsSpan(head, firstSegment).CopyTo(destination);

        if (firstSegment < sampleCount)
        {
            sampleBuffer.AsSpan(0, sampleCount - firstSegment).CopyTo(destination[firstSegment..]);
        }
    }

    private float GetSampleAt(int offset)
    {
        return sampleBuffer[(head + offset) % sampleBuffer.Length];
    }
}
