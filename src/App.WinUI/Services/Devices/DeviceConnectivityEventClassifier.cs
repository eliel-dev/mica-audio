namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#render-estavel-na-devicespage
internal static class DeviceConnectivityEventClassifier
{
    public static bool IsWebSocketConnectivityEvent(string? eventName)
        => !string.IsNullOrWhiteSpace(eventName)
            && eventName.Trim().StartsWith("ws_", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldSurfaceConnectivityEvent(string? eventName)
        => !string.IsNullOrWhiteSpace(eventName) && !IsWebSocketConnectivityEvent(eventName);

    public static string? NormalizeForUi(string? eventName, string? previousVisibleEvent = null)
    {
        if (!IsWebSocketConnectivityEvent(eventName))
        {
            return string.IsNullOrWhiteSpace(eventName)
                ? null
                : eventName.Trim();
        }

        return ShouldSurfaceConnectivityEvent(previousVisibleEvent)
            ? previousVisibleEvent!.Trim()
            : null;
    }
}
