using Device.Protocol.Models;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#migracao-de-registro-de-devices
namespace App.WinUI.Services.Devices;

internal static class DeviceRegistryPresenceNormalizer
{
    public static bool NormalizeIsRegistered(bool? isRegistered)
    {
        return isRegistered ?? true;
    }

    public static DateTimeOffset? NormalizeFirstSeenUtc(DateTimeOffset? firstSeenUtc, DateTimeOffset lastSeenUtc, DateTimeOffset createdAtUtc)
    {
        return NormalizeTimestamp(firstSeenUtc)
            ?? NormalizeTimestamp(lastSeenUtc)
            ?? createdAtUtc;
    }

    public static DateTimeOffset? NormalizeLastAuthUtc(DateTimeOffset? lastAuthUtc, DateTimeOffset lastSeenUtc, DateTimeOffset createdAtUtc)
    {
        return NormalizeTimestamp(lastAuthUtc)
            ?? NormalizeTimestamp(lastSeenUtc)
            ?? createdAtUtc;
    }

    public static DeviceConfigState NormalizeConfigState(DeviceConfigState? configState)
    {
        return configState ?? DeviceConfigState.Unknown;
    }

    public static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? timestamp)
    {
        if (timestamp is null || timestamp.Value == default || timestamp.Value == DateTimeOffset.MinValue)
        {
            return null;
        }

        return timestamp;
    }
}

