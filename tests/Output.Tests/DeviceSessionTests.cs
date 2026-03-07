using System.Net.WebSockets;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public class DeviceSessionTests
{
    [Fact]
    public void MarkAuthenticated_ShouldStampFirstSeenAndLastAuth()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        using var session = new DeviceSession(CreateRecord(timeProvider.GetUtcNow()), timeProvider, TimeSpan.FromMilliseconds(500));

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        session.MarkAuthenticated();

        Assert.True(session.Record.IsRegistered);
        Assert.Equal(timeProvider.GetUtcNow(), session.Record.FirstSeenUtc);
        Assert.Equal(timeProvider.GetUtcNow(), session.Record.LastAuthUtc);
        Assert.Equal(DeviceConfigState.KnownGood, session.Record.ConfigState);
    }

    [Fact]
    public void MarkTelemetry_ShouldStampTelemetryAndPreserveAuth()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        using var session = new DeviceSession(CreateRecord(timeProvider.GetUtcNow()), timeProvider, TimeSpan.FromMilliseconds(500));

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        session.MarkAuthenticated();
        var authUtc = session.Record.LastAuthUtc;

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        session.MarkTelemetry(
            ip: "192.168.0.10",
            rssi: -52,
            firmwareVersion: "1.0.0",
            activeAppId: "visualizer",
            activeAppName: "Visualizer",
            boardModel: "esp32s3_devkitc1",
            panelType: "hub75",
            uptimeSeconds: 321,
            loopLoadPercent: 40,
            freeHeapBytes: 4096,
            largestHeapBlockBytes: 2048,
            psramAvailable: true,
            freePsramBytes: 8192,
            largestPsramBlockBytes: 4096,
            wifiConnected: true,
            wifiState: "connected");

        Assert.Equal(timeProvider.GetUtcNow(), session.Record.LastTelemetryUtc);
        Assert.Equal(authUtc, session.Record.LastAuthUtc);
        Assert.Equal("192.168.0.10", session.Record.LastKnownIp);
        Assert.Equal(321, session.Record.UptimeSeconds);
        Assert.Equal("Visualizer", session.Record.ActiveAppName);
        Assert.Equal("hub75", session.Record.PanelType);
    }

    [Fact]
    public void DetachSocket_ShouldRespectGracePeriodWithControlledTime()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        using var session = new DeviceSession(CreateRecord(timeProvider.GetUtcNow()), timeProvider, TimeSpan.FromMilliseconds(500));
        using var socket = new TestWebSocket();

        session.AttachSocket(socket, "192.168.0.11");
        Assert.Equal(DeviceStatus.Online, session.ToSnapshot(TimeSpan.FromSeconds(15)).Status);

        Assert.True(session.DetachSocket(socket));
        Assert.Equal(DeviceStatus.Online, session.ToSnapshot(TimeSpan.FromSeconds(15)).Status);

        timeProvider.Advance(TimeSpan.FromMilliseconds(600));
        Assert.Equal(DeviceStatus.Offline, session.ToSnapshot(TimeSpan.FromSeconds(15)).Status);
    }

    private static DeviceRecord CreateRecord(DateTimeOffset now)
    {
        return new DeviceRecord
        {
            DeviceId = "device-1",
            Token = "token-1",
            Name = "Device 1",
            CreatedAtUtc = now,
            LastSeenUtc = now,
            IsRegistered = false,
        };
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
