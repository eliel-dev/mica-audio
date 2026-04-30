namespace App.WinUI.Services.Firmware;

internal sealed record ResolvedFirmwareArtifact(
    PrecompiledFirmwareOption Option,
    string FirmwarePath,
    string ManifestPath,
    FirmwareArtifactManifest Manifest)
{
    public string? OtaFirmwarePath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Manifest.OtaFileName))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(FirmwarePath);
            return string.IsNullOrWhiteSpace(directory)
                ? Manifest.OtaFileName
                : Path.Combine(directory, Manifest.OtaFileName);
        }
    }
}
