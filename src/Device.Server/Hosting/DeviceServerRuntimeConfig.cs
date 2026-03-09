using System.Net;
using Device.Protocol.Contracts;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
internal sealed class DeviceServerRuntimeConfig
{
    private DeviceServerRuntimeConfig(
        string listenHost,
        int port,
        int mqttPort,
        int maxDevices,
        string mdnsServiceName,
        string publicHost,
        string mqttRootTopic,
        bool restrictToPrivateNetworks,
        IReadOnlyList<CidrRange> allowedCidrs,
        int configuredAllowedCidrsCount,
        int pairRequestsPerMinute,
        int commandAckRequestsPerSecond,
        int webSocketHandshakesPerMinute,
        int pairingAttemptsPerWindow,
        TimeSpan pairingAttemptWindow,
        TimeSpan deviceOfflineTimeout,
        bool allowLegacyWebSocketQueryToken,
        long maxJsonBodyBytes,
        int maxWebSocketMessageBytes)
    {
        ListenHost = listenHost;
        Port = port;
        MqttPort = mqttPort;
        MaxDevices = maxDevices;
        MdnsServiceName = mdnsServiceName;
        PublicHost = publicHost;
        MqttRootTopic = mqttRootTopic;
        RestrictToPrivateNetworks = restrictToPrivateNetworks;
        AllowedCidrs = allowedCidrs;
        ConfiguredAllowedCidrsCount = configuredAllowedCidrsCount;
        PairRequestsPerMinute = pairRequestsPerMinute;
        CommandAckRequestsPerSecond = commandAckRequestsPerSecond;
        WebSocketHandshakesPerMinute = webSocketHandshakesPerMinute;
        PairingAttemptsPerWindow = pairingAttemptsPerWindow;
        PairingAttemptWindow = pairingAttemptWindow;
        DeviceOfflineTimeout = deviceOfflineTimeout;
        AllowLegacyWebSocketQueryToken = allowLegacyWebSocketQueryToken;
        MaxJsonBodyBytes = maxJsonBodyBytes;
        MaxWebSocketMessageBytes = maxWebSocketMessageBytes;
    }

    public string ListenHost { get; }

    public int Port { get; }

    public int MqttPort { get; }

    public int MaxDevices { get; }

    public string MdnsServiceName { get; }

    public string PublicHost { get; }

    public string MqttRootTopic { get; }

    public bool RestrictToPrivateNetworks { get; }

    public IReadOnlyList<CidrRange> AllowedCidrs { get; }

    public int ConfiguredAllowedCidrsCount { get; }

    public bool HasConfiguredAllowedCidrs => ConfiguredAllowedCidrsCount > 0;

    public int PairRequestsPerMinute { get; }

    public int CommandAckRequestsPerSecond { get; }

    public int WebSocketHandshakesPerMinute { get; }

    public int PairingAttemptsPerWindow { get; }

    public TimeSpan PairingAttemptWindow { get; }

    public TimeSpan DeviceOfflineTimeout { get; }

    public bool AllowLegacyWebSocketQueryToken { get; }

    public long MaxJsonBodyBytes { get; }

    public int MaxWebSocketMessageBytes { get; }

    public static DeviceServerRuntimeConfig From(ServerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var allowedCidrs = ParseAllowedCidrs(config.AllowedCidrs);
        var configuredAllowedCidrsCount = config.AllowedCidrs?.Length ?? 0;

        return new DeviceServerRuntimeConfig(
            listenHost: config.ListenHost,
            port: config.Port,
            mqttPort: Math.Clamp(config.MqttPort, 1, 65535),
            maxDevices: config.MaxDevices,
            mdnsServiceName: config.MdnsServiceName,
            publicHost: config.PublicHost,
            mqttRootTopic: NormalizeRootTopic(config.MqttRootTopic),
            restrictToPrivateNetworks: config.RestrictToPrivateNetworks,
            allowedCidrs: allowedCidrs,
            configuredAllowedCidrsCount: configuredAllowedCidrsCount,
            pairRequestsPerMinute: Math.Max(1, config.PairRequestsPerMinute),
            commandAckRequestsPerSecond: Math.Max(1, config.CommandAckRequestsPerSecond),
            webSocketHandshakesPerMinute: Math.Max(1, config.WebSocketHandshakesPerMinute),
            pairingAttemptsPerWindow: Math.Max(1, config.PairingAttemptsPerWindow),
            pairingAttemptWindow: TimeSpan.FromSeconds(Math.Max(10, config.PairingAttemptWindowSeconds)),
            deviceOfflineTimeout: TimeSpan.FromSeconds(Math.Clamp(config.DeviceFreshThresholdSeconds, 5, 120)),
            allowLegacyWebSocketQueryToken: config.AllowLegacyWebSocketQueryToken,
            maxJsonBodyBytes: Math.Clamp(config.MaxJsonBodyBytes, 1024L, 1024L * 1024L),
            maxWebSocketMessageBytes: Math.Clamp(config.MaxWebSocketMessageBytes, 1024, 1024 * 1024));
    }

    private static string NormalizeRootTopic(string? rawRootTopic)
    {
        if (string.IsNullOrWhiteSpace(rawRootTopic))
        {
            return "mica/v1/devices";
        }

        var normalized = rawRootTopic.Trim().Trim('/');
        return string.IsNullOrWhiteSpace(normalized)
            ? "mica/v1/devices"
            : normalized;
    }

    private static IReadOnlyList<CidrRange> ParseAllowedCidrs(IEnumerable<string>? cidrValues)
    {
        if (cidrValues is null)
        {
            return Array.Empty<CidrRange>();
        }

        var parsed = new List<CidrRange>();
        foreach (var raw in cidrValues)
        {
            if (TryParseCidr(raw, out var cidr))
            {
                parsed.Add(cidr);
            }
        }

        return parsed;
    }

    private static bool TryParseCidr(string? rawValue, out CidrRange cidr)
    {
        cidr = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var parts = rawValue.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var prefixLength = 32;
        if (parts.Length == 2 && !int.TryParse(parts[1], out prefixLength))
        {
            return false;
        }

        if (prefixLength < 0 || prefixLength > 32)
        {
            return false;
        }

        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var network = ConvertIpv4ToUInt32(address) & mask;
        cidr = new CidrRange(network, prefixLength);
        return true;
    }

    private static uint ConvertIpv4ToUInt32(IPAddress ipv4)
    {
        var bytes = ipv4.GetAddressBytes();
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
    }
}

internal readonly struct CidrRange
{
    public CidrRange(uint network, int prefixLength)
    {
        Network = network;
        PrefixLength = prefixLength;
    }

    public uint Network { get; }

    public int PrefixLength { get; }

    public bool Contains(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        var value = ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
        var mask = PrefixLength == 0 ? 0u : uint.MaxValue << (32 - PrefixLength);
        return (value & mask) == Network;
    }
}
