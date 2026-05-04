using Audio.Loopback.Capture;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
internal static class AudioPipelineCaptureProfile
{
    public static CaptureConfig CreateDefault()
    {
        return new CaptureConfig
        {
            TargetSampleRate = 48_000,
            ChannelCapacity = 8,
            BufferMilliseconds = 12,
        };
    }
}
