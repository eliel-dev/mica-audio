namespace Device.Protocol.Models;

public sealed class DeviceConfigResponse
{
    public string DeviceId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int MatrixWidth { get; init; } = 64;

    public int MatrixHeight { get; init; } = 32;

    public string StreamMode { get; init; } = "bins64";

    public string MdnsService { get; init; } = "_micaaudio._tcp";
}
