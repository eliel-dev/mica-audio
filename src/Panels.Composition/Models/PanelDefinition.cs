using MicaAudio.Core.Led;

namespace Panels.Composition.Models;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// Cross-platform mirror of App.WinUI.Models.Panels.PanelDefinition. Used by
// MicaAudio.Server to persist the active panel per device and by the server-
// side composer to render autonomous widgets after the WinUI client has
// disconnected.
//
// The shape matches the WinUI internal model exactly so that JSON serialized
// by either side round-trips unchanged.
public sealed class PanelDefinition
{
    public string PanelId { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Painel";

    public int Width { get; set; } = LedDefaults.MatrixWidth;

    public int Height { get; set; } = LedDefaults.MatrixHeight;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<PanelWidgetDefinition> Widgets { get; set; } = [];

    public PanelDefinition Clone()
    {
        return new PanelDefinition
        {
            PanelId = PanelId,
            Name = Name,
            Width = Width,
            Height = Height,
            UpdatedAtUtc = UpdatedAtUtc,
            Widgets = Widgets.Select(static widget => widget.Clone()).ToList(),
        };
    }

    public void Normalize()
    {
        PanelId = string.IsNullOrWhiteSpace(PanelId) ? Guid.NewGuid().ToString("N") : PanelId.Trim();
        Name = string.IsNullOrWhiteSpace(Name) ? "Painel" : Name.Trim();
        Width = LedDefaults.MatrixWidth;
        Height = LedDefaults.MatrixHeight;
        Widgets ??= [];
        foreach (var widget in Widgets)
        {
            widget.Normalize(Width, Height);
        }

        Widgets = Widgets
            .OrderBy(widget => widget.ZIndex)
            .ThenBy(widget => widget.WidgetId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < Widgets.Count; i++)
        {
            Widgets[i].ZIndex = i;
        }
    }
}
