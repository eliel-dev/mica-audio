using System.Collections.Concurrent;

namespace Analyzer.Dsp.Math;

// DOCS: docs/wiki/modules/analyzer-dsp.md#otimizacao-2026-03---fase-dsp-1-buffers-planos-e-output-only
internal sealed class RealFftFloatPlan
{
    private static readonly ConcurrentDictionary<int, RealFftFloatPlan> Cache = new();

    private readonly int[] bitReversal;
    private readonly float[][] cosByStage;
    private readonly float[][] sinByStage;

    private RealFftFloatPlan(int size)
    {
        Size = size;
        bitReversal = BuildBitReversal(size);
        (cosByStage, sinByStage) = BuildTwiddles(size);
    }

    public int Size { get; }

    public static RealFftFloatPlan ForSize(int size)
    {
        return Cache.GetOrAdd(size, static value => new RealFftFloatPlan(value));
    }

    public void TransformRealToPowerSpectrum(
        ReadOnlySpan<float> input,
        Span<float> realBuffer,
        Span<float> imaginaryBuffer,
        Span<float> powerSpectrum)
    {
        if (input.Length != Size)
        {
            throw new ArgumentException("Input length does not match the cached plan size.", nameof(input));
        }

        if (realBuffer.Length != Size || imaginaryBuffer.Length != Size)
        {
            throw new ArgumentException("FFT scratch buffers must match the cached plan size.");
        }

        if (powerSpectrum.Length != ((Size / 2) + 1))
        {
            throw new ArgumentException("Power spectrum buffer length does not match the cached plan size.", nameof(powerSpectrum));
        }

        input.CopyTo(realBuffer);
        imaginaryBuffer.Clear();

        for (var i = 0; i < Size; i++)
        {
            var j = bitReversal[i];
            if (j <= i)
            {
                continue;
            }

            (realBuffer[i], realBuffer[j]) = (realBuffer[j], realBuffer[i]);
            (imaginaryBuffer[i], imaginaryBuffer[j]) = (imaginaryBuffer[j], imaginaryBuffer[i]);
        }

        for (var stage = 0; stage < cosByStage.Length; stage++)
        {
            var len = 1 << (stage + 1);
            var half = len >> 1;
            var cos = cosByStage[stage];
            var sin = sinByStage[stage];

            for (var offset = 0; offset < Size; offset += len)
            {
                for (var j = 0; j < half; j++)
                {
                    var evenIndex = offset + j;
                    var oddIndex = evenIndex + half;
                    var oddReal = realBuffer[oddIndex];
                    var oddImaginary = imaginaryBuffer[oddIndex];

                    var twiddleReal = cos[j];
                    var twiddleImaginary = sin[j];
                    var vReal = (oddReal * twiddleReal) - (oddImaginary * twiddleImaginary);
                    var vImaginary = (oddReal * twiddleImaginary) + (oddImaginary * twiddleReal);

                    var uReal = realBuffer[evenIndex];
                    var uImaginary = imaginaryBuffer[evenIndex];

                    realBuffer[evenIndex] = uReal + vReal;
                    imaginaryBuffer[evenIndex] = uImaginary + vImaginary;
                    realBuffer[oddIndex] = uReal - vReal;
                    imaginaryBuffer[oddIndex] = uImaginary - vImaginary;
                }
            }
        }

        var normalization = 1f / (Size * Size);
        for (var index = 0; index < powerSpectrum.Length; index++)
        {
            var real = realBuffer[index];
            var imaginary = imaginaryBuffer[index];
            powerSpectrum[index] = ((real * real) + (imaginary * imaginary)) * normalization;
        }
    }

    private static int[] BuildBitReversal(int size)
    {
        var bits = (int)global::System.Math.Log2(size);
        var table = new int[size];

        for (var i = 0; i < size; i++)
        {
            table[i] = ReverseBits(i, bits);
        }

        return table;
    }

    private static (float[][] Cos, float[][] Sin) BuildTwiddles(int size)
    {
        var stages = (int)global::System.Math.Log2(size);
        var cos = new float[stages][];
        var sin = new float[stages][];

        for (var stage = 0; stage < stages; stage++)
        {
            var len = 1 << (stage + 1);
            var half = len >> 1;
            var angle = -2f * MathF.PI / len;
            var stageCos = new float[half];
            var stageSin = new float[half];

            for (var index = 0; index < half; index++)
            {
                var theta = angle * index;
                stageCos[index] = MathF.Cos(theta);
                stageSin[index] = MathF.Sin(theta);
            }

            cos[stage] = stageCos;
            sin[stage] = stageSin;
        }

        return (cos, sin);
    }

    private static int ReverseBits(int value, int bits)
    {
        var reversed = 0;
        for (var i = 0; i < bits; i++)
        {
            reversed = (reversed << 1) | (value & 1);
            value >>= 1;
        }

        return reversed;
    }
}
