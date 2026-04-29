using System.Reflection;
using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class DevicesPageSmokeTests
{
    [Fact]
    public void DevicesPageShouldDeclareEmbeddedDashboardAndPairingFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetField("DevicesList", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceDetailsGrid", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceDashboardWebView", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("CopyDashboardLinkButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("PairingCodeText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("PairingCopyCodeButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("RemoveDeviceButton", flags));

        Assert.Null(typeof(DevicesPage).GetField("ReprovisionWifiButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("NewDeviceButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardOverlay", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardPortPanel", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardPortComboBox", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardRefreshPortsButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardDetailsToggleButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardDiagnosticsPanel", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardSerialStatusText", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardRecaptureBootButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardCopySerialLogsButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardClearSerialLogsButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardSerialListView", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardSerialPlaceholderText", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardServerBaseAddressText", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardFinishButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardFlashProgressHost", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardFlashProgressBar", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardFlashPercentText", flags));

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
        Assert.NotNull(typeof(DevicesPage).GetMethod("BuildDashboardShareUri", staticFlags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("OnCopyDashboardLinkClicked", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("OnRemoveDeviceClicked", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ApplySelectionDetails", flags));

        Assert.Null(typeof(DevicesPage).GetMethod("OnReprovisionWifiClicked", flags));
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
    public void BuildDashboardShareUri_ShouldKeepLanHostAndPinSelectedDevice()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

        var method = typeof(DevicesPage).GetMethod("BuildDashboardShareUri", flags);
        Assert.NotNull(method);

        var uri = method!.Invoke(null, ["http://192.168.1.50:5272", "mp-device-01"]) as Uri;

        Assert.NotNull(uri);
        Assert.Equal("192.168.1.50", uri!.Host);
        Assert.Equal(5272, uri.Port);
        Assert.Equal("/dashboard", uri.AbsolutePath);
        Assert.Equal("?deviceId=mp-device-01", uri.Query);
    }

    [Fact]
    public void BuildDashboardShareUri_ShouldRejectLoopbackHosts()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

        var method = typeof(DevicesPage).GetMethod("BuildDashboardShareUri", flags);
        Assert.NotNull(method);

        var uri = method!.Invoke(null, ["http://127.0.0.1:5272", "mp-device-01"]) as Uri;

        Assert.Null(uri);
    }

    [Fact]
    public void DevicesPageShouldRouteDashboardActionsThroughAsyncHelpers()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetMethod("ExecuteTestLedAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ExecuteSetBrightnessAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ExecuteRemoveDeviceAsync", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ExecuteUpdateFirmwareAsync", flags));
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
    public void DevicesPageShouldNotExposeUsbOnboardingWorkflowMethods()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetMethod("SaveFirmwareAsync", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("ShowNewDeviceWizardAsync", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("ShowUsbFirmwareRefreshWizardAsync", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("PrepareWizardSerialMonitorAsync", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("BeginWizardSerialMonitoringAsync", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("RecaptureWizardBootAsync", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("CopyWizardSerialLogsToClipboard", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("EvaluateWizardSerialAutoStop", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("RefreshWizardPortsAsync", flags));
        Assert.Null(typeof(DevicesPage).GetMethod("RunWizardOnboardingAsync", flags));
    }

    [Fact]
    public void DevicesPageConstructor_ShouldNotRequireUsbOnboardingServices()
    {
        var constructors = typeof(DevicesPage).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
        var ctor = Assert.Single(constructors);
        var parameterTypes = ctor.GetParameters().Select(parameter => parameter.ParameterType.FullName).ToArray();

        Assert.DoesNotContain("App.WinUI.Infrastructure.Serial.ISerialPortCatalogService", parameterTypes);
        Assert.DoesNotContain("App.WinUI.Infrastructure.Serial.ISerialMonitorService", parameterTypes);
        Assert.DoesNotContain("App.WinUI.Services.Devices.Onboarding.IDeviceUsbOnboardingService", parameterTypes);
    }

    [Fact]
    public void DevicesPageShouldExposeInlinePairingBannerControls()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(DevicesPage).GetField("PairingCodeText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("PairingCopyCodeButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("BuildInlineStatusBanner", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ShowInlineStatusMessage", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("ShowPairingCodeNotice", flags));
        Assert.NotNull(typeof(DevicesPage).GetMethod("OnCopyPairingCodeClicked", flags));
    }

    [Fact]
    public void BuildPairingCodeBannerMessage_ShouldIncludeCodeExpiryAndUsageHint()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

        var method = typeof(DevicesPage).GetMethod("BuildPairingCodeBannerMessage", flags);
        Assert.NotNull(method);

        var message = method!.Invoke(null, ["ABC123", new DateTimeOffset(2026, 4, 18, 14, 30, 0, TimeSpan.Zero)]) as string;

        Assert.Equal(
            "Pareamento: ABC123 (expira 14:30:00 UTC). Use este codigo no pareamento do dispositivo.",
            message);
    }
}
