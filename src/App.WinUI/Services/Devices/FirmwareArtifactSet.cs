namespace App.WinUI.Services.Devices;

internal sealed class FirmwareArtifactSet
{
    public string Profile { get; init; } = string.Empty;

    public string BuildDirectory { get; init; } = string.Empty;

    public string? MergedBinPath { get; init; }

    public string? FirmwareBinPath { get; init; }

    public string? BootloaderBinPath { get; init; }

    public string? PartitionsBinPath { get; init; }

    public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();
}
