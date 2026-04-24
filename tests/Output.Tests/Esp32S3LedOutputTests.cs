using System.Buffers.Binary;
using Device.Client;
using Device.Protocol.Models;
using Device.Protocol.Stream;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Output.Tests;

public class Esp32S3LedOutputTests
{
    [Fact]
    public void Send_ShouldBroadcastEncodedBins128Frame()
    {
        var host = new FakeDeviceFrameTransport();
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
            BinsFlags = Bins128VisualFlags.Create(Bins128VisualStyle.MirrorLines, Bins128PaletteFamily.Rainbow),
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
        Assert.Equal(Bins128VisualFlags.Create(Bins128VisualStyle.MirrorLines, Bins128PaletteFamily.Rainbow), payload[144]);
    }

    [Fact]
    public void SetBrightness_ShouldAffectBrightnessByteInPayload()
    {
        var host = new FakeDeviceFrameTransport();
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
        var host = new FakeDeviceFrameTransport();
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
        var host = new FakeDeviceFrameTransport();
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
        var host = new FakeDeviceFrameTransport();
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
        var host = new FakeDeviceFrameTransport();
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

    [Fact]
    public void Send_WithTargetDeviceId_ShouldUseDirectedFrameDispatch()
    {
        var host = new FakeDeviceFrameTransport();
        var output = new Esp32S3LedOutput(host);

        output.Start(new LedOutputConfig
        {
            Width = 128,
            Height = 64,
            Brightness = 1f,
            TargetDeviceId = "device-42",
        });

        var frame = new RgbaColor[128 * 64];
        frame[0] = new RgbaColor(0, 0, 255, 255);

        output.Send(new LedPayload
        {
            Frame128x64 = frame,
            Level = 1f,
        });

        Assert.Empty(host.BroadcastFrames);
        var directed = Assert.Single(host.TargetedFrames);
        Assert.Equal("device-42", directed.DeviceId);
        Assert.Equal(StreamFrameV2.MessageTypeFrame128x64Rgb565, directed.Payload[1]);
    }

    [Fact]
    public void Send_WithTargetOwnerEpoch_ShouldUseDirectedStreamFrameV3()
    {
        var host = new FakeDeviceFrameTransport();
        using var sessions = new FakeDeviceClientSessionManager();
        sessions.SetOwner("device-42", 11);
        var output = new Esp32S3LedOutput(host, sessions);

        output.Start(new LedOutputConfig
        {
            Width = 128,
            Height = 64,
            Brightness = 1f,
            TargetDeviceId = "device-42",
        });

        output.Send(new LedPayload
        {
            Bins128 = Enumerable.Repeat(0.5f, 128).ToArray(),
            Level = 0.25f,
        });

        Assert.Empty(host.BroadcastFrames);
        var directed = Assert.Single(host.TargetedFrames);
        Assert.Equal("device-42", directed.DeviceId);
        Assert.Equal(StreamFrameV3.PayloadSizeBins128, directed.Payload.Length);
        Assert.Equal(StreamFrameV3.Version, directed.Payload[0]);
        Assert.Equal(StreamFrameV3.MessageTypeBins128, directed.Payload[1]);
        Assert.Equal((uint)11, BinaryPrimitives.ReadUInt32LittleEndian(directed.Payload.AsSpan(6, 4)));
    }

    [Fact]
    public void Send_BroadcastWithVisualizerOwners_ShouldFanOutStreamFrameV3PerDevice()
    {
        var host = new FakeDeviceFrameTransport();
        using var sessions = new FakeDeviceClientSessionManager();
        sessions.SetFrameTargets("visualizer", [
            new DeviceClientFrameTarget("device-a", 3),
            new DeviceClientFrameTarget("device-b", 4),
        ]);
        var output = new Esp32S3LedOutput(host, sessions);

        output.Start(new LedOutputConfig
        {
            Width = 128,
            Height = 64,
            Brightness = 1f,
        });

        output.Send(new LedPayload
        {
            Bins128 = Enumerable.Repeat(0.75f, 128).ToArray(),
            Level = 0.5f,
        });

        Assert.Empty(host.BroadcastFrames);
        Assert.Equal(2, host.TargetedFrames.Count);
        Assert.Equal("device-a", host.TargetedFrames[0].DeviceId);
        Assert.Equal((uint)3, BinaryPrimitives.ReadUInt32LittleEndian(host.TargetedFrames[0].Payload.AsSpan(6, 4)));
        Assert.Equal("device-b", host.TargetedFrames[1].DeviceId);
        Assert.Equal((uint)4, BinaryPrimitives.ReadUInt32LittleEndian(host.TargetedFrames[1].Payload.AsSpan(6, 4)));
    }

    private sealed class FakeDeviceFrameTransport : IDeviceFrameTransport
    {
        public List<byte[]> BroadcastFrames { get; } = new();

        public List<(string DeviceId, byte[] Payload)> TargetedFrames { get; } = new();

        public void SendFrame(string deviceId, byte[] framePayload) => TargetedFrames.Add((deviceId, framePayload));
        public void BroadcastFrame(byte[] framePayload) => BroadcastFrames.Add(framePayload);
    }

    private sealed class FakeDeviceClientSessionManager : IDeviceClientSessionManager
    {
        private readonly Dictionary<string, uint> ownerEpochByDeviceId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<DeviceClientFrameTarget>> frameTargetsByMode = new(StringComparer.OrdinalIgnoreCase);

        public string ClientId => "win-test";

        public uint? GetOwnerEpoch(string deviceId)
            => ownerEpochByDeviceId.TryGetValue(deviceId, out var epoch) ? epoch : null;

        public IReadOnlyList<DeviceClientFrameTarget> GetFrameTargets(string? deviceId, string mode)
        {
            if (!string.IsNullOrWhiteSpace(deviceId) && ownerEpochByDeviceId.TryGetValue(deviceId, out var targetEpoch))
            {
                return [new DeviceClientFrameTarget(deviceId, targetEpoch)];
            }

            return frameTargetsByMode.TryGetValue(mode, out var targets)
                ? targets
                : Array.Empty<DeviceClientFrameTarget>();
        }

        public DeviceCommandSessionContext? CreateCommandContext(string deviceId)
            => GetOwnerEpoch(deviceId) is { } epoch ? new DeviceCommandSessionContext(ClientId, epoch, null) : null;

        public Task<DeviceCommandSessionContext> AssumeOwnerAsync(string deviceId, string mode, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeviceCommandSessionContext(ClientId, ownerEpochByDeviceId[deviceId], null));

        public void ObserveDevices(IReadOnlyList<Device.Protocol.Models.DeviceSnapshot> devices)
        {
        }

        public void StartHeartbeat(string deviceId, string mode)
        {
        }

        public void StopHeartbeat(string deviceId)
        {
        }

        public Task<DeviceClientLockLease> AcquireLockAsync(
            string deviceId,
            string reason,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeviceClientLockLease(deviceId, "lock", reason));

        public Task ReleaseLockAsync(string deviceId, string lockToken, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void SetOwner(string deviceId, uint ownerEpoch)
            => ownerEpochByDeviceId[deviceId] = ownerEpoch;

        public void SetFrameTargets(string mode, IReadOnlyList<DeviceClientFrameTarget> targets)
            => frameTargetsByMode[mode] = targets;

        public void Dispose()
        {
        }
    }
}
