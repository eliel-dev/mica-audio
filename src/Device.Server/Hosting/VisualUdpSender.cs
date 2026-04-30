using System.Net;
using System.Net.Sockets;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#atualizacao-2026-04---visual-udp-lan-opt-in
// DOCS: docs/wiki/reference/ws-protocol-v2.md#udp-visual-v1
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
internal interface IVisualUdpSender
{
    bool TrySend(IPAddress address, int port, ReadOnlySpan<byte> datagram);
}

internal sealed class SocketVisualUdpSender : IVisualUdpSender, IDisposable
{
    private readonly Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    public bool TrySend(IPAddress address, int port, ReadOnlySpan<byte> datagram)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetwork || port is < 1 or > 65535 || datagram.IsEmpty)
        {
            return false;
        }

        try
        {
            return socket.SendTo(datagram, SocketFlags.None, new IPEndPoint(address, port)) == datagram.Length;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        socket.Dispose();
    }
}
