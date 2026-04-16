using Device.Protocol.Models;

namespace App.WinUI.Infrastructure.Serial;

// DOCS: docs/wiki/guides/setup-new-device.md#logs-seriais-no-wizard
internal static class WizardSerialMonitorPolicy
{
    public static bool ShouldAutoStop(
        bool wizardCompletedSuccessfully,
        bool autoStopRequested,
        string? targetDeviceId,
        DeviceSnapshot? snapshot)
    {
        if (!wizardCompletedSuccessfully
            || autoStopRequested
            || string.IsNullOrWhiteSpace(targetDeviceId)
            || snapshot?.IsConnected != true)
        {
            return false;
        }

        return string.Equals(snapshot.DeviceId, targetDeviceId, StringComparison.OrdinalIgnoreCase);
    }
}
