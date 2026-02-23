namespace Device.Protocol.Contracts;

// DOCS: docs/wiki/modules/device-server-protocol.md#politicas-de-seguranca
public sealed class ServerConfig
{
    public string ListenHost { get; init; } = "0.0.0.0";

    public int Port { get; init; } = 5272;

    public int MaxDevices { get; init; } = 5;

    public string MdnsServiceName { get; init; } = "_micaaudio._tcp";

    public string PublicHost { get; init; } = "micaaudio.local";

    // Security-first defaults for local network usage.
    public bool RestrictToPrivateNetworks { get; init; } = true;

    public string[] AllowedCidrs { get; init; } = Array.Empty<string>();

    public int PairRequestsPerMinute { get; init; } = 20;

    public int CommandAckRequestsPerSecond { get; init; } = 40;

    public int WebSocketHandshakesPerMinute { get; init; } = 40;

    public int PairingAttemptsPerWindow { get; init; } = 12;

    public int PairingAttemptWindowSeconds { get; init; } = 60;
}
