using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class DeviceLifecycleSettingsTests
{
    [Fact]
    public void From_ShouldNormalizeThresholdOrder()
    {
        var settings = new AppSettings
        {
            DeviceFreshThresholdSeconds = 90,
            DeviceStaleThresholdMinutes = 1,
            DeviceDormantThresholdHours = 1,
        };

        var thresholds = DeviceLifecycleSettings.From(settings);

        Assert.Equal(90, thresholds.DeviceFreshThresholdSeconds);
        Assert.Equal(3, thresholds.DeviceStaleThresholdMinutes);
        Assert.Equal(1, thresholds.DeviceDormantThresholdHours);
    }
}
