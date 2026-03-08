using System.Diagnostics;
using MicaAudio.Core.Audio;
using NAudio.Wave;

namespace Audio.Loopback.Capture;

// DOCS: docs/wiki/modules/audio-loopback.md#fluxo-de-execucao
internal static class LoopbackFrameFactory
{
    public static PcmFrame? TryCreateFrame(byte[] buffer, int bytesRecorded, WaveFormat waveFormat, int targetSampleRate)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(waveFormat);

        if (bytesRecorded <= 0)
        {
            return null;
        }

        var sourceSamples = PcmConversion.DecodeToFloat(buffer, bytesRecorded, waveFormat);
        if (sourceSamples.Length == 0)
        {
            return null;
        }

        var mono = PcmConversion.DownmixToMono(sourceSamples, waveFormat.Channels);
        var monoResampled = PcmConversion.ResampleLinear(mono, waveFormat.SampleRate, targetSampleRate);
        if (monoResampled.Length == 0)
        {
            return null;
        }

        return new PcmFrame(monoResampled, Stopwatch.GetTimestamp());
    }
}
