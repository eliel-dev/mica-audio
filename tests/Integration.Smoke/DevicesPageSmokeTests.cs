using System.Reflection;
using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class DevicesPageSmokeTests
{
    [Fact]
    public void DevicesPageShouldDeclareDashboardAndDeviceLogsFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetField("SelectedDeviceSignalText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardPlaceholderText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardMetricsGrid", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardLoopLoadBar", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardBrightnessSlider", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardBrightnessStatusText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardTelemetryHeartbeatText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardLoopTrendGrid", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardLoopTrendBars", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceLogsTextBox", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("NewDeviceButton", flags));

        Assert.Null(typeof(DevicesPage).GetField("CommandStatusText", flags));
        Assert.Null(typeof(DevicesPage).GetField("DashboardConnectionChipText", flags));
        Assert.Null(typeof(DevicesPage).GetField("DashboardConnectivityStateText", flags));
        Assert.Null(typeof(DevicesPage).GetField("SearchBox", flags));
    }

    [Fact]
    public void DevicesPageShouldKeepSelectionDetailsHandler()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.NotNull(typeof(DevicesPage).GetMethod("ApplySelectionDetails", flags));
    }

    [Fact]
    public void DevicesPageShouldExposeOnlyTestLedAndRemoveActions()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetField("TestLedButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("RemoveDeviceButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("EnterProvisioningButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("RevokeButton", flags));
    }

    [Fact]
    public void DevicesPageShouldNotDeclareSelectedPreviewPanelFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.Null(typeof(DevicesPage).GetField("SelectedDevicePreview", flags));
        Assert.Null(typeof(DevicesPage).GetField("SelectedDevicePreviewPlaceholderText", flags));
    }
}
