using System.Net.WebSockets;
using System.Threading.Channels;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#fluxo-de-execucao
internal sealed class DeviceSession : IDisposable
{
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan socketDetachGracePeriod;
    private CancellationTokenSource senderCts = new();
    private DateTimeOffset? socketDetachGraceUntilUtc;

    public DeviceSession(DeviceRecord record, TimeProvider timeProvider, TimeSpan socketDetachGracePeriod)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.timeProvider = timeProvider;
        this.socketDetachGracePeriod = socketDetachGracePeriod;
        Record = record;
        LastActivityUtc = record.LastSeenUtc != default && record.LastSeenUtc != DateTimeOffset.MinValue
            ? record.LastSeenUtc
            : record.CreatedAtUtc;
        Outgoing = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    }

    public DeviceRecord Record { get; private set; }

    public WebSocket? Socket { get; private set; }

    public Channel<byte[]> Outgoing { get; }

    public DateTimeOffset LastActivityUtc { get; private set; }

    public CancellationToken SendToken => senderCts.Token;

    public void MarkSeen(
        string? ip,
        int? rssi,
        string? firmwareVersion,
        string? activeAppId = null,
        string? activeAppName = null,
        string? boardModel = null,
        string? panelType = null)
    {
        var now = timeProvider.GetUtcNow();
        LastActivityUtc = now;
        Record = DeviceRecordMutations.MarkSeen(
            Record,
            now,
            ip,
            rssi,
            firmwareVersion,
            activeAppId,
            activeAppName,
            boardModel,
            panelType);
    }

    public void MarkAuthenticated()
    {
        Record = DeviceRecordMutations.MarkAuthenticated(Record, timeProvider.GetUtcNow());
    }

    public void MarkTelemetry(
        string? ip,
        int? rssi,
        string? firmwareVersion,
        string? activeAppId = null,
        string? activeAppName = null,
        string? boardModel = null,
        string? panelType = null,
        int? uptimeSeconds = null,
        int? loopLoadPercent = null,
        long? freeHeapBytes = null,
        long? largestHeapBlockBytes = null,
        bool? psramAvailable = null,
        long? freePsramBytes = null,
        long? largestPsramBlockBytes = null,
        bool? wifiConnected = null,
        string? wifiState = null,
        bool? provisioningPortalActive = null,
        bool? auxLedAvailable = null,
        bool? testLedAvailable = null,
        string? lastWifiEvent = null,
        uint? streamLastSequence = null,
        uint? streamFramesReceived = null,
        uint? streamFramesApplied = null,
        uint? streamSequenceGapCount = null,
        uint? streamInvalidFrameCount = null,
        uint? telemetrySequence = null,
        int? brightnessCap = null,
        int? brightnessRequested = null,
        int? brightnessApplied = null,
        bool? testLedEnabled = null,
        int? testLedDuty = null)
    {
        var now = timeProvider.GetUtcNow();
        LastActivityUtc = now;
        Record = DeviceRecordMutations.MarkTelemetry(
            Record,
            now,
            ip,
            rssi,
            firmwareVersion,
            activeAppId,
            activeAppName,
            boardModel,
            panelType,
            uptimeSeconds,
            loopLoadPercent,
            freeHeapBytes,
            largestHeapBlockBytes,
            psramAvailable,
            freePsramBytes,
            largestPsramBlockBytes,
            wifiConnected,
            wifiState,
            provisioningPortalActive,
            auxLedAvailable,
            testLedAvailable,
            lastWifiEvent,
            streamLastSequence,
            streamFramesReceived,
            streamFramesApplied,
            streamSequenceGapCount,
            streamInvalidFrameCount,
            telemetrySequence,
            brightnessCap,
            brightnessRequested,
            brightnessApplied,
            testLedEnabled,
            testLedDuty);
    }

    public void Touch()
    {
        LastActivityUtc = timeProvider.GetUtcNow();
    }

    public void AttachSocket(WebSocket socket, string? ip)
    {
        ArgumentNullException.ThrowIfNull(socket);

        senderCts.Cancel();
        senderCts.Dispose();
        senderCts = new CancellationTokenSource();
        Socket = socket;
        socketDetachGraceUntilUtc = null;
        MarkSeen(ip, Record.LastKnownRssi, Record.FirmwareVersion);
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
        socketDetachGraceUntilUtc = timeProvider.GetUtcNow() + socketDetachGracePeriod;
        return true;
    }

    public void QueueFrame(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Outgoing.Writer.TryWrite(frame);
    }

    public DeviceSnapshot ToSnapshot(TimeSpan offlineTimeout)
    {
        var now = timeProvider.GetUtcNow();
        var onlineBySocket = Socket is { State: WebSocketState.Open } && (now - LastActivityUtc) <= offlineTimeout;

        var withinDetachGrace = false;
        if (socketDetachGraceUntilUtc.HasValue)
        {
            if (now <= socketDetachGraceUntilUtc.Value)
            {
                withinDetachGrace = true;
            }
            else
            {
                socketDetachGraceUntilUtc = null;
            }
        }

        return DeviceRecordMutations.ToSnapshot(
            Record,
            onlineBySocket || withinDetachGrace ? DeviceStatus.Online : DeviceStatus.Offline);
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
