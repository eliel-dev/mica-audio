using System.Net.WebSockets;
using System.Reflection;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Protocol.Stream;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class DeviceServerHostTargetedFrameTests
{
    [Fact]
    public async Task SendFrame_ShouldQueuePayloadOnlyForTargetDevice()
    {
        await using var host = new DeviceServerHost();
        host.SeedDevices([
            CreateRecord("device-1", "token-1"),
            CreateRecord("device-2", "token-2"),
        ]);

        var registry = GetFrameRegistry(host);
        var targetConnection = registry.GetOrCreate("device-1");
        var otherConnection = registry.GetOrCreate("device-2");
        using var targetSocket = new TestWebSocket();
        using var otherSocket = new TestWebSocket();
        targetConnection.AttachSocket(targetSocket);
        otherConnection.AttachSocket(otherSocket);

        var payload = new byte[] { 1, 2, 3, 4 };
        host.SendFrame("device-1", payload);

        Assert.True(targetConnection.Outgoing.Reader.TryRead(out var queuedForTarget));
        Assert.Equal(payload, queuedForTarget);
        Assert.False(otherConnection.Outgoing.Reader.TryRead(out _));
    }

    [Fact]
    public async Task SendFrame_ShouldUseUdpForOnlineDeviceWithBins128Capability()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemorySessionStateStore();
        var state = new DeviceSessionState(
            CreateUdpRecord("device-udp", "token-udp", "192.168.1.44"),
            TimeSpan.FromMilliseconds(500));
        state.MarkControlPlaneConnected(now, "192.168.1.44");
        store.Upsert(state);

        var udpSender = new FakeVisualUdpSender();
        await using var host = CreateHost(store, udpSender);
        SetRuntimeConfig(host, new ServerConfig
        {
            PreferLanUdpVisualTransport = true,
            VisualUdpPort = 5274,
        });

        var connection = GetFrameRegistry(host).GetOrCreate("device-udp");
        using var socket = new TestWebSocket();
        connection.AttachSocket(socket);

        var payload = CreateBinsPayload(sequence: 9);
        host.SendFrame("device-udp", payload);

        var send = Assert.Single(udpSender.Sends);
        Assert.Equal("192.168.1.44", send.Address.ToString());
        Assert.Equal(5274, send.Port);
        Assert.True(VisualUdpFrameV1.TryValidateDatagram(
            send.Datagram,
            "token-udp",
            lastAcceptedSequence: null,
            out var sequence,
            out var payloadOffset,
            out var payloadLength));
        Assert.Equal(9u, sequence);
        Assert.Equal(payload, send.Datagram.AsSpan(payloadOffset, payloadLength).ToArray());
        Assert.False(connection.Outgoing.Reader.TryRead(out _));
    }

    [Fact]
    public async Task SendFrame_ShouldFallbackToWebSocketWhenUdpCannotSend()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemorySessionStateStore();
        var state = new DeviceSessionState(
            CreateUdpRecord("device-fallback", "token-fallback", "192.168.1.45"),
            TimeSpan.FromMilliseconds(500));
        state.MarkControlPlaneConnected(now, "192.168.1.45");
        store.Upsert(state);

        var udpSender = new FakeVisualUdpSender { Result = false };
        await using var host = CreateHost(store, udpSender);
        SetRuntimeConfig(host, new ServerConfig
        {
            PreferLanUdpVisualTransport = true,
            VisualUdpPort = 5274,
        });

        var connection = GetFrameRegistry(host).GetOrCreate("device-fallback");
        using var socket = new TestWebSocket();
        connection.AttachSocket(socket);

        var payload = CreateBinsPayload(sequence: 10);
        host.SendFrame("device-fallback", payload);

        Assert.Single(udpSender.Sends);
        Assert.True(connection.Outgoing.Reader.TryRead(out var queued));
        Assert.Equal(payload, queued);
    }

    [Fact]
    public async Task SendFrame_ShouldKeepRawRgb565OnWebSocket()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemorySessionStateStore();
        var state = new DeviceSessionState(
            CreateUdpRecord("device-raw", "token-raw", "192.168.1.46"),
            TimeSpan.FromMilliseconds(500));
        state.MarkControlPlaneConnected(now, "192.168.1.46");
        store.Upsert(state);

        var udpSender = new FakeVisualUdpSender();
        await using var host = CreateHost(store, udpSender);
        SetRuntimeConfig(host, new ServerConfig
        {
            PreferLanUdpVisualTransport = true,
            VisualUdpPort = 5274,
        });

        var connection = GetFrameRegistry(host).GetOrCreate("device-raw");
        using var socket = new TestWebSocket();
        connection.AttachSocket(socket);

        var payload = StreamFrameV2.CreateFrame128x64Rgb565(
            sequence: 11,
            timestampQpc: 100,
            pixels128x64Rgb565: new ushort[StreamFrameV2.PixelCount128x64],
            brightness0To255: 255);
        host.SendFrame("device-raw", payload);

        Assert.Empty(udpSender.Sends);
        Assert.True(connection.Outgoing.Reader.TryRead(out var queued));
        Assert.Equal(payload, queued);
    }

    private static DeviceRecord CreateRecord(string deviceId, string token)
    {
        return new DeviceRecord
        {
            DeviceId = deviceId,
            Token = token,
            Name = deviceId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow,
            IsRegistered = true,
        };
    }

    private static DeviceRecord CreateUdpRecord(string deviceId, string token, string lastKnownIp)
    {
        return new DeviceRecord
        {
            DeviceId = deviceId,
            Token = token,
            Name = deviceId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastSeenUtc = DateTimeOffset.UtcNow,
            IsRegistered = true,
            LastKnownIp = lastKnownIp,
            VisualUdpSupported = true,
            VisualUdpPort = 5274,
            VisualUdpMode = "bins128",
        };
    }

    private static DeviceServerHost CreateHost(ISessionStateStore store, IVisualUdpSender udpSender)
        => new(
            TimeProvider.System,
            firmwareCatalog: null,
            panelsBatchStore: null,
            pairingStore: null,
            commandStateStore: null,
            sessionStateStore: store,
            visualUdpSender: udpSender);

    private static byte[] CreateBinsPayload(uint sequence)
    {
        Span<byte> bins = stackalloc byte[StreamFrameV2.BinCount128];
        for (var index = 0; index < bins.Length; index++)
        {
            bins[index] = (byte)index;
        }

        return StreamFrameV2.CreateBins128(sequence, timestampQpc: 100, level0To255: 200, bins, brightness0To255: 128);
    }

    private static DeviceFrameConnectionRegistry GetFrameRegistry(DeviceServerHost host)
    {
        var field = typeof(DeviceServerHost).GetField("frameConnections", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<DeviceFrameConnectionRegistry>(field!.GetValue(host));
    }

    private static void SetRuntimeConfig(DeviceServerHost host, ServerConfig config)
    {
        var field = typeof(DeviceServerHost).GetField("runtimeConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(host, DeviceServerRuntimeConfig.From(config));
    }

    private sealed class TestWebSocket : WebSocket
    {
        private WebSocketState state = WebSocketState.Open;

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State => state;

        public override string? SubProtocol => null;

        public override void Abort()
        {
            state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            state = WebSocketState.Closed;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVisualUdpSender : IVisualUdpSender
    {
        public bool Result { get; init; } = true;

        public List<(System.Net.IPAddress Address, int Port, byte[] Datagram)> Sends { get; } = [];

        public bool TrySend(System.Net.IPAddress address, int port, ReadOnlySpan<byte> datagram)
        {
            Sends.Add((address, port, datagram.ToArray()));
            return Result;
        }
    }
}
