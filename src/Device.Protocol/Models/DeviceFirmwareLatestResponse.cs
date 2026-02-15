namespace Device.Protocol.Models;

public sealed class DeviceFirmwareLatestResponse
{
    public string Version { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string DownloadUrl { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
