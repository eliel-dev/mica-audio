namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#zero-code-lan-onboarding
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public sealed class MicaDiscoveryRequestV1
{
    public string Protocol { get; init; } = "mica.discovery.v1";

    public string DeviceMac { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string? FirmwareVersion { get; init; }

    public string? BoardModel { get; init; }

    public string? PanelType { get; init; }

    public string? Profile { get; init; }
}
