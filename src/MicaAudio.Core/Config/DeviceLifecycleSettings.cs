using MicaAudio.Core.Presets;

namespace MicaAudio.Core.Config;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#thresholds-de-presence-de-device
internal readonly record struct DeviceLifecycleSettings(
    int DeviceFreshThresholdSeconds,
    int DeviceStaleThresholdMinutes,
    int DeviceDormantThresholdHours)
{
    public static DeviceLifecycleSettings From(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Normalize(
            settings.DeviceFreshThresholdSeconds,
            settings.DeviceStaleThresholdMinutes,
            settings.DeviceDormantThresholdHours);
    }

    public static DeviceLifecycleSettings Normalize(
        int freshThresholdSeconds,
        int staleThresholdMinutes,
        int dormantThresholdHours)
    {
        var normalizedFresh = freshThresholdSeconds <= 0
            ? 15
            : Math.Clamp(freshThresholdSeconds, 5, 120);
        var normalizedStale = staleThresholdMinutes <= 0
            ? 2
            : Math.Clamp(staleThresholdMinutes, 1, 240);
        var normalizedDormant = dormantThresholdHours <= 0
            ? 24
            : Math.Clamp(dormantThresholdHours, 1, 720);

        var fresh = TimeSpan.FromSeconds(normalizedFresh);
        var stale = TimeSpan.FromMinutes(normalizedStale);
        var dormant = TimeSpan.FromHours(normalizedDormant);

        if (fresh >= stale)
        {
            stale = fresh + TimeSpan.FromMinutes(1);
            normalizedStale = Math.Clamp((int)Math.Ceiling(stale.TotalMinutes), 1, 240);
            stale = TimeSpan.FromMinutes(normalizedStale);
        }

        if (stale >= dormant)
        {
            dormant = stale + TimeSpan.FromHours(1);
            normalizedDormant = Math.Clamp((int)Math.Ceiling(dormant.TotalHours), 1, 720);
        }

        return new DeviceLifecycleSettings(normalizedFresh, normalizedStale, normalizedDormant);
    }
}
