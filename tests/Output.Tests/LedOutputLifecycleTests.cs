using Device.Protocol.Models;
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
        using var host = new FakeDeviceServerHost();
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

    private sealed class FakeDeviceServerHost : IDeviceServerHost, IDisposable
    {
        public List<byte[]> BroadcastFrames { get; } = new();

        public event EventHandler? DevicesChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? LogMessage
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived
        {
            add { }
            remove { }
        }

        public Task StartAsync(Device.Protocol.Contracts.ServerConfig config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public PairingCodeInfo CreatePairingCode(TimeSpan ttl) => new() { Code = "000000", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };
        public IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot() => Array.Empty<DeviceSnapshot>();
        public IReadOnlyList<DeviceRecord> GetDeviceRecords() => Array.Empty<DeviceRecord>();
        public void SeedDevices(IEnumerable<DeviceRecord> devices) { }
        public Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<CommandDispatchResult> SendCommandTrackedAsync(string deviceId, DeviceCommandType commandType, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommandDispatchResult { DeviceId = deviceId, Accepted = false, Completed = true, Success = false, ProgressPercent = 0, Stage = "offline", ErrorCode = "not_implemented" });
        public Task<CommandDispatchResult> SendCommandTrackedAsync(string deviceId, DeviceCommandType commandType, IReadOnlyDictionary<string, string>? parameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => SendCommandTrackedAsync(deviceId, commandType, timeout, cancellationToken);
        public bool RemoveDevice(string deviceId) => false;
        public void SendFrame(string deviceId, byte[] framePayload) => BroadcastFrames.Add(framePayload);
        public void BroadcastFrame(byte[] framePayload) => BroadcastFrames.Add(framePayload);
        public PanelsBatchRegistration RegisterPanelsBatch(string deviceId, string panelsSessionId, ulong batchSequence, byte[] payload, int frameCount, int durationMs, string contentType = "image/webp")
            => new(panelsSessionId, batchSequence, payload.LongLength, Sha256: string.Empty, contentType, frameCount, durationMs, DownloadUrl: string.Empty);
        public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null) { }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
