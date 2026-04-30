using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#render-estavel-na-devicespage
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
internal readonly record struct DeviceRefreshStateSnapshot(
    IReadOnlyList<DeviceSnapshot> Devices,
    DateTimeOffset LastRefreshUtc);

internal readonly record struct DeviceRefreshUpdate(
    bool Changed,
    IReadOnlyList<DeviceSnapshot> PreviousSnapshot,
    IReadOnlyList<DeviceSnapshot> CurrentSnapshot);

internal sealed class DeviceRefreshCoordinator
{
    private readonly object gate = new();
    private readonly List<DeviceSnapshot> devicesSnapshot = new();
    private int refreshInFlight;
    private bool refreshActive;
    private DateTimeOffset lastRefreshUtc;

    public DeviceRefreshStateSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new DeviceRefreshStateSnapshot(devicesSnapshot.ToArray(), lastRefreshUtc);
        }
    }

    public void SetVisible(bool visible)
    {
        lock (gate)
        {
            refreshActive = visible;
        }
    }

    public bool IsVisible()
    {
        lock (gate)
        {
            return refreshActive;
        }
    }

    public bool TryEnterRefresh()
        => Interlocked.CompareExchange(ref refreshInFlight, 1, 0) == 0;

    public void ExitRefresh()
        => Interlocked.Exchange(ref refreshInFlight, 0);

    public DeviceRefreshUpdate Apply(
        DeviceSnapshot[] nextSnapshot,
        bool forcePublish,
        DateTimeOffset refreshedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(nextSnapshot);

        lock (gate)
        {
            lastRefreshUtc = refreshedAtUtc;
            var previousById = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in devicesSnapshot)
            {
                if (!string.IsNullOrWhiteSpace(item.DeviceId))
                {
                    previousById[item.DeviceId] = item;
                }
            }

            var normalizedSnapshot = NormalizeForUi(nextSnapshot, previousById);
            var nonOnlinePresent = normalizedSnapshot.Any(static d => d.Status != DeviceStatus.Online);

            if (!forcePublish && !nonOnlinePresent && AreSnapshotsEquivalent(devicesSnapshot, normalizedSnapshot))
            {
                return new DeviceRefreshUpdate(false, Array.Empty<DeviceSnapshot>(), devicesSnapshot.ToArray());
            }

            var previousSnapshot = devicesSnapshot.ToArray();
            devicesSnapshot.Clear();
            devicesSnapshot.AddRange(normalizedSnapshot);
            return new DeviceRefreshUpdate(true, previousSnapshot, devicesSnapshot.ToArray());
        }
    }

    private static DeviceSnapshot[] NormalizeForUi(
        DeviceSnapshot[] nextSnapshot,
        Dictionary<string, DeviceSnapshot> previousById)
    {
        var normalized = new DeviceSnapshot[nextSnapshot.Length];
        for (var i = 0; i < nextSnapshot.Length; i++)
        {
            var current = nextSnapshot[i];
            previousById.TryGetValue(current.DeviceId, out var previousSnapshot);
            var normalizedConnectivityEvent = DeviceConnectivityEventClassifier.NormalizeForUi(
                current.LastWifiEvent,
                previousSnapshot?.LastWifiEvent);

            normalized[i] = string.Equals(normalizedConnectivityEvent, current.LastWifiEvent, StringComparison.OrdinalIgnoreCase)
                ? current
                : CloneWithConnectivityEvent(current, normalizedConnectivityEvent);
        }

        return normalized;
    }

    private static DeviceSnapshot CloneWithConnectivityEvent(DeviceSnapshot source, string? lastWifiEvent)
    {
        return new DeviceSnapshot
        {
            DeviceId = source.DeviceId,
            Name = source.Name,
            Profile = source.Profile,
            Status = source.Status,
            ControlPlaneState = source.ControlPlaneState,
            IsRegistered = source.IsRegistered,
            LastSeenUtc = source.LastSeenUtc,
            FirstSeenUtc = source.FirstSeenUtc,
            LastTelemetryUtc = source.LastTelemetryUtc,
            LastAuthUtc = source.LastAuthUtc,
            ConfigState = source.ConfigState,
            LastKnownIp = source.LastKnownIp,
            LastKnownRssi = source.LastKnownRssi,
            UptimeSeconds = source.UptimeSeconds,
            LoopHealthyPercent = source.LoopHealthyPercent,
            LoopLoadPercent = source.LoopLoadPercent,
            ChipTemperatureCelsius = source.ChipTemperatureCelsius,
            FreeHeapBytes = source.FreeHeapBytes,
            LargestHeapBlockBytes = source.LargestHeapBlockBytes,
            PsramAvailable = source.PsramAvailable,
            FreePsramBytes = source.FreePsramBytes,
            LargestPsramBlockBytes = source.LargestPsramBlockBytes,
            WifiConnected = source.WifiConnected,
            WifiState = source.WifiState,
            ProvisioningPortalActive = source.ProvisioningPortalActive,
            AuxLedAvailable = source.AuxLedAvailable,
            TestLedAvailable = source.TestLedAvailable,
            LastWifiEvent = lastWifiEvent,
            StreamLastSequence = source.StreamLastSequence,
            StreamFramesReceived = source.StreamFramesReceived,
            StreamFramesApplied = source.StreamFramesApplied,
            Hub75PresentFrames = source.Hub75PresentFrames,
            StreamSequenceGapCount = source.StreamSequenceGapCount,
            StreamInvalidFrameCount = source.StreamInvalidFrameCount,
            FirmwareVersion = source.FirmwareVersion,
            TelemetrySequence = source.TelemetrySequence,
            BrightnessCap = source.BrightnessCap,
            BrightnessRequested = source.BrightnessRequested,
            BrightnessApplied = source.BrightnessApplied,
            TestLedEnabled = source.TestLedEnabled,
            TestLedDuty = source.TestLedDuty,
            ActiveAppId = source.ActiveAppId,
            ActiveAppName = source.ActiveAppName,
            BoardModel = source.BoardModel,
            PanelType = source.PanelType,
            AnimatedWebpBatchSupported = source.AnimatedWebpBatchSupported,
            VisualUdpSupported = source.VisualUdpSupported,
            VisualUdpPort = source.VisualUdpPort,
            VisualUdpMode = source.VisualUdpMode,
            ChipModel = source.ChipModel,
            ChipRevision = source.ChipRevision,
            ChipCores = source.ChipCores,
            CpuFreqMHz = source.CpuFreqMHz,
            SdkVersion = source.SdkVersion,
            HeapTotalBytes = source.HeapTotalBytes,
            PsramTotalBytes = source.PsramTotalBytes,
            FlashTotalBytes = source.FlashTotalBytes,
            SketchSizeBytes = source.SketchSizeBytes,
            FreeSketchBytes = source.FreeSketchBytes,
        };
    }

    private static bool AreSnapshotsEquivalent(List<DeviceSnapshot> current, DeviceSnapshot[] next)
    {
        if (current.Count != next.Length)
        {
            return false;
        }

        for (var i = 0; i < current.Count; i++)
        {
            var a = current[i];
            var b = next[i];
            if (!string.Equals(a.DeviceId, b.DeviceId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                || a.Status != b.Status
                || !string.Equals(a.Profile, b.Profile, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.FirmwareVersion, b.FirmwareVersion, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.LastKnownIp, b.LastKnownIp, StringComparison.OrdinalIgnoreCase)
                || a.LastKnownRssi != b.LastKnownRssi
                || a.UptimeSeconds != b.UptimeSeconds
                || a.LoopHealthyPercent != b.LoopHealthyPercent
                || a.LoopLoadPercent != b.LoopLoadPercent
                || a.ChipTemperatureCelsius != b.ChipTemperatureCelsius
                || a.FreeHeapBytes != b.FreeHeapBytes
                || a.LargestHeapBlockBytes != b.LargestHeapBlockBytes
                || a.PsramAvailable != b.PsramAvailable
                || a.FreePsramBytes != b.FreePsramBytes
                || a.LargestPsramBlockBytes != b.LargestPsramBlockBytes
                || a.WifiConnected != b.WifiConnected
                || !string.Equals(a.WifiState, b.WifiState, StringComparison.OrdinalIgnoreCase)
                || a.ProvisioningPortalActive != b.ProvisioningPortalActive
                || a.AuxLedAvailable != b.AuxLedAvailable
                || a.TestLedAvailable != b.TestLedAvailable
                || !string.Equals(a.LastWifiEvent, b.LastWifiEvent, StringComparison.OrdinalIgnoreCase)
                || a.StreamLastSequence != b.StreamLastSequence
                || a.StreamFramesReceived != b.StreamFramesReceived
                || a.StreamFramesApplied != b.StreamFramesApplied
                || a.Hub75PresentFrames != b.Hub75PresentFrames
                || a.StreamSequenceGapCount != b.StreamSequenceGapCount
                || a.StreamInvalidFrameCount != b.StreamInvalidFrameCount
                || a.TelemetrySequence != b.TelemetrySequence
                || a.BrightnessCap != b.BrightnessCap
                || a.BrightnessRequested != b.BrightnessRequested
                || a.BrightnessApplied != b.BrightnessApplied
                || a.TestLedEnabled != b.TestLedEnabled
                || a.TestLedDuty != b.TestLedDuty
                || !string.Equals(a.ActiveAppId, b.ActiveAppId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.ActiveAppName, b.ActiveAppName, StringComparison.Ordinal)
                || !string.Equals(a.BoardModel, b.BoardModel, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.PanelType, b.PanelType, StringComparison.OrdinalIgnoreCase)
                || a.AnimatedWebpBatchSupported != b.AnimatedWebpBatchSupported
                || a.VisualUdpSupported != b.VisualUdpSupported
                || a.VisualUdpPort != b.VisualUdpPort
                || !string.Equals(a.VisualUdpMode, b.VisualUdpMode, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.ChipModel, b.ChipModel, StringComparison.OrdinalIgnoreCase)
                || a.ChipRevision != b.ChipRevision
                || a.ChipCores != b.ChipCores
                || a.CpuFreqMHz != b.CpuFreqMHz
                || !string.Equals(a.SdkVersion, b.SdkVersion, StringComparison.OrdinalIgnoreCase)
                || a.HeapTotalBytes != b.HeapTotalBytes
                || a.PsramTotalBytes != b.PsramTotalBytes
                || a.FlashTotalBytes != b.FlashTotalBytes
                || a.SketchSizeBytes != b.SketchSizeBytes
                || a.FreeSketchBytes != b.FreeSketchBytes
                || a.IsRegistered != b.IsRegistered
                || a.FirstSeenUtc != b.FirstSeenUtc
                || a.LastTelemetryUtc != b.LastTelemetryUtc
                || a.LastAuthUtc != b.LastAuthUtc
                || a.ConfigState != b.ConfigState)
            {
                return false;
            }
        }

        return true;
    }
}
