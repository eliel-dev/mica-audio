namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#zero-code-lan-onboarding
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public sealed class MicaDiscoveryResponseV1
{
    public string Protocol { get; init; } = "mica.discovery.v1";

    public string DeviceId { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string HttpBase { get; init; } = string.Empty;

    public string MqttHost { get; init; } = string.Empty;

    public int MqttPort { get; init; }

    public string MqttRootTopic { get; init; } = string.Empty;

    public string WsPath { get; init; } = "/ws/v1/stream";

    public int VisualUdpPort { get; init; }
}
