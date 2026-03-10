using System.Net.WebSockets;
using System.Reflection;
using Device.Protocol.Models;
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

        var registry = GetRegistry(host);
        var targetSession = GetSession(registry, "device-1");
        var otherSession = GetSession(registry, "device-2");
        using var targetSocket = new TestWebSocket();
        using var otherSocket = new TestWebSocket();
        targetSession.AttachSocket(targetSocket, "192.168.0.10");
        otherSession.AttachSocket(otherSocket, "192.168.0.11");

        var payload = new byte[] { 1, 2, 3, 4 };
        host.SendFrame("device-1", payload);

        Assert.True(targetSession.Outgoing.Reader.TryRead(out var queuedForTarget));
        Assert.Equal(payload, queuedForTarget);
        Assert.False(otherSession.Outgoing.Reader.TryRead(out _));
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

    private static DeviceSessionRegistry GetRegistry(DeviceServerHost host)
    {
        var field = typeof(DeviceServerHost).GetField("devices", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<DeviceSessionRegistry>(field!.GetValue(host));
    }

    private static DeviceSession GetSession(DeviceSessionRegistry registry, string deviceId)
    {
        Assert.True(registry.TryGetValue(deviceId, out var session));
        return Assert.IsType<DeviceSession>(session);
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
}
