using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Output.Tests;

public class LedOutputLifecycleTests
{
    [Fact]
    public void NullLedOutput_ShouldAcceptLifecycleCalls()
    {
        var output = new NullLedOutput();

        output.Start(new LedOutputConfig { Width = 128, Height = 64, Brightness = 1f });
        output.Send(new LedPayload { Bins128 = Enumerable.Repeat(1f, 128).ToArray(), Level = 1f });
        output.SetBrightness(0.5f);
        output.Stop();
    }

    [Fact]
    public void SimulatorLedOutput_ShouldAcceptLifecycleCallsAndExposeFrameSnapshot()
    {
        var output = new SimulatorLedOutput();

        output.Start(new LedOutputConfig { Width = 128, Height = 64, Brightness = 1f });
        output.SetBrightness(0.5f);
        output.Send(new LedPayload
        {
            Frame128x64 = CreateFrame(),
            Level = 0.5f,
        });

        var snapshot = output.GetFrameSnapshot();

        Assert.Equal(128 * 64, snapshot.Length);
        Assert.NotEqual(default, snapshot[0]);
        output.Stop();
    }

    [Fact]
    public void Esp32Output_ShouldRespectStartStopAndRestartLifecycle()
    {
        var host = new FakeDeviceFrameTransport();
        var output = new Esp32S3LedOutput(host);
        var payload = new LedPayload
        {
            Frame128x64 = CreateFrame(),
            Level = 0.5f,
        };

        output.Send(payload);
        Assert.Empty(host.BroadcastFrames);

        output.Start(new LedOutputConfig { Width = 128, Height = 64, Brightness = 1f });
        output.Send(payload);
        Assert.Single(host.BroadcastFrames);

        output.Stop();
        output.Send(payload);
        Assert.Single(host.BroadcastFrames);

        output.Start(new LedOutputConfig { Width = 128, Height = 64, Brightness = 1f });
        output.Send(payload);
        Assert.Equal(2, host.BroadcastFrames.Count);
    }

    private static RgbaColor[] CreateFrame()
    {
        var frame = new RgbaColor[128 * 64];
        frame[0] = new RgbaColor(255, 0, 0, 255);
        return frame;
    }

    private sealed class FakeDeviceFrameTransport : IDeviceFrameTransport
    {
        public List<byte[]> BroadcastFrames { get; } = new();

        public void SendFrame(string deviceId, byte[] framePayload) => BroadcastFrames.Add(framePayload);
        public void BroadcastFrame(byte[] framePayload) => BroadcastFrames.Add(framePayload);
    }
}
