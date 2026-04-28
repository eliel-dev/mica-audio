namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md
public sealed class DeviceVisualEndpointInfo
{
    public string DeviceId { get; init; } = string.Empty;

    public string LanIpAddress { get; init; } = string.Empty;

    public int VisualUdpPort { get; init; }

    public string VisualUdpMode { get; init; } = string.Empty;

    public string DeviceToken { get; init; } = string.Empty;

    public string? FirmwareVersion { get; init; }

    public bool StreamSocketConnected { get; init; }

    public DeviceControlPlaneState ControlPlaneState { get; init; }
}
