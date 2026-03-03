using MicaAudio.Core.Presets;

// DOCS: docs/wiki/architecture/05-device-session-and-reconnect.md#thresholds-de-lifecycle
namespace App.WinUI.Services.Devices;

internal readonly record struct DeviceLifecycleThresholds(TimeSpan Fresh, TimeSpan Stale, TimeSpan Dormant)
{
    public static DeviceLifecycleThresholds Default => new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromHours(24));

    public static DeviceLifecycleThresholds FromSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new DeviceLifecycleThresholds(
            TimeSpan.FromSeconds(settings.DeviceFreshThresholdSeconds),
            TimeSpan.FromMinutes(settings.DeviceStaleThresholdMinutes),
            TimeSpan.FromHours(settings.DeviceDormantThresholdHours));
    }
}

