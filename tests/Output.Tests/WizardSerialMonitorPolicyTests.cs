using App.WinUI.Infrastructure.Serial;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class WizardSerialMonitorPolicyTests
{
    [Fact]
    public void ShouldAutoStop_ShouldReturnTrueWhenDiscoveredDeviceIsOnline()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "mp-device-01",
            Status = DeviceStatus.Online,
            ControlPlaneState = DeviceControlPlaneState.MqttOnline,
        };

        var shouldAutoStop = WizardSerialMonitorPolicy.ShouldAutoStop(
            wizardCompletedSuccessfully: true,
            autoStopRequested: false,
            targetDeviceId: "mp-device-01",
            snapshot);

        Assert.True(shouldAutoStop);
    }

    [Fact]
    public void ShouldAutoStop_ShouldReturnFalseWhenDeviceIsMissingOrOffline()
    {
        var missingTarget = WizardSerialMonitorPolicy.ShouldAutoStop(
            wizardCompletedSuccessfully: true,
            autoStopRequested: false,
            targetDeviceId: null,
            snapshot: null);

        var offlineSnapshot = new DeviceSnapshot
        {
            DeviceId = "mp-device-01",
            Status = DeviceStatus.Offline,
            ControlPlaneState = DeviceControlPlaneState.Offline,
        };
        var offlineResult = WizardSerialMonitorPolicy.ShouldAutoStop(
            wizardCompletedSuccessfully: true,
            autoStopRequested: false,
            targetDeviceId: "mp-device-01",
            offlineSnapshot);

        Assert.False(missingTarget);
        Assert.False(offlineResult);
    }
}
