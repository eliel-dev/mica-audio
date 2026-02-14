using System.Buffers.Binary;
using NAudio.Wave;

namespace Audio.Loopback.Capture;

internal static class PcmConversion
{
    public static float[] DecodeToFloat(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        if (format.BitsPerSample == 32 && format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var sampleCount = bytesRecorded / 4;
            var output = new float[sampleCount];
            Buffer.BlockCopy(buffer, 0, output, 0, bytesRecorded);
            return output;
        }

        if (format.BitsPerSample == 16)
        {
            var sampleCount = bytesRecorded / 2;
            var output = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var raw = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i * 2, 2));
                output[i] = raw / 32768f;
            }

            return output;
        }

        if (format.BitsPerSample == 24)
        {
            var sampleCount = bytesRecorded / 3;
            var output = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var offset = i * 3;
                var sample = (buffer[offset + 2] << 24) | (buffer[offset + 1] << 16) | (buffer[offset] << 8);
                output[i] = sample / (float)int.MaxValue;
            }

            return output;
        }

        if (format.BitsPerSample == 32)
        {
            var sampleCount = bytesRecorded / 4;
            var output = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var raw = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(i * 4, 4));
                output[i] = raw / (float)int.MaxValue;
            }

            return output;
        }

        return Array.Empty<float>();
    }

    public static float[] DownmixToMono(float[] samples, int channels)
    {
        if (channels <= 1)
        {
            return samples;
        }

        var frameCount = samples.Length / channels;
        var mono = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            var baseIndex = frame * channels;
            for (var c = 0; c < channels; c++)
            {
                sum += samples[baseIndex + c];
            }

            mono[frame] = sum / channels;
        }

        return mono;
    }

    public static float[] ResampleLinear(float[] samples, int inputRate, int outputRate)
    {
        if (samples.Length == 0)
        {
            return samples;
        }

        if (inputRate == outputRate)
        {
            return samples;
        }

        var ratio = inputRate / (double)outputRate;
        var outputLength = (int)Math.Floor(samples.Length / ratio);
        if (outputLength < 1)
        {
            return Array.Empty<float>();
        }

        var output = new float[outputLength];
        for (var i = 0; i < outputLength; i++)
        {
            var sourceIndex = i * ratio;
            var index0 = (int)sourceIndex;
            var index1 = Math.Min(index0 + 1, samples.Length - 1);
            var frac = sourceIndex - index0;
            output[i] = (float)(samples[index0] + ((samples[index1] - samples[index0]) * frac));
        }

        return output;
    }
}
