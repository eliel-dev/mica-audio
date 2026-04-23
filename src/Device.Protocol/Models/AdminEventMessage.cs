namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed class AdminEventMessage
{
    public string Type { get; init; } = string.Empty;

    public IReadOnlyList<DeviceSnapshot>? Devices { get; init; }

    public DeviceLogMessage? DeviceLog { get; init; }

    public DeviceCommandProgressMessage? CommandProgress { get; init; }

    public DateTimeOffset Utc { get; init; }
}
