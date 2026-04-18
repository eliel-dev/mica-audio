using System.Net.WebSockets;
using System.Threading.Channels;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#fluxo-de-execucao
internal sealed class DeviceSession : IDisposable
{
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan detachGracePeriod;
    private CancellationTokenSource senderCts = new();
    private DateTimeOffset? socketDetachGraceUntilUtc;
    private DateTimeOffset? controlPlaneDetachGraceUntilUtc;
    private DateTimeOffset? lastLegacyControlPlaneActivityUtc;

    public DeviceSession(DeviceRecord record, TimeProvider timeProvider, TimeSpan detachGracePeriod)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.timeProvider = timeProvider;
        this.detachGracePeriod = detachGracePeriod;
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

    public bool IsControlPlaneOnline { get; private set; }

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
        int? loopHealthyPercent = null,
        int? loopLoadPercent = null,
        double? chipTemperatureCelsius = null,
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
        uint? hub75PresentFrames = null,
        uint? streamSequenceGapCount = null,
        uint? streamInvalidFrameCount = null,
        string? resetReason = null,
        uint? controlQueueDepth = null,
        string? controlWorkerState = null,
        string? panelsWorkerState = null,
        string? lastSlowCommand = null,
        long? lastSlowCommandDurationMs = null,
        uint? telemetrySequence = null,
        int? brightnessCap = null,
        int? brightnessRequested = null,
        int? brightnessApplied = null,
        bool? testLedEnabled = null,
        int? testLedDuty = null,
        bool? animatedWebpBatchSupported = null)
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
            loopHealthyPercent,
            loopLoadPercent,
            chipTemperatureCelsius,
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
            hub75PresentFrames,
            streamSequenceGapCount,
            streamInvalidFrameCount,
            resetReason,
            controlQueueDepth,
            controlWorkerState,
            panelsWorkerState,
            lastSlowCommand,
            lastSlowCommandDurationMs,
            telemetrySequence,
            brightnessCap,
            brightnessRequested,
            brightnessApplied,
            testLedEnabled,
            testLedDuty,
            animatedWebpBatchSupported);
    }

    public void MarkStats(
        string? ip,
        string? chipModel = null,
        int? chipRevision = null,
        int? chipCores = null,
        int? cpuFreqMHz = null,
        string? sdkVersion = null,
        long? heapTotalBytes = null,
        long? psramTotalBytes = null,
        long? flashTotalBytes = null,
        long? sketchSizeBytes = null,
        long? freeSketchBytes = null)
    {
        var now = timeProvider.GetUtcNow();
        LastActivityUtc = now;
        Record = DeviceRecordMutations.MarkStats(
            Record,
            now,
            ip,
            chipModel,
            chipRevision,
            chipCores,
            cpuFreqMHz,
            sdkVersion,
            heapTotalBytes,
            psramTotalBytes,
            flashTotalBytes,
            sketchSizeBytes,
            freeSketchBytes);
    }

    public void Touch()
    {
        LastActivityUtc = timeProvider.GetUtcNow();
    }

    public void MarkControlPlaneConnected(string? ip)
    {
        IsControlPlaneOnline = true;
        controlPlaneDetachGraceUntilUtc = null;
        lastLegacyControlPlaneActivityUtc = null;
        MarkSeen(
            ip,
            Record.LastKnownRssi,
            Record.FirmwareVersion,
            Record.ActiveAppId,
            Record.ActiveAppName,
            Record.BoardModel,
            Record.PanelType);
    }

    public void MarkControlPlaneDisconnected()
    {
        IsControlPlaneOnline = false;
        controlPlaneDetachGraceUntilUtc = timeProvider.GetUtcNow() + detachGracePeriod;
    }

    public bool MarkLegacyControlPlaneTraffic()
    {
        if (IsControlPlaneOnline)
        {
            return false;
        }

        var isFirstObservedLegacyTraffic = !lastLegacyControlPlaneActivityUtc.HasValue;
        lastLegacyControlPlaneActivityUtc = timeProvider.GetUtcNow();
        return isFirstObservedLegacyTraffic;
    }

    public void AttachSocket(WebSocket socket, string? ip)
    {
        ArgumentNullException.ThrowIfNull(socket);

        senderCts.Cancel();
        senderCts.Dispose();
        senderCts = new CancellationTokenSource();
        Socket = socket;
        socketDetachGraceUntilUtc = null;
        LastActivityUtc = timeProvider.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            Record = DeviceRecordMutations.MarkSeen(
                Record,
                LastActivityUtc,
                ip,
                Record.LastKnownRssi,
                Record.FirmwareVersion,
                Record.ActiveAppId,
                Record.ActiveAppName,
                Record.BoardModel,
                Record.PanelType);
        }
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
        socketDetachGraceUntilUtc = timeProvider.GetUtcNow() + detachGracePeriod;
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
        var withinControlPlaneGrace = IsWithinGraceWindow(now, ref controlPlaneDetachGraceUntilUtc);
        _ = IsWithinGraceWindow(now, ref socketDetachGraceUntilUtc);
        var hasFreshLegacyTraffic = HasRecentActivity(now, offlineTimeout, ref lastLegacyControlPlaneActivityUtc);
        var controlPlaneState = IsControlPlaneOnline || withinControlPlaneGrace
            ? DeviceControlPlaneState.MqttOnline
            : hasFreshLegacyTraffic
                ? DeviceControlPlaneState.LegacyOnly
                : DeviceControlPlaneState.Offline;

        return DeviceRecordMutations.ToSnapshot(
            Record,
            controlPlaneState == DeviceControlPlaneState.MqttOnline ? DeviceStatus.Online : DeviceStatus.Offline,
            controlPlaneState);
    }

    private static bool IsWithinGraceWindow(DateTimeOffset now, ref DateTimeOffset? graceUntilUtc)
    {
        if (!graceUntilUtc.HasValue)
        {
            return false;
        }

        if (now <= graceUntilUtc.Value)
        {
            return true;
        }

        graceUntilUtc = null;
        return false;
    }

    private static bool HasRecentActivity(DateTimeOffset now, TimeSpan window, ref DateTimeOffset? lastActivityUtc)
    {
        if (!lastActivityUtc.HasValue)
        {
            return false;
        }

        if (window > TimeSpan.Zero && (now - lastActivityUtc.Value) <= window)
        {
            return true;
        }

        lastActivityUtc = null;
        return false;
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
