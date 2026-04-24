namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
public sealed class DeviceCommandRequest
{
    public string CommandId { get; init; } = Guid.NewGuid().ToString("N");

    public string DeviceId { get; init; } = string.Empty;

    public DeviceCommandType CommandType { get; init; }

    // DOCS: docs/wiki/guides/add-device-command.md#passos
    public IDictionary<string, string>? Parameters { get; init; }

    public DeviceCommandSessionContext? Session { get; init; }
}
