namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public sealed class PanelLibraryItem
{
    public string PanelId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Width { get; init; } = 128;

    public int Height { get; init; } = 64;

    public bool IsEnabled { get; init; } = true;

    public IReadOnlyList<PanelWidgetItem> Widgets { get; init; } = Array.Empty<PanelWidgetItem>();
}
