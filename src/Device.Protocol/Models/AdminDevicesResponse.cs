namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed class AdminDevicesResponse
{
    public IReadOnlyList<DeviceSnapshot> Devices { get; init; } = Array.Empty<DeviceSnapshot>();
}
