namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#contrato-mqtt-de-controle
public sealed class DeviceFirmwareReleaseInfo
{
    public string FirmwareVersion { get; init; } = string.Empty;

    public string BoardModel { get; init; } = string.Empty;

    public string PanelType { get; init; } = string.Empty;

    public string Profile { get; init; } = string.Empty;

    public string ControlPlane { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public string DownloadPath { get; init; } = string.Empty;
}
