using System.Collections.Concurrent;
using System.Numerics;

namespace Analyzer.Dsp.Math;

// DOCS: docs/wiki/modules/analyzer-dsp.md#otimizacao-2026-03---fase-dsp-1-buffers-planos-e-output-only
internal sealed class ComplexFftPlan
{
    private static readonly ConcurrentDictionary<int, ComplexFftPlan> Cache = new();

    private readonly int[] bitReversal;
    private readonly Complex[][] twiddlesByStage;

    private ComplexFftPlan(int size)
    {
        Size = size;
        bitReversal = BuildBitReversal(size);
        twiddlesByStage = BuildTwiddles(size);
    }

    public int Size { get; }

    public static ComplexFftPlan ForSize(int size)
    {
        return Cache.GetOrAdd(size, static value => new ComplexFftPlan(value));
    }

    public void Forward(Span<Complex> buffer)
    {
        if (buffer.Length != Size)
        {
            throw new ArgumentException("FFT buffer length does not match the cached plan size.", nameof(buffer));
        }

        for (var i = 0; i < buffer.Length; i++)
        {
            var j = bitReversal[i];
            if (j > i)
            {
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        for (var stage = 0; stage < twiddlesByStage.Length; stage++)
        {
            var len = 1 << (stage + 1);
            var half = len >> 1;
            var twiddles = twiddlesByStage[stage];

            for (var offset = 0; offset < buffer.Length; offset += len)
            {
                for (var j = 0; j < half; j++)
                {
                    var u = buffer[offset + j];
                    var v = buffer[offset + j + half] * twiddles[j];
                    buffer[offset + j] = u + v;
                    buffer[offset + j + half] = u - v;
                }
            }
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

    private static Complex[][] BuildTwiddles(int size)
    {
        var stages = (int)global::System.Math.Log2(size);
        var table = new Complex[stages][];

        for (var stage = 0; stage < stages; stage++)
        {
            var len = 1 << (stage + 1);
            var half = len >> 1;
            var angle = -2.0 * global::System.Math.PI / len;
            var stageTwiddles = new Complex[half];

            for (var index = 0; index < half; index++)
            {
                var theta = angle * index;
                stageTwiddles[index] = new Complex(global::System.Math.Cos(theta), global::System.Math.Sin(theta));
            }

            table[stage] = stageTwiddles;
        }

        return table;
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
