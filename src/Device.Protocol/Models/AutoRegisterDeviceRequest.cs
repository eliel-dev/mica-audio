namespace Device.Protocol.Models;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// Auto-registration request from a firmware booting in STA-hardcoded mode.
// Server is responsible for validating the request comes from a private LAN address
// and for issuing a deterministic deviceId per MAC address (idempotent).
public sealed class AutoRegisterDeviceRequest
{
    public string MacAddress { get; init; } = string.Empty;

    public string? DeviceName { get; init; }

    public string? Profile { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? BoardModel { get; init; }

    public string? PanelType { get; init; }
}
