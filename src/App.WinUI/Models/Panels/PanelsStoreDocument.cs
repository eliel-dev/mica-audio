namespace App.WinUI.Models.Panels;

// DOCS: docs/wiki/modules/paineis.md#persistencia-do-layout
internal sealed class PanelsStoreDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string? LastSelectedPanelId { get; set; }

    public List<PanelDefinition> Panels { get; set; } = [];

    public PanelsStoreDocument Clone()
    {
        return new PanelsStoreDocument
        {
            SchemaVersion = SchemaVersion,
            LastSelectedPanelId = LastSelectedPanelId,
            Panels = Panels.Select(static panel => panel.Clone()).ToList(),
        };
    }

    public void Normalize()
    {
        SchemaVersion = CurrentSchemaVersion;
        Panels ??= [];
        foreach (var panel in Panels)
        {
            panel.Normalize();
        }

        Panels = Panels
            .OrderBy(panel => panel.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(panel => panel.PanelId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(LastSelectedPanelId)
            || Panels.All(panel => !string.Equals(panel.PanelId, LastSelectedPanelId, StringComparison.OrdinalIgnoreCase)))
        {
            LastSelectedPanelId = Panels.FirstOrDefault()?.PanelId;
        }
    }
}
