namespace Device.Protocol.Contracts;

public sealed class ServerConfig
{
    public string ListenHost { get; init; } = "0.0.0.0";

    public int Port { get; init; } = 5272;

    public int MaxDevices { get; init; } = 5;

    public string MdnsServiceName { get; init; } = "_micaaudio._tcp";

    public string PublicHost { get; init; } = "micaaudio.local";
}
