namespace Device.Protocol.Models;

public sealed class PairingCodeInfo
{
    public string Code { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
