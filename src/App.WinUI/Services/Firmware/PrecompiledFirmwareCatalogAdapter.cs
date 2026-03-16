using Device.Server.Hosting;

namespace App.WinUI.Services.Firmware;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
internal sealed class PrecompiledFirmwareCatalogAdapter : IDeviceOfficialFirmwareCatalog
{
    private readonly PrecompiledFirmwareService firmwareService;

    public PrecompiledFirmwareCatalogAdapter(PrecompiledFirmwareService firmwareService)
    {
        this.firmwareService = firmwareService;
    }

    public bool TryResolveLatest(
        string? boardModel,
        string? panelType,
        string? profile,
        out DeviceOfficialFirmwarePackage package,
        out string failureReason)
    {
        package = default!;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(boardModel)
            || string.IsNullOrWhiteSpace(panelType)
            || string.IsNullOrWhiteSpace(profile))
        {
            failureReason = "Metadados do dispositivo insuficientes para resolver o release oficial.";
            return false;
        }

        if (!firmwareService.TryResolveArtifact(boardModel.Trim(), panelType.Trim(), profile.Trim(), out var artifact, out failureReason))
        {
            return false;
        }

        package = new DeviceOfficialFirmwarePackage(
            artifact.Manifest.FirmwareVersion,
            artifact.Manifest.BoardModel,
            artifact.Manifest.PanelType,
            artifact.Manifest.Profile,
            artifact.Manifest.ControlPlane,
            artifact.FirmwarePath,
            artifact.ManifestPath,
            artifact.Manifest.Sha256,
            artifact.Manifest.FileSizeBytes);
        return true;
    }
}
