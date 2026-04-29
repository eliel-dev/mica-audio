namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
// DOCS: docs/handoffs/2026-04-29-lan-panel-architecture-realignment.md
public sealed class PanelLibraryDocument
{
    public int SchemaVersion { get; init; } = 1;

    public string? LastSelectedPanelId { get; init; }

    public IReadOnlyList<PanelDeviceState> ActivePanels { get; init; } = Array.Empty<PanelDeviceState>();

    public IReadOnlyList<PanelLibraryItem> Panels { get; init; } = Array.Empty<PanelLibraryItem>();
}
