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
        NormalizeWidgetZOrder();
    }

    public bool CanMoveWidgetForward(string widgetId)
    {
        var index = FindWidgetIndex(widgetId);
        return index >= 0 && index < Widgets.Count - 1;
    }

    public bool CanMoveWidgetBackward(string widgetId)
    {
        var index = FindWidgetIndex(widgetId);
        return index > 0;
    }

    public bool TryMoveWidgetForward(string widgetId)
    {
        Normalize();
        var index = FindWidgetIndex(widgetId);
        if (index < 0 || index >= Widgets.Count - 1)
        {
            return false;
        }

        (Widgets[index], Widgets[index + 1]) = (Widgets[index + 1], Widgets[index]);
        NormalizeWidgetZOrder();
        return true;
    }

    public bool TryMoveWidgetBackward(string widgetId)
    {
        Normalize();
        var index = FindWidgetIndex(widgetId);
        if (index <= 0)
        {
            return false;
        }

        (Widgets[index], Widgets[index - 1]) = (Widgets[index - 1], Widgets[index]);
        NormalizeWidgetZOrder();
        return true;
    }

    public IReadOnlyList<PanelWidgetDefinition> GetWidgetsInVisualOrderTopFirst()
    {
        Normalize();
        return Widgets.AsEnumerable().Reverse().ToList();
    }

    public IReadOnlyList<PanelWidgetDefinition> GetOverlappingWidgetsInVisualOrderTopFirst(string widgetId)
    {
        Normalize();
        var selected = FindWidget(widgetId);
        if (selected is null)
        {
            return [];
        }

        return Widgets
            .Where(widget =>
                !string.Equals(widget.WidgetId, selected.WidgetId, StringComparison.OrdinalIgnoreCase)
                && HasPositiveAreaIntersection(selected, widget))
            .OrderByDescending(widget => widget.ZIndex)
            .ToList();
    }

    public bool TryGetNextOverlappingWidgetId(string widgetId, out string nextWidgetId)
    {
        Normalize();
        var selected = FindWidget(widgetId);
        if (selected is null)
        {
            nextWidgetId = string.Empty;
            return false;
        }

        var overlappingGroup = Widgets
            .Where(widget =>
                string.Equals(widget.WidgetId, selected.WidgetId, StringComparison.OrdinalIgnoreCase)
                || HasPositiveAreaIntersection(selected, widget))
            .OrderByDescending(widget => widget.ZIndex)
            .ToList();

        if (overlappingGroup.Count < 2)
        {
            nextWidgetId = string.Empty;
            return false;
        }

        var selectedIndex = overlappingGroup.FindIndex(widget => string.Equals(widget.WidgetId, selected.WidgetId, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0)
        {
            nextWidgetId = string.Empty;
            return false;
        }

        nextWidgetId = overlappingGroup[(selectedIndex + 1) % overlappingGroup.Count].WidgetId;
        return true;
    }

    private int FindWidgetIndex(string widgetId)
    {
        return Widgets.FindIndex(widget => string.Equals(widget.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));
    }

    private PanelWidgetDefinition? FindWidget(string widgetId)
    {
        return Widgets.FirstOrDefault(widget => string.Equals(widget.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasPositiveAreaIntersection(PanelWidgetDefinition left, PanelWidgetDefinition right)
    {
        var intersectionLeft = Math.Max(left.X, right.X);
        var intersectionTop = Math.Max(left.Y, right.Y);
        var intersectionRight = Math.Min(left.X + left.Width, right.X + right.Width);
        var intersectionBottom = Math.Min(left.Y + left.Height, right.Y + right.Height);
        return intersectionRight > intersectionLeft && intersectionBottom > intersectionTop;
    }

    private void NormalizeWidgetZOrder()
    {
        for (var i = 0; i < Widgets.Count; i++)
        {
            Widgets[i].ZIndex = i;
        }
    }
}
