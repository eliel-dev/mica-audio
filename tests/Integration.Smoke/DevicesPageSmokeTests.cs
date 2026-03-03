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
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardConnectionChipText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardConnectionChipIcon", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardWifiChipIcon", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardRssiChipIcon", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardLoopTrendGrid", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardLoopTrendBars", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceLogsTextBox", flags));
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
