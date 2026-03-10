using MicaAudio.Core.Led;

namespace App.WinUI.Models.Panels;

// DOCS: docs/wiki/modules/paineis.md#persistencia-do-layout
internal sealed class PanelDefinition
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
    }
}
