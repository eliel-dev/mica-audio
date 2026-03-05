using System.Reflection;
using App.WinUI.Views;
using Device.Protocol.Models;

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
        Assert.NotNull(typeof(DevicesPage).GetField("EspDashSectionBorder", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("ConnectivitySectionBorder", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DeviceLogsTextBox", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("NewDeviceButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardOverlay", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardPortPanel", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardPortComboBox", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardRefreshPortsButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFinishButton", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFlashProgressHost", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFlashProgressBar", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("WizardFlashPercentText", flags));

        Assert.Null(typeof(DevicesPage).GetField("WizardPageOnePanel", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardPageTwoPanel", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardBackButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("WizardNextButton", flags));
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
        Assert.Null(typeof(DevicesPage).GetField("DownloadFirmwareButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("PairDeviceButton", flags));
        Assert.Null(typeof(DevicesPage).GetField("UpdateFirmwareButton", flags));
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

    [Fact]
    public void DevicesPageShouldRouteOfflineSelectionToFallbackGuard()
    {
        const BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;

        var offlineSnapshot = new DeviceSnapshot
        {
            DeviceId = "offline-device",
            Name = "Offline Device",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-4),
            LastTelemetryUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastKnownRssi = -72,
            UptimeSeconds = 124,
            WifiConnected = false,
        };

        var shouldFallbackMethod = typeof(DevicesPage).GetMethod("ShouldUseOfflineDashboardFallback", staticFlags);
        Assert.NotNull(shouldFallbackMethod);

        var result = shouldFallbackMethod!.Invoke(null, new object?[] { true, offlineSnapshot });
        Assert.IsType<bool>(result);
        Assert.True((bool)result!);

        var signatureMethod = typeof(DevicesPage).GetMethod("BuildOfflineDashboardSignature", staticFlags);
        Assert.NotNull(signatureMethod);

        var signature = signatureMethod!.Invoke(null, new object?[] { offlineSnapshot.DeviceId, offlineSnapshot });
        Assert.NotNull(signature);
        Assert.Contains("offline|", signature!.ToString(), StringComparison.Ordinal);
        Assert.Contains(offlineSnapshot.DeviceId, signature.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
