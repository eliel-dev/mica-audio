namespace Device.Protocol.Models;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
public sealed class AutoRegisterDeviceResponse
{
    public string DeviceId { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string WsPath { get; init; } = "/ws/v1/stream";

    public string HttpBase { get; init; } = string.Empty;

    public string MqttHost { get; init; } = string.Empty;

    public int MqttPort { get; init; } = 5273;

    public string MqttRootTopic { get; init; } = "mica/v1/devices";

    public string MdnsService { get; init; } = "_micaaudio._tcp";

    /// <summary>
    /// True when an existing device record with the same MAC was reused
    /// (idempotent). False when a brand-new device record was created.
    /// </summary>
    public bool ReusedExisting { get; init; }
}
