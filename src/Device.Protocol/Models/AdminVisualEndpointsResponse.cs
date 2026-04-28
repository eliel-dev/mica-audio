namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md
public sealed class AdminVisualEndpointsResponse
{
    public IReadOnlyList<DeviceVisualEndpointInfo> Devices { get; init; } = [];
}
