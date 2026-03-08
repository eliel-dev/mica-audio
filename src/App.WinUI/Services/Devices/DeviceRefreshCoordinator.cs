using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

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
            var nonOnlinePresent = nextSnapshot.Any(static d => d.Status != DeviceStatus.Online);

            if (!forcePublish && !nonOnlinePresent && AreSnapshotsEquivalent(devicesSnapshot, nextSnapshot))
            {
                return new DeviceRefreshUpdate(false, Array.Empty<DeviceSnapshot>(), devicesSnapshot.ToArray());
            }

            var previousSnapshot = devicesSnapshot.ToArray();
            devicesSnapshot.Clear();
            devicesSnapshot.AddRange(nextSnapshot);
            return new DeviceRefreshUpdate(true, previousSnapshot, devicesSnapshot.ToArray());
        }
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
                || a.LoopLoadPercent != b.LoopLoadPercent
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
