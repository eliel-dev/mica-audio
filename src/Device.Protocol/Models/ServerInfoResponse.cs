namespace Device.Protocol.Models;

public sealed class ServerInfoResponse
{
    public string Name { get; init; } = "MicaAudio Device Server";

    public string Version { get; init; } = "1";

    public string WsPath { get; init; } = "/ws/v1/stream";

    public string HttpBase { get; init; } = string.Empty;

    public string MdnsService { get; init; } = "_micaaudio._tcp";

    public int MaxDevices { get; init; } = 5;
}
