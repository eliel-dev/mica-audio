namespace Device.Protocol.Models;

public sealed class PairDeviceRequest
{
    public string PairingCode { get; init; } = string.Empty;

    public string? DeviceName { get; init; }

    public string? Profile { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? DeviceMac { get; init; }
}
