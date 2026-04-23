namespace Device.Protocol.Contracts;

// DOCS: docs/wiki/modules/device-server-protocol.md#politicas-de-seguranca
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-22-micaudio-server-docker-advertised-endpoints.md
public sealed class ServerConfig
{
    public string ListenHost { get; init; } = "0.0.0.0";

    public int Port { get; init; } = 5272;

    public int MqttPort { get; init; } = 5273;

    public int MaxDevices { get; init; } = 5;

    public string MdnsServiceName { get; init; } = "_micaaudio._tcp";

    public string PublicHost { get; init; } = "micaaudio.local";

    public string PublicHttpBaseAddress { get; init; } = string.Empty;

    public string MqttRootTopic { get; init; } = "mica/v1/devices";

    public string AdminToken { get; init; } = string.Empty;

    // Security-first defaults for local network usage.
    public bool RestrictToPrivateNetworks { get; init; } = true;

    public string[] AllowedCidrs { get; init; } = Array.Empty<string>();

    public int PairRequestsPerMinute { get; init; } = 20;

    public int CommandAckRequestsPerSecond { get; init; } = 40;

    public int WebSocketHandshakesPerMinute { get; init; } = 40;

    public int PairingAttemptsPerWindow { get; init; } = 12;

    public int PairingAttemptWindowSeconds { get; init; } = 60;

    public int DeviceFreshThresholdSeconds { get; init; } = 15;

    // Transitional compatibility for firmware that still sends token via query string on WebSocket handshake.
    public bool AllowLegacyWebSocketQueryToken { get; init; }

    // Hard limits for incoming request/message payloads.
    public long MaxJsonBodyBytes { get; init; } = 64L * 1024L;

    public int MaxWebSocketMessageBytes { get; init; } = 64 * 1024;
}
