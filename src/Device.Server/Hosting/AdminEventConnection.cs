using System.Net.WebSockets;

namespace Device.Server.Hosting;

internal sealed class AdminEventConnection : IDisposable
{
    private readonly SemaphoreSlim sendGate = new(1, 1);

    public AdminEventConnection(WebSocket socket)
    {
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
    }

    public WebSocket Socket { get; }

    public async Task<bool> TrySendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (Socket.State != WebSocketState.Open)
        {
            return false;
        }

        try
        {
            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (Socket.State != WebSocketState.Open)
            {
                return false;
            }

            await Socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (sendGate.CurrentCount == 0)
            {
                sendGate.Release();
            }
        }
    }

    public void Dispose()
    {
        try
        {
            Socket.Abort();
            Socket.Dispose();
        }
        catch
        {
        }

        sendGate.Dispose();
    }
}
