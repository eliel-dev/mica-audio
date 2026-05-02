namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public sealed class PanelLibraryDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string? LastSelectedPanelId { get; init; }

    public IReadOnlyList<PanelLibraryItem> Panels { get; init; } = Array.Empty<PanelLibraryItem>();
}
