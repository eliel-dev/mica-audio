using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Audio.Loopback.Capture;

internal sealed class LowLatencyWasapiLoopbackCapture : WasapiCapture
{
    public LowLatencyWasapiLoopbackCapture(MMDevice device, int latencyMilliseconds)
        : base(device, useEventSync: true, latencyMilliseconds)
    {
    }

    protected override AudioClientStreamFlags GetAudioClientStreamFlags()
    {
        return base.GetAudioClientStreamFlags() | AudioClientStreamFlags.Loopback;
    }
}
