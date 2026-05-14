using Panels.Composition.Models;

namespace Device.Server.Hosting;

// Panel catalog: server-side store that keeps ALL panels ever created by any
// client (Windows, Android, …). This is distinct from IServerPanelStore, which
// holds only the ONE panel currently active on each device for autonomous
// server-side rendering.
//
// Every create/update from any client must call Upsert so the catalog stays
// authoritative. GET /api/v1/admin/panels returns the full catalog with an
// activeOnDeviceId annotation so clients can see which panels are live.
public interface IServerPanelCatalogStore
{
    /// <summary>Returns all panels in the catalog.</summary>
    IReadOnlyCollection<PanelDefinition> ListAll();

    /// <summary>Returns the panel with the given id, or null when not found.</summary>
    PanelDefinition? TryGet(string panelId);

    /// <summary>
    /// Inserts or replaces the panel in the catalog (keyed by PanelId).
    /// Returns the stored snapshot.
    /// </summary>
    PanelDefinition Upsert(PanelDefinition panel);

    /// <summary>Removes the panel from the catalog. Returns true when it existed.</summary>
    bool Remove(string panelId);
}
