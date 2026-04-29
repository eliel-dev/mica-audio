namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-29-lan-panel-architecture-realignment.md
public sealed class PanelDeviceState
{
    public string DeviceId { get; init; } = string.Empty;

    public string? ActivePanelId { get; init; }

    public string? ActiveAppId { get; init; }

    public string? LastServerOwnedPanelId { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
