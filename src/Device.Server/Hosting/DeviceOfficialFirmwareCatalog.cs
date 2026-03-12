namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
public interface IDeviceOfficialFirmwareCatalog
{
    bool TryResolveLatest(
        string? boardModel,
        string? panelType,
        string? profile,
        out DeviceOfficialFirmwarePackage package,
        out string failureReason);
}

public sealed record DeviceOfficialFirmwarePackage(
    string FirmwareVersion,
    string BoardModel,
    string PanelType,
    string Profile,
    string ControlPlane,
    string FilePath,
    string ManifestPath,
    string Sha256,
    long FileSizeBytes);
