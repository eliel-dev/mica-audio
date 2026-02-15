namespace App.WinUI.Services.Devices;

internal interface IFirmwareBuildService
{
    Task EnsureToolchainAsync(CancellationToken cancellationToken = default);

    Task<FirmwareArtifactSet> BuildAsync(FirmwareBuildRequest request, IProgress<BuildProgressUpdate>? progress = null, CancellationToken cancellationToken = default);

    Task<string> ExportAsync(FirmwareArtifactSet artifactSet, string targetRootDirectory, CancellationToken cancellationToken = default);
}
