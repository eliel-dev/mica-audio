using System.Net.WebSockets;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class DeviceFrameConnectionTests
{
    [Fact]
    public void AttachSocket_ShouldReplaceSocketAndRenewSendToken()
    {
        using var connection = new DeviceFrameConnection();
        using var first = new TestWebSocket();
        using var second = new TestWebSocket();

        connection.AttachSocket(first);
        var firstToken = connection.SendToken;

        connection.AttachSocket(second);

        Assert.Same(second, connection.Socket);
        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(connection.SendToken.IsCancellationRequested);
    }

    [Fact]
    public void DetachSocket_ShouldOnlyDetachMatchingSocket()
    {
        using var connection = new DeviceFrameConnection();
        using var active = new TestWebSocket();
        using var other = new TestWebSocket();
        connection.AttachSocket(active);

        Assert.False(connection.DetachSocket(other));
        Assert.Same(active, connection.Socket);

        Assert.True(connection.DetachSocket(active));
        Assert.Null(connection.Socket);
        Assert.True(connection.SendToken.IsCancellationRequested);
    }

    [Fact]
    public void QueueFrame_ShouldKeepThreeLatestPayloadsInBoundedQueue()
    {
        using var connection = new DeviceFrameConnection();
        var first = new byte[] { 1 };
        var second = new byte[] { 2 };
        var third = new byte[] { 3 };
        var fourth = new byte[] { 4 };

        connection.QueueFrame(first);
        connection.QueueFrame(second);
        connection.QueueFrame(third);
        connection.QueueFrame(fourth);

        Assert.True(connection.Outgoing.Reader.TryRead(out var queued));
        Assert.Equal(second, queued);
        Assert.True(connection.Outgoing.Reader.TryRead(out queued));
        Assert.Equal(third, queued);
        Assert.True(connection.Outgoing.Reader.TryRead(out queued));
        Assert.Equal(fourth, queued);
        Assert.False(connection.Outgoing.Reader.TryRead(out _));
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
            => throw new NotSupportedException();

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
