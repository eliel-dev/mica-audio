namespace Device.Protocol.Models;

public sealed class PairDeviceResponse
{
    // DOCS: docs/wiki/modules/device-server-protocol.md#contrato-mqtt-de-controle
    public string DeviceId { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string WsPath { get; init; } = "/ws/v1/stream";

    public string HttpBase { get; init; } = string.Empty;

    public string MqttHost { get; init; } = string.Empty;

    public int MqttPort { get; init; } = 5273;

    public string MqttRootTopic { get; init; } = "mica/v1/devices";

    public string MdnsService { get; init; } = "_micaaudio._tcp";
}
