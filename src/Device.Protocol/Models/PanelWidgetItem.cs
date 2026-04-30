namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
// DOCS: docs/handoffs/2026-04-29-lan-panel-architecture-realignment.md
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
public sealed class PanelWidgetItem
{
    public string WidgetId { get; init; } = string.Empty;

    public string AppId { get; init; } = string.Empty;

    public string DataSource { get; init; } = PanelWidgetDataSources.Server;

    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int ZIndex { get; init; }

    public IReadOnlyDictionary<string, string> ConfigValues { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> RuntimeState { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
