namespace Device.Protocol.Models;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
//
// What the server reports back to clients about the panel currently stored
// (and possibly being rendered autonomously) for a given device.
//
// PanelJson is the raw JSON of the panel definition exactly as the server
// stored it — clients deserialize using their own PanelDefinition shape.
// Capability mirrors Panels.Composition.ServerSide.PanelServerCapability:
//   "ServerCapable" — server is rendering the panel autonomously
//   "RequiresClient" — server has the panel but needs the WinUI client to drive frames
//   "Empty" — server has an empty panel
public sealed class ServerPanelSnapshot
{
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Raw panel JSON (shape of <c>Panels.Composition.Models.PanelDefinition</c>).
    /// Clients deserialize into their own model.
    /// </summary>
    public string PanelJson { get; set; } = string.Empty;

    public string PanelId { get; set; } = string.Empty;

    public string PanelName { get; set; } = string.Empty;

    public int WidgetCount { get; set; }

    /// <summary>
    /// Server-side capability classification. Strings rather than enum to
    /// keep the wire format forward-compatible when new values are added.
    /// </summary>
    public string Capability { get; set; } = string.Empty;
}
