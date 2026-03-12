using System.Reflection;
using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class DevicesPageSmokeTests
{
    [Fact]
    public void DevicesPageShouldDeclareEmbeddedDashboardAndWizardFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetField("DevicesList", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("NewDeviceButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceDetailsGrid", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceDashboardWebView", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardOverlay", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardPortPanel", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardPortComboBox", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardRefreshPortsButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFinishButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFlashProgressHost", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFlashProgressBar", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFlashPercentText", flags));

        Assert.Null(typeof(DevicesPage).GetField("DeviceDetailsTabView", flags));
        Assert.Null(typeof(DevicesPage).GetField("StatisticsContentPanel", flags));
        Assert.Null(typeof(DevicesPage).GetField("DeviceLogsHeaderText", flags));
        Assert.Null(typeof(DevicesPage).GetField("DeviceLogsSearchBox", flags));
    }

    [Fact]
    public void DevicesPageShouldKeepWebViewDashboardBridgeMethods()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;

        Assert.NotNull(typeof(DevicesPage).GetMethod("PushDashboardSelectionAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("EnsureDashboardWebViewReadyAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("DetachDashboardWebViewBridge", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("OnDashboardWebMessageReceived", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("BuildDashboardWebViewUri", staticFlags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ApplySelectionDetails", flags));

        Assert.Null(typeof(DevicesPage).GetMethod("BuildDetailsTabHost", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("ApplyStatisticsPanel", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("UpdateStructuredDeviceLogs", flags));
    }

    [Fact]
    public void BuildDashboardWebViewUri_ShouldForceLoopbackDashboardEndpoint()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

        var method = typeof(DevicesPage).GetMethod("BuildDashboardWebViewUri", flags);
        Assert.NotNull(method);

        var uri = method!.Invoke(null, ["http://192.168.1.50:5272"]) as Uri;

        Assert.NotNull(uri);
        Assert.Equal("127.0.0.1", uri!.Host);
        Assert.Equal(5272, uri.Port);
        Assert.Equal("/dashboard", uri.AbsolutePath);
        Assert.Equal("?embedded=1", uri.Query);
    }

    [Fact]
    public void DevicesPageShouldRouteDashboardActionsThroughAsyncHelpers()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetMethod("ExecuteTestLedAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ExecuteSetBrightnessAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ExecuteRemoveDeviceAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ResolveCommandDevice", flags));
    }

    [Fact]
    public void DevicesPageShouldKeepPreviewPumpMethods()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetMethod("StartPreviewPump", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("StopPreviewPump", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("OnPreviewPumpTick", flags));
    }

    [Fact]
    public void DevicesPageShouldKeepOnboardingWorkflowMethods()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetMethod("ShowNewDeviceWizardAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("RefreshWizardPortsAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("RunWizardOnboardingAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("SaveFirmwareAsync", flags));
    }
}
