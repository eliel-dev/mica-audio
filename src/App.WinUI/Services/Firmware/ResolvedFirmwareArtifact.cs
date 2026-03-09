namespace App.WinUI.Services.Firmware;

internal sealed record ResolvedFirmwareArtifact(
    PrecompiledFirmwareOption Option,
    string FirmwarePath,
    string ManifestPath,
    FirmwareArtifactManifest Manifest);
