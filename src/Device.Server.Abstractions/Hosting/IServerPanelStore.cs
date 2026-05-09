using Panels.Composition.Models;

namespace Device.Server.Hosting;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// Persistence boundary for the active per-device panel that the server-side
// compositor renders after the WinUI client has disconnected. Implementations
// may keep state in memory (tests) or on disk (production via FileServerPanelStore).
public interface IServerPanelStore
{
    /// <summary>
    /// Returns the panel currently configured for the device, or null when no
    /// panel has been uploaded yet.
    /// </summary>
    PanelDefinition? TryGet(string deviceId);

    /// <summary>Persists the panel for the device.</summary>
    void Save(string deviceId, PanelDefinition panel);

    /// <summary>Removes any stored panel for the device.</summary>
    bool Remove(string deviceId);

    /// <summary>Enumerates all device ids that currently have a stored panel.</summary>
    IReadOnlyCollection<string> EnumerateDeviceIds();
}
