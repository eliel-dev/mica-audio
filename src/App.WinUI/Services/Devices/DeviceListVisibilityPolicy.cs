using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
internal static class DeviceListVisibilityPolicy
{
    public static DeviceSnapshot[] BuildVisibleList(IReadOnlyList<DeviceSnapshot> devices, DeviceLifecycleThresholds thresholds, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(devices);

        return devices
            .Where(static device => !string.IsNullOrWhiteSpace(device.DeviceId))
            .OrderBy(device => GetStatusSortKey(device, thresholds, nowUtc))
            .ThenBy(static device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static device => device.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int GetStatusSortKey(DeviceSnapshot device, DeviceLifecycleThresholds thresholds, DateTimeOffset nowUtc)
    {
        if (device.Status == DeviceStatus.Pairing)
        {
            return 4;
        }

        var presence = DeviceLifecyclePolicy.Build(device, thresholds, nowUtc);
        if (device.Status == DeviceStatus.Online && presence.CanRunCommands)
        {
            return 0;
        }

        if (presence.IsConfigUncertain)
        {
            return 2;
        }

        if (presence.IsNeverSeen)
        {
            return 3;
        }

        return 1;
    }
}
