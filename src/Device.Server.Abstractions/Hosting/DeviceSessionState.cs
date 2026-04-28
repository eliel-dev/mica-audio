using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-sessoes-de-device
// DOCS: docs/wiki/modules/device-server-protocol.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md
public sealed class DeviceSessionState
{
    private readonly TimeSpan detachGracePeriod;
    private DateTimeOffset? controlPlaneDetachGraceUntilUtc;
    private DateTimeOffset? lastLegacyControlPlaneActivityUtc;
    private DeviceSessionShadowMessage? sessionShadow;

    public DeviceSessionState(DeviceRecord record, TimeSpan detachGracePeriod)
    {
        ArgumentNullException.ThrowIfNull(record);

        this.detachGracePeriod = detachGracePeriod;
        Record = record;
        LastActivityUtc = record.LastSeenUtc != default && record.LastSeenUtc != DateTimeOffset.MinValue
            ? record.LastSeenUtc
            : record.CreatedAtUtc;
    }

    public DeviceRecord Record { get; private set; }

    public DateTimeOffset LastActivityUtc { get; private set; }

    public bool IsControlPlaneOnline { get; private set; }

    public void MarkSeen(
        DateTimeOffset now,
        string? ip,
        int? rssi,
        string? firmwareVersion,
        string? activeAppId = null,
        string? activeAppName = null,
        string? boardModel = null,
        string? panelType = null,
        string? lanIpAddress = null)
    {
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
            panelType,
            lanIpAddress);
    }

    public void MarkAuthenticated(DateTimeOffset now)
    {
        Record = DeviceRecordMutations.MarkAuthenticated(Record, now);
    }

    public void MarkTelemetry(
        DateTimeOffset now,
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
        bool? animatedWebpBatchSupported = null,
        bool? visualUdpSupported = null,
        int? visualUdpPort = null,
        string? visualUdpMode = null,
        string? deviceMac = null,
        string? lanIpAddress = null,
        string? sessionMode = null,
        string? sessionActiveClientId = null,
        uint? sessionActiveOwnerEpoch = null,
        int? sessionOwnerLeaseRemainingMs = null,
        bool? sessionLockHeld = null,
        string? sessionLockClientId = null,
        string? sessionLockReason = null,
        int? sessionLockLeaseRemainingMs = null,
        string? sessionFallbackState = null)
    {
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
            animatedWebpBatchSupported,
            visualUdpSupported,
            visualUdpPort,
            visualUdpMode,
            deviceMac,
            lanIpAddress);

        if (HasSessionTelemetry(
                sessionMode,
                sessionActiveClientId,
                sessionActiveOwnerEpoch,
                sessionOwnerLeaseRemainingMs,
                sessionLockHeld,
                sessionLockClientId,
                sessionLockReason,
                sessionLockLeaseRemainingMs,
                sessionFallbackState))
        {
            sessionShadow = new DeviceSessionShadowMessage
            {
                DeviceId = Record.DeviceId,
                Mode = sessionMode,
                ActiveClientId = sessionActiveClientId,
                ActiveOwnerEpoch = sessionActiveOwnerEpoch,
                OwnerLeaseRemainingMs = sessionOwnerLeaseRemainingMs,
                LockHeld = sessionLockHeld,
                LockClientId = sessionLockClientId,
                LockReason = sessionLockReason,
                LockLeaseRemainingMs = sessionLockLeaseRemainingMs,
                FallbackState = sessionFallbackState,
                ActiveAppId = Record.ActiveAppId,
            };
        }
    }

    public bool ApplySessionShadow(DeviceSessionShadowMessage shadow, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(shadow);

        if (!string.IsNullOrWhiteSpace(shadow.DeviceId)
            && !string.Equals(shadow.DeviceId, Record.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        LastActivityUtc = now;
        sessionShadow = new DeviceSessionShadowMessage
        {
            DeviceId = Record.DeviceId,
            ShadowVersion = shadow.ShadowVersion,
            Mode = NormalizeOptional(shadow.Mode),
            ActiveClientId = NormalizeOptional(shadow.ActiveClientId),
            ActiveOwnerEpoch = shadow.ActiveOwnerEpoch,
            OwnerLeaseRemainingMs = NormalizeNonNegative(shadow.OwnerLeaseRemainingMs),
            LockHeld = shadow.LockHeld,
            LockClientId = NormalizeOptional(shadow.LockClientId),
            LockReason = NormalizeOptional(shadow.LockReason),
            LockLeaseRemainingMs = NormalizeNonNegative(shadow.LockLeaseRemainingMs),
            ActiveAppId = NormalizeOptional(shadow.ActiveAppId),
            FallbackState = NormalizeOptional(shadow.FallbackState),
        };
        return true;
    }

    public void MarkStats(
        DateTimeOffset now,
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

    public void Touch(DateTimeOffset now)
    {
        LastActivityUtc = now;
    }

    public void MarkControlPlaneConnected(DateTimeOffset now, string? ip)
    {
        IsControlPlaneOnline = true;
        controlPlaneDetachGraceUntilUtc = null;
        lastLegacyControlPlaneActivityUtc = null;
        MarkSeen(
            now,
            ip,
            Record.LastKnownRssi,
            Record.FirmwareVersion,
            Record.ActiveAppId,
            Record.ActiveAppName,
            Record.BoardModel,
            Record.PanelType);
    }

    public void MarkControlPlaneDisconnected(DateTimeOffset now)
    {
        IsControlPlaneOnline = false;
        controlPlaneDetachGraceUntilUtc = now + detachGracePeriod;
    }

    public bool MarkLegacyControlPlaneTraffic(DateTimeOffset now)
    {
        if (IsControlPlaneOnline)
        {
            return false;
        }

        var isFirstObservedLegacyTraffic = !lastLegacyControlPlaneActivityUtc.HasValue;
        lastLegacyControlPlaneActivityUtc = now;
        return isFirstObservedLegacyTraffic;
    }

    public DeviceSnapshot ToSnapshot(DateTimeOffset now, TimeSpan offlineTimeout)
    {
        var withinControlPlaneGrace = IsWithinGraceWindow(now, ref controlPlaneDetachGraceUntilUtc);
        var hasFreshLegacyTraffic = HasRecentActivity(now, offlineTimeout, ref lastLegacyControlPlaneActivityUtc);
        var controlPlaneState = IsControlPlaneOnline || withinControlPlaneGrace
            ? DeviceControlPlaneState.MqttOnline
            : hasFreshLegacyTraffic
                ? DeviceControlPlaneState.LegacyOnly
                : DeviceControlPlaneState.Offline;

        return DeviceRecordMutations.ToSnapshot(
            Record,
            controlPlaneState == DeviceControlPlaneState.MqttOnline ? DeviceStatus.Online : DeviceStatus.Offline,
            controlPlaneState,
            sessionShadow);
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

    private static bool HasSessionTelemetry(
        string? sessionMode,
        string? sessionActiveClientId,
        uint? sessionActiveOwnerEpoch,
        int? sessionOwnerLeaseRemainingMs,
        bool? sessionLockHeld,
        string? sessionLockClientId,
        string? sessionLockReason,
        int? sessionLockLeaseRemainingMs,
        string? sessionFallbackState)
    {
        return !string.IsNullOrWhiteSpace(sessionMode)
            || !string.IsNullOrWhiteSpace(sessionActiveClientId)
            || sessionActiveOwnerEpoch.HasValue
            || sessionOwnerLeaseRemainingMs.HasValue
            || sessionLockHeld.HasValue
            || !string.IsNullOrWhiteSpace(sessionLockClientId)
            || !string.IsNullOrWhiteSpace(sessionLockReason)
            || sessionLockLeaseRemainingMs.HasValue
            || !string.IsNullOrWhiteSpace(sessionFallbackState);
    }

    private static int? NormalizeNonNegative(int? value)
        => value is >= 0 ? value : null;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
