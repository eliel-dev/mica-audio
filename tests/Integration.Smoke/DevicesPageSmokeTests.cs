using System.Reflection;
using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class DevicesPageSmokeTests
{
    [Fact]
    public void DevicesPageShouldDeclareDashboardAndDeviceLogsFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetField("DashboardStatusText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardPlaceholderText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardMetricsGrid", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardLoopLoadBar", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceLogsTextBox", flags));
    }

    [Fact]
    public void DevicesPageShouldKeepSelectionDetailsHandler()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.NotNull(typeof(DevicesPage).GetMethod("ApplySelectionDetails", flags));
    }
}
