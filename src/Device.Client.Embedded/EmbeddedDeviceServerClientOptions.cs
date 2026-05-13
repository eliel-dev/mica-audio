namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public sealed class EmbeddedDeviceServerClientOptions
{
    public string ListenHost { get; init; } = "0.0.0.0";

    public int HttpPort { get; init; } = 5272;

    public int MqttPort { get; init; } = 5273;

    public int MaxDevices { get; init; } = 5;

    public string MdnsServiceName { get; init; } = "_micaaudio._tcp";

    public string MqttRootTopic { get; init; } = "mica/v1/devices";

    public bool TrustedLanAutoRegistration { get; init; } = true;

    public int DiscoveryUdpPort { get; init; } = 5275;

    public long MaxMediaUploadBytes { get; init; } = 20L * 1024L * 1024L;
}
