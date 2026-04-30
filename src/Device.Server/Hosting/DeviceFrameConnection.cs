using System.Net.WebSockets;
using System.Threading.Channels;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-sessoes-de-device
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
internal sealed class DeviceFrameConnection : IDisposable
{
    public const int DefaultOutgoingFrameCapacity = 3;

    private CancellationTokenSource senderCts = new();

    public DeviceFrameConnection(int outgoingFrameCapacity = DefaultOutgoingFrameCapacity)
    {
        Outgoing = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(Math.Max(1, outgoingFrameCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    }

    public WebSocket? Socket { get; private set; }

    public Channel<byte[]> Outgoing { get; }

    public CancellationToken SendToken => senderCts.Token;

    public void AttachSocket(WebSocket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);

        senderCts.Cancel();
        senderCts.Dispose();
        senderCts = new CancellationTokenSource();
        Socket = socket;
    }

    public bool DetachSocket(WebSocket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);

        if (!ReferenceEquals(Socket, socket))
        {
            return false;
        }

        senderCts.Cancel();
        Socket = null;
        return true;
    }

    public void QueueFrame(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Outgoing.Writer.TryWrite(frame);
    }

    public void Dispose()
    {
        senderCts.Cancel();
        senderCts.Dispose();

        if (Socket is not null)
        {
            try
            {
                Socket.Abort();
                Socket.Dispose();
            }
            catch
            {
                // ignore socket disposal errors
            }

            Socket = null;
        }

        Outgoing.Writer.TryComplete();
    }
}
