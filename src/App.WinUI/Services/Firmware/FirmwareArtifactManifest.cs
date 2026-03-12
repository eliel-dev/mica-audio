namespace App.WinUI.Services.Firmware;

internal sealed class FirmwareArtifactManifest
{
    public int SchemaVersion { get; init; } = 1;

    public string FirmwareVersion { get; init; } = string.Empty;

    public string GitSha { get; init; } = string.Empty;

    public string Profile { get; init; } = string.Empty;

    public string BoardModel { get; init; } = string.Empty;

    public string PanelType { get; init; } = string.Empty;

    public string ControlPlane { get; init; } = string.Empty;

    public DateTimeOffset BuiltAtUtc { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }
}
