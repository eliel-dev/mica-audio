using System.Threading.Channels;
using MicaAudio.Core.Audio;

namespace Audio.Loopback.Capture;

public interface ILoopbackCapture : IAsyncDisposable
{
    ChannelReader<PcmFrame> Frames { get; }

    event EventHandler<CaptureStatusChangedEventArgs>? StatusChanged;

    Task StartAsync(CaptureConfig config, CancellationToken cancellationToken = default);

    Task StopAsync();
}
