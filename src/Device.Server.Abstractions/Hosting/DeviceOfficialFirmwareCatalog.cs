namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
// DOCS: docs/handoffs/2026-04-21-server-embedded-decoupling.md
public sealed record DeviceOfficialFirmwarePackage(
    string FirmwareVersion,
    string BoardModel,
    string PanelType,
    string Profile,
    string ControlPlane,
    string FilePath,
    string ManifestPath,
    string Sha256,
    long FileSizeBytes,
    string? OtaFilePath = null,
    string OtaSha256 = "",
    long OtaFileSizeBytes = 0);
