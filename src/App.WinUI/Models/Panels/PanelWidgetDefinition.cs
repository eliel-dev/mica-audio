namespace App.WinUI.Models.Panels;

// DOCS: docs/wiki/modules/paineis.md#persistencia-do-layout
internal sealed class PanelWidgetDefinition
{
    public string WidgetId { get; set; } = Guid.NewGuid().ToString("N");

    public string AppId { get; set; } = string.Empty;

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; } = 64;

    public int Height { get; set; } = 32;

    public int ZIndex { get; set; }

    public Dictionary<string, string> ConfigValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> RuntimeState { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PanelWidgetDefinition Clone()
    {
        return new PanelWidgetDefinition
        {
            WidgetId = WidgetId,
            AppId = AppId,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            ZIndex = ZIndex,
            ConfigValues = new Dictionary<string, string>(ConfigValues, StringComparer.OrdinalIgnoreCase),
            RuntimeState = new Dictionary<string, string>(RuntimeState, StringComparer.OrdinalIgnoreCase),
        };
    }

    public void Normalize(int panelWidth, int panelHeight)
    {
        WidgetId = string.IsNullOrWhiteSpace(WidgetId) ? Guid.NewGuid().ToString("N") : WidgetId.Trim();
        AppId = string.IsNullOrWhiteSpace(AppId) ? string.Empty : AppId.Trim();
        Width = Math.Clamp(Width <= 0 ? 64 : Width, 1, Math.Max(1, panelWidth));
        Height = Math.Clamp(Height <= 0 ? 32 : Height, 1, Math.Max(1, panelHeight));
        X = Math.Clamp(X, 0, Math.Max(0, panelWidth - Width));
        Y = Math.Clamp(Y, 0, Math.Max(0, panelHeight - Height));
        ConfigValues = new Dictionary<string, string>(ConfigValues ?? [], StringComparer.OrdinalIgnoreCase);
        RuntimeState = new Dictionary<string, string>(RuntimeState ?? [], StringComparer.OrdinalIgnoreCase);
    }
}
