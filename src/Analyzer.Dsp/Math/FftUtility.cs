using System.Numerics;

namespace Analyzer.Dsp.Math;

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
        var n = buffer.Length;
        var bits = (int)global::System.Math.Log2(n);

        for (var i = 0; i < n; i++)
        {
            var j = ReverseBits(i, bits);
            if (j > i)
            {
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = -2.0 * global::System.Math.PI / len;
            var wLen = new Complex(global::System.Math.Cos(angle), global::System.Math.Sin(angle));

            for (var i = 0; i < n; i += len)
            {
                var w = Complex.One;
                var half = len >> 1;
                for (var j = 0; j < half; j++)
                {
                    var u = buffer[i + j];
                    var v = buffer[i + j + half] * w;
                    buffer[i + j] = u + v;
                    buffer[i + j + half] = u - v;
                    w *= wLen;
                }
            }
        }
    }

    public static float[] PowerSpectrum(Complex[] fftBuffer)
    {
        var half = fftBuffer.Length / 2;
        var normalization = 1f / (fftBuffer.Length * fftBuffer.Length);
        var spectrum = new float[half + 1];
        for (var i = 0; i <= half; i++)
        {
            var c = fftBuffer[i];
            spectrum[i] = (float)(((c.Real * c.Real) + (c.Imaginary * c.Imaginary)) * normalization);
        }

        return spectrum;
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
