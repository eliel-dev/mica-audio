using Device.Protocol.Models;

namespace App.WinUI.Models.Panels;

// DOCS: docs/wiki/modules/paineis.md#persistencia-do-layout
// DOCS: docs/handoffs/2026-04-29-lan-panel-architecture-realignment.md
internal sealed class PanelsStoreDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string? LastSelectedPanelId { get; set; }

    public List<PanelDeviceState> ActivePanels { get; set; } = [];

    public List<PanelDefinition> Panels { get; set; } = [];

    public PanelsStoreDocument Clone()
    {
        return new PanelsStoreDocument
        {
            SchemaVersion = SchemaVersion,
            LastSelectedPanelId = LastSelectedPanelId,
            ActivePanels = (ActivePanels ?? [])
                .Select(static state => new PanelDeviceState
                {
                    DeviceId = state.DeviceId,
                    ActivePanelId = state.ActivePanelId,
                    ActiveAppId = state.ActiveAppId,
                    LastServerOwnedPanelId = state.LastServerOwnedPanelId,
                    UpdatedAtUtc = state.UpdatedAtUtc,
                })
                .ToList(),
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

        ActivePanels ??= [];
        var knownPanelIds = Panels
            .Select(static panel => panel.PanelId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ActivePanels = ActivePanels
            .Where(static state => !string.IsNullOrWhiteSpace(state.DeviceId))
            .GroupBy(static state => state.DeviceId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => NormalizeDeviceState(group.Last(), knownPanelIds))
            .OrderBy(static state => state.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PanelDeviceState NormalizeDeviceState(PanelDeviceState state, ISet<string> knownPanelIds)
    {
        return new PanelDeviceState
        {
            DeviceId = state.DeviceId.Trim(),
            ActivePanelId = NormalizePanelReference(state.ActivePanelId, knownPanelIds),
            ActiveAppId = NormalizeOptional(state.ActiveAppId),
            LastServerOwnedPanelId = NormalizePanelReference(state.LastServerOwnedPanelId, knownPanelIds),
            UpdatedAtUtc = state.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : state.UpdatedAtUtc,
        };
    }

    private static string? NormalizePanelReference(string? panelId, ISet<string> knownPanelIds)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return null;
        }

        var trimmed = panelId.Trim();
        return knownPanelIds.Contains(trimmed) ? trimmed : null;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
