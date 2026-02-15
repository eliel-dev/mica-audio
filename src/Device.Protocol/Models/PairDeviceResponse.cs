namespace Device.Protocol.Models;

public sealed class PairDeviceResponse
{
    public string DeviceId { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string WsPath { get; init; } = "/ws/v1/stream";

    public string HttpBase { get; init; } = string.Empty;

    public string MdnsService { get; init; } = "_micaaudio._tcp";
}
