namespace Device.Protocol.Models;

/// <summary>
/// Response from GET /api/v1/device/display-state.
/// Tells the ESP32 firmware which idle screen to show when no frames are
/// being pushed by the compositor or the WinUI client.
/// </summary>
public sealed class DeviceDisplayStateResponse
{
    /// <summary>
    /// One of: <c>"panel_active"</c>, <c>"no_mode_active"</c>, <c>"first_run"</c>.
    /// </summary>
    public string State { get; set; } = string.Empty;
}
