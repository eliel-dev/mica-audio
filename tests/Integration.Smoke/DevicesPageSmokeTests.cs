using System.Reflection;
using App.WinUI.Views;
using Device.Protocol.Models;

namespace Integration.Smoke;

public sealed class DevicesPageSmokeTests
{
    private static readonly string[] ReorderedSelectionDeviceIds = ["device-c", "device-b", "device-a"];
    private static readonly string[] CaseInsensitiveSelectionDeviceIds = ["device-b", "device-a"];
    private static readonly string[] RemovedSelectionDeviceIds = ["device-a", "device-c"];

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
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardStatusSectionBorder", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardNetworkText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardUptimeText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardPortalStateText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardLastEventText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("DashboardAuxLedText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("StreamFramesReceivedText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("StreamFramesAppliedText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("StreamSuccessRateText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("StreamGapCountText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("StreamInvalidCountText", flags));
        Assert.NotNull(typeof(DevicesPage).GetField("StreamLastSequenceText", flags));
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

    [Fact]
    public void DevicesPageShouldRouteOnlineSelectionWithoutTelemetryToPendingFallbackGuard()
    {
        const BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags formatterFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var waitingSnapshot = new DeviceSnapshot
        {
            DeviceId = "online-device",
            Name = "Online Device",
            Status = DeviceStatus.Online,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastAuthUtc = DateTimeOffset.UtcNow,
        };

        var readySnapshot = new DeviceSnapshot
        {
            DeviceId = "online-device",
            Name = "Online Device",
            Status = DeviceStatus.Online,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastAuthUtc = DateTimeOffset.UtcNow,
            LastTelemetryUtc = DateTimeOffset.UtcNow,
            LoopLoadPercent = 14,
            WifiConnected = true,
        };

        var formatterType = typeof(DevicesPage).Assembly.GetType("App.WinUI.Services.Devices.DeviceMetricsFormatter");
        Assert.NotNull(formatterType);

        var buildMethod = formatterType!.GetMethod("Build", formatterFlags);
        Assert.NotNull(buildMethod);

        var waitingMetrics = buildMethod!.Invoke(null, new object?[] { waitingSnapshot });
        var readyMetrics = buildMethod.Invoke(null, new object?[] { readySnapshot });

        var pendingMethod = typeof(DevicesPage).GetMethod("ShouldUsePendingTelemetryFallback", staticFlags);
        Assert.NotNull(pendingMethod);

        var waitingResult = pendingMethod!.Invoke(null, new[] { (object?)true, waitingSnapshot, waitingMetrics });
        var readyResult = pendingMethod.Invoke(null, new[] { (object?)true, readySnapshot, readyMetrics });

        Assert.IsType<bool>(waitingResult);
        Assert.IsType<bool>(readyResult);
        Assert.True((bool)waitingResult!);
        Assert.False((bool)readyResult!);
    }

    [Fact]
    public void DevicesPageShouldRetainSelectionByDeviceIdAcrossRefreshes()
    {
        const BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;

        var retainMethod = typeof(DevicesPage).GetMethod("ResolveRetainedSelectionDeviceId", staticFlags);
        Assert.NotNull(retainMethod);

        var reordered = retainMethod!.Invoke(null, [ "device-b", ReorderedSelectionDeviceIds ]);
        var caseInsensitive = retainMethod.Invoke(null, [ "DEVICE-B", CaseInsensitiveSelectionDeviceIds ]);
        var removed = retainMethod.Invoke(null, [ "device-b", RemovedSelectionDeviceIds ]);

        Assert.Equal("device-b", reordered);
        Assert.Equal("DEVICE-B", caseInsensitive);
        Assert.Null(removed);
    }

    [Fact]
    public void DevicesPageShouldFormatSafeOnlineStatusLabels()
    {
        const BindingFlags staticFlags = BindingFlags.NonPublic | BindingFlags.Static;

        var snapshot = new DeviceSnapshot
        {
            DeviceId = "safe-online-device",
            Status = DeviceStatus.Online,
            ProvisioningPortalActive = false,
            AuxLedAvailable = true,
            StreamFramesReceived = 240,
            StreamFramesApplied = 228,
        };

        var portalMethod = typeof(DevicesPage).GetMethod("BuildProvisioningPortalLabel", staticFlags);
        var auxLedMethod = typeof(DevicesPage).GetMethod("BuildAuxLedAvailabilityLabel", staticFlags);
        var successRateMethod = typeof(DevicesPage).GetMethod("BuildStreamSuccessRateLabel", staticFlags);

        Assert.NotNull(portalMethod);
        Assert.NotNull(auxLedMethod);
        Assert.NotNull(successRateMethod);

        Assert.Equal("Inativo", portalMethod!.Invoke(null, new object?[] { snapshot }));
        Assert.Equal("Disponivel", auxLedMethod!.Invoke(null, new object?[] { snapshot }));
        Assert.Equal("95%", successRateMethod!.Invoke(null, new object?[] { snapshot }));
        Assert.Equal("-", successRateMethod.Invoke(null, new object?[] { null }));
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
