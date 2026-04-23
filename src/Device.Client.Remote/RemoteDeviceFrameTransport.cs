using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace Device.Client.Remote;

// DOCS: docs/wiki/modules/output-led.md#modulo-output-led
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-websocket-frames
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public sealed class RemoteDeviceFrameTransport : IDeviceFrameTransport, IDeviceServerClientRuntime
{
    private readonly RemoteDeviceServerClientOptions options;
    private readonly Channel<FrameEnvelope> queue;
    private readonly object lifecycleGate = new();

    private CancellationTokenSource? cts;
    private Task? sendTask;

    public RemoteDeviceFrameTransport(RemoteDeviceServerClientOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        queue = Channel.CreateBounded<FrameEnvelope>(new BoundedChannelOptions(Math.Max(1, options.FrameQueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (lifecycleGate)
        {
            if (sendTask is not null)
            {
                return Task.CompletedTask;
            }

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendTask = Task.Run(() => SendLoopAsync(cts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? localCts;
        Task? localTask;
        lock (lifecycleGate)
        {
            localCts = cts;
            localTask = sendTask;
            cts = null;
            sendTask = null;
        }

        if (localCts is null)
        {
            return;
        }

        await localCts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (localTask is not null)
            {
                await localTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            localCts.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    public void SendFrame(string deviceId, byte[] framePayload)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(framePayload);
        queue.Writer.TryWrite(new FrameEnvelope(true, deviceId.Trim(), framePayload));
    }

    public void BroadcastFrame(byte[] framePayload)
    {
        ArgumentNullException.ThrowIfNull(framePayload);
        queue.Writer.TryWrite(new FrameEnvelope(false, string.Empty, framePayload));
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var ws = CreateAdminWebSocket();
                await ws.ConnectAsync(BuildWebSocketUri("/ws/v1/admin/frames"), cancellationToken).ConfigureAwait(false);
                await DrainQueueAsync(ws, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
            }

            try
            {
                await Task.Delay(options.FrameReconnectDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task DrainQueueAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var frame = await queue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var envelope = BuildFrameEnvelope(frame, out var envelopeLength);
            try
            {
                await ws.SendAsync(
                        new ReadOnlyMemory<byte>(envelope, 0, envelopeLength),
                        WebSocketMessageType.Binary,
                        true,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(envelope);
            }
        }
    }

    private ClientWebSocket CreateAdminWebSocket()
    {
        var ws = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(options.AdminToken))
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {options.AdminToken}");
        }

        return ws;
    }

    private Uri BuildWebSocketUri(string path)
    {
        var baseAddress = NormalizeBaseAddress(options.BaseAddress);
        var scheme = string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? "wss"
            : "ws";
        var builder = new UriBuilder(baseAddress)
        {
            Scheme = scheme,
            Path = path.TrimStart('/'),
            Query = string.Empty,
        };
        return builder.Uri;
    }

    private static byte[] BuildFrameEnvelope(FrameEnvelope frame, out int envelopeLength)
    {
        var deviceIdByteCount = Encoding.UTF8.GetByteCount(frame.DeviceId);
        if (deviceIdByteCount > ushort.MaxValue)
        {
            throw new InvalidOperationException("Device id is too large for the admin frame envelope.");
        }

        var payload = frame.Payload;
        envelopeLength = 1 + sizeof(ushort) + deviceIdByteCount + payload.Length;
        var envelope = ArrayPool<byte>.Shared.Rent(envelopeLength);
        envelope[0] = frame.Targeted ? (byte)1 : (byte)0;
        envelope[1] = (byte)(deviceIdByteCount & 0xFF);
        envelope[2] = (byte)(deviceIdByteCount >> 8);
        Encoding.UTF8.GetBytes(frame.DeviceId.AsSpan(), envelope.AsSpan(3, deviceIdByteCount));
        payload.CopyTo(envelope.AsSpan(3 + deviceIdByteCount, payload.Length));
        return envelope;
    }

    private static Uri NormalizeBaseAddress(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new Uri("http://127.0.0.1:5272");
        }

        return uri;
    }

    private readonly record struct FrameEnvelope(bool Targeted, string DeviceId, byte[] Payload);
}
