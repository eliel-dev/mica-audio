using Device.Protocol.Models;
using Device.Protocol.Stream;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Output.Tests;

public class Esp32S3LedOutputTests
{
    [Fact]
    public void Send_ShouldBroadcastEncodedBins128Frame()
    {
        var host = new FakeDeviceServerHost();
        var output = new Esp32S3LedOutput(host);

        output.Start(new LedOutputConfig
        {
            Width = 128,
            Height = 64,
            Brightness = 0.5f,
        });

        var bins = Enumerable.Range(0, 128).Select(i => i / 127f).ToArray();
        output.Send(new LedPayload
        {
            Bins128 = bins,
            Level = 1f,
            PresetId = "audiomotion-clone",
        });

        Assert.Single(host.BroadcastFrames);
        var payload = host.BroadcastFrames[0];

        Assert.Equal(StreamFrameV2.PayloadSizeBins128, payload.Length);
        Assert.Equal(StreamFrameV2.Version, payload[0]);
        Assert.Equal(StreamFrameV2.MessageTypeBins128, payload[1]);
        Assert.Equal((byte)255, payload[14]);
        Assert.Equal((byte)0, payload[15]);
        Assert.Equal((byte)255, payload[142]);
        Assert.Equal((byte)128, payload[143]);
        Assert.Equal((byte)0, payload[144]);
    }

    [Fact]
    public void SetBrightness_ShouldAffectBrightnessByteInPayload()
    {
        var host = new FakeDeviceServerHost();
        var output = new Esp32S3LedOutput(host);

        output.Start(new LedOutputConfig { Width = 128, Height = 64, Brightness = 1f });
        output.SetBrightness(0f);

        output.Send(new LedPayload
        {
            Bins128 = Enumerable.Repeat(1f, 128).ToArray(),
            Level = 0.5f,
        });

        Assert.Single(host.BroadcastFrames);
        Assert.Equal((byte)0, host.BroadcastFrames[0][143]);
    }

    [Fact]
    public void Stop_ShouldPreventFurtherBroadcasts()
    {
        var host = new FakeDeviceServerHost();
        var output = new Esp32S3LedOutput(host);

        output.Start(new LedOutputConfig { Width = 128, Height = 64, Brightness = 1f });
        output.Stop();

        output.Send(new LedPayload
        {
            Bins128 = Enumerable.Repeat(1f, 128).ToArray(),
            Level = 1f,
        });

        Assert.Empty(host.BroadcastFrames);
    }

    [Fact]
    public void Send_WithFrame128x64_ShouldBroadcastRgb565FramePayload()
    {
        var host = new FakeDeviceServerHost();
        var output = new Esp32S3LedOutput(host);

        output.Start(new LedOutputConfig
        {
            Width = 128,
            Height = 64,
            Brightness = 1f,
        });

        var frame = new RgbaColor[128 * 64];
        frame[0] = new RgbaColor(255, 0, 0, 255);
        frame[1] = new RgbaColor(0, 255, 0, 255);
        frame[2] = new RgbaColor(0, 0, 255, 255);

        output.Send(new LedPayload
        {
            Frame128x64 = frame,
            Level = 0.5f,
            Bins128 = Enumerable.Repeat(0.2f, 128).ToArray(),
        });

        Assert.Single(host.BroadcastFrames);
        var payload = host.BroadcastFrames[0];
        Assert.Equal(StreamFrameV2.PayloadSizeFrame128x64Rgb565, payload.Length);
        Assert.Equal(StreamFrameV2.MessageTypeFrame128x64Rgb565, payload[1]);
        Assert.Equal((byte)255, payload[14]);

        Assert.Equal((byte)0x00, payload[15]);
        Assert.Equal((byte)0xF8, payload[16]);
        Assert.Equal((byte)0xE0, payload[17]);
        Assert.Equal((byte)0x07, payload[18]);
        Assert.Equal((byte)0x1F, payload[19]);
        Assert.Equal((byte)0x00, payload[20]);
        Assert.Equal((byte)0, payload[^1]);
    }

    [Fact]
    public void Send_WithUnchangedFrame_ShouldSkipConsecutiveBroadcast()
    {
        var host = new FakeDeviceServerHost();
        var output = new Esp32S3LedOutput(host);

        output.Start(new LedOutputConfig
        {
            Width = 128,
            Height = 64,
            Brightness = 1f,
        });

        var frame = new RgbaColor[128 * 64];
        frame[0] = new RgbaColor(255, 0, 0, 255);

        output.Send(new LedPayload { Frame128x64 = frame, Level = 1f });
        output.Send(new LedPayload { Frame128x64 = frame, Level = 1f });

        Assert.Single(host.BroadcastFrames);
    }

    [Fact]
    public void Send_WithUnchangedPixelsAndDifferentBrightness_ShouldBroadcastAgain()
    {
        var host = new FakeDeviceServerHost();
        var output = new Esp32S3LedOutput(host);

        output.Start(new LedOutputConfig
        {
            Width = 128,
            Height = 64,
            Brightness = 1f,
        });

        var frame = new RgbaColor[128 * 64];
        frame[0] = new RgbaColor(255, 0, 0, 255);

        output.Send(new LedPayload { Frame128x64 = frame, Level = 1f });
        output.SetBrightness(0.5f);
        output.Send(new LedPayload { Frame128x64 = frame, Level = 1f });

        Assert.Equal(2, host.BroadcastFrames.Count);
    }

    private sealed class FakeDeviceServerHost : IDeviceServerHost
    {
        public List<byte[]> BroadcastFrames { get; } = new();

#pragma warning disable CS0067
        public event EventHandler? DevicesChanged;
        public event EventHandler<string>? LogMessage;
        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;
#pragma warning restore CS0067

        public Task StartAsync(Device.Protocol.Contracts.ServerConfig config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public PairingCodeInfo CreatePairingCode(TimeSpan ttl) => new() { Code = "000000", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };
        public IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot() => Array.Empty<DeviceSnapshot>();
        public IReadOnlyList<DeviceRecord> GetDeviceRecords() => Array.Empty<DeviceRecord>();
        public void SeedDevices(IEnumerable<DeviceRecord> devices) { }
        public Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<CommandDispatchResult> SendCommandTrackedAsync(string deviceId, DeviceCommandType commandType, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommandDispatchResult { DeviceId = deviceId, Accepted = false, Completed = true, Success = false, ProgressPercent = 0, Stage = "offline", ErrorCode = "not_implemented", });
        public Task<CommandDispatchResult> SendCommandTrackedAsync(string deviceId, DeviceCommandType commandType, IReadOnlyDictionary<string, string>? parameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => SendCommandTrackedAsync(deviceId, commandType, timeout, cancellationToken);
        public bool RemoveDevice(string deviceId) => false;
        public void BroadcastFrame(byte[] framePayload) => BroadcastFrames.Add(framePayload);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
