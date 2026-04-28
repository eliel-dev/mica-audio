using System.Buffers;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Device.Protocol.Models;
using Device.Protocol.Stream;

namespace Device.Client.Remote;

// DOCS: docs/wiki/modules/output-led.md#modulo-output-led
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-websocket-frames
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md
public sealed class RemoteDeviceFrameTransport : IDeviceFrameTransport, IDeviceServerClientRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly TimeSpan EndpointRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly RemoteDeviceServerClientOptions options;
    private readonly Channel<FrameEnvelope> queue;
    private readonly object lifecycleGate = new();
    private readonly object endpointGate = new();
    private readonly SemaphoreSlim endpointRefreshGate = new(1, 1);
    private readonly Dictionary<string, DeviceVisualEndpointInfo> visualEndpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient endpointHttpClient;
    private readonly Socket udpSocket;

    private CancellationTokenSource? cts;
    private Task? sendTask;
    private DateTimeOffset endpointsLastRefreshUtc = DateTimeOffset.MinValue;
    private long directUdpFramesSent;
    private long webSocketFallbackFramesQueued;
    private long missingEndpointFrames;
    private string? lastEndpointRefreshError;
    private string? lastUdpError;

    public RemoteDeviceFrameTransport(RemoteDeviceServerClientOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        queue = Channel.CreateBounded<FrameEnvelope>(new BoundedChannelOptions(Math.Max(1, options.FrameQueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        endpointHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3),
        };
        RemoteDeviceServerClient.ConfigureHttpClient(endpointHttpClient, options);
        udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationToken localToken;
        lock (lifecycleGate)
        {
            if (sendTask is not null)
            {
                return;
            }

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localToken = cts.Token;
            sendTask = Task.Run(() => SendLoopAsync(localToken), CancellationToken.None);
        }

        await RefreshVisualEndpointsAsync(localToken).ConfigureAwait(false);
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
        endpointRefreshGate.Dispose();
        endpointHttpClient.Dispose();
        udpSocket.Dispose();
    }

    public RemoteDeviceFrameTransportDiagnostics GetDiagnosticsSnapshot()
        => new(
            Interlocked.Read(ref directUdpFramesSent),
            Interlocked.Read(ref webSocketFallbackFramesQueued),
            Interlocked.Read(ref missingEndpointFrames),
            Volatile.Read(ref lastEndpointRefreshError),
            Volatile.Read(ref lastUdpError));

    public void SendFrame(string deviceId, byte[] framePayload)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(framePayload);
        var normalizedDeviceId = deviceId.Trim();
        if (VisualUdpFrameV1.IsSupportedPayload(framePayload)
            && TrySendDirectUdp(normalizedDeviceId, framePayload))
        {
            return;
        }

        QueueFallback(new FrameEnvelope(true, normalizedDeviceId, framePayload));
    }

    public void BroadcastFrame(byte[] framePayload)
    {
        ArgumentNullException.ThrowIfNull(framePayload);
        if (VisualUdpFrameV1.IsSupportedPayload(framePayload) && TryBroadcastDirectUdp(framePayload))
        {
            return;
        }

        QueueFallback(new FrameEnvelope(false, string.Empty, framePayload));
    }

    public async Task RefreshVisualEndpointsAsync(CancellationToken cancellationToken = default)
    {
        await endpointRefreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await endpointHttpClient
                .GetFromJsonAsync<AdminVisualEndpointsResponse>("/api/v1/admin/visual-endpoints", JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            lock (endpointGate)
            {
                visualEndpoints.Clear();
                if (response is not null)
                {
                    foreach (var endpoint in response.Devices)
                    {
                        if (!string.IsNullOrWhiteSpace(endpoint.DeviceId))
                        {
                            visualEndpoints[endpoint.DeviceId] = endpoint;
                        }
                    }
                }

                endpointsLastRefreshUtc = DateTimeOffset.UtcNow;
            }

            Volatile.Write(ref lastEndpointRefreshError, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Volatile.Write(ref lastEndpointRefreshError, ex.Message);
        }
        finally
        {
            endpointRefreshGate.Release();
        }
    }

    private bool TrySendDirectUdp(string deviceId, byte[] framePayload)
    {
        if (!TryGetEndpoint(deviceId, out var endpoint))
        {
            Interlocked.Increment(ref missingEndpointFrames);
            RefreshEndpointsIfStale();
            return false;
        }

        return TrySendDirectUdp(endpoint, framePayload);
    }

    private bool TryBroadcastDirectUdp(byte[] framePayload)
    {
        DeviceVisualEndpointInfo[] endpoints;
        lock (endpointGate)
        {
            endpoints = visualEndpoints.Values.ToArray();
        }

        if (endpoints.Length == 0)
        {
            Interlocked.Increment(ref missingEndpointFrames);
            RefreshEndpointsIfStale();
            return false;
        }

        var sent = 0;
        foreach (var endpoint in endpoints)
        {
            if (TrySendDirectUdp(endpoint, framePayload))
            {
                sent++;
            }
        }

        if (sent == 0)
        {
            RefreshEndpointsIfStale();
        }

        return sent > 0;
    }

    private bool TrySendDirectUdp(DeviceVisualEndpointInfo endpoint, byte[] framePayload)
    {
        if (!TryResolveEndpoint(endpoint, out var remoteEndPoint))
        {
            Interlocked.Increment(ref missingEndpointFrames);
            return false;
        }

        var datagramLength = VisualUdpFrameV1.GetDatagramSize(framePayload.Length);
        var rented = ArrayPool<byte>.Shared.Rent(datagramLength);
        try
        {
            if (!VisualUdpFrameV1.TryWriteDatagram(
                    rented.AsSpan(0, datagramLength),
                    framePayload,
                    endpoint.DeviceToken,
                    out var written))
            {
                return false;
            }

            udpSocket.SendTo(rented.AsSpan(0, written), SocketFlags.None, remoteEndPoint);
            Interlocked.Increment(ref directUdpFramesSent);
            Volatile.Write(ref lastUdpError, null);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            Volatile.Write(ref lastUdpError, ex.Message);
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private bool TryGetEndpoint(string deviceId, out DeviceVisualEndpointInfo endpoint)
    {
        lock (endpointGate)
        {
            return visualEndpoints.TryGetValue(deviceId, out endpoint!);
        }
    }

    private void RefreshEndpointsIfStale()
    {
        DateTimeOffset lastRefresh;
        lock (endpointGate)
        {
            lastRefresh = endpointsLastRefreshUtc;
        }

        if (DateTimeOffset.UtcNow - lastRefresh < EndpointRefreshInterval)
        {
            return;
        }

        _ = Task.Run(() => RefreshVisualEndpointsAsync(CancellationToken.None), CancellationToken.None);
    }

    private void QueueFallback(FrameEnvelope frame)
    {
        Interlocked.Increment(ref webSocketFallbackFramesQueued);
        queue.Writer.TryWrite(frame);
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

    private static bool TryResolveEndpoint(DeviceVisualEndpointInfo endpoint, out IPEndPoint remoteEndPoint)
    {
        remoteEndPoint = null!;
        if (string.IsNullOrWhiteSpace(endpoint.LanIpAddress)
            || string.IsNullOrWhiteSpace(endpoint.DeviceToken)
            || endpoint.VisualUdpPort is < 1 or > 65535
            || !string.Equals(endpoint.VisualUdpMode, "bins128", StringComparison.OrdinalIgnoreCase)
            || !IPAddress.TryParse(endpoint.LanIpAddress, out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetwork
            || (!IPAddress.IsLoopback(address) && !IsPrivateNetworkAddress(address)))
        {
            return false;
        }

        remoteEndPoint = new IPEndPoint(address, endpoint.VisualUdpPort);
        return true;
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

    private static bool IsPrivateNetworkAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private readonly record struct FrameEnvelope(bool Targeted, string DeviceId, byte[] Payload);
}

public sealed record RemoteDeviceFrameTransportDiagnostics(
    long DirectUdpFramesSent,
    long WebSocketFallbackFramesQueued,
    long MissingEndpointFrames,
    string? LastEndpointRefreshError,
    string? LastUdpError);
