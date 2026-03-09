namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#contrato-mqtt-de-controle
public sealed class DevicePresenceMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public string State { get; init; } = "offline";
}
