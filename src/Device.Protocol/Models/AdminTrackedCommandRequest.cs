namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed class AdminTrackedCommandRequest
{
    public DeviceCommandType CommandType { get; init; }

    public Dictionary<string, string>? Parameters { get; init; }

    public int TimeoutMs { get; init; } = 5000;
}
