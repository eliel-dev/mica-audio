using System.Collections.Concurrent;
using Panels.Composition.Models;

namespace Device.Server.Hosting;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
//
// Lightweight in-memory implementation of IServerPanelStore used by tests and
// by callers that do not need cross-restart persistence. Production deployments
// of MicaAudio.Server replace this with FileServerPanelStore.
public sealed class InMemoryServerPanelStore : IServerPanelStore
{
    private readonly ConcurrentDictionary<string, PanelDefinition> panelsByDeviceId =
        new(StringComparer.OrdinalIgnoreCase);

    public PanelDefinition? TryGet(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return panelsByDeviceId.TryGetValue(deviceId, out var panel) ? panel.Clone() : null;
    }

    public void Save(string deviceId, PanelDefinition panel)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("deviceId is required.", nameof(deviceId));
        }

        ArgumentNullException.ThrowIfNull(panel);
        panelsByDeviceId[deviceId] = panel.Clone();
    }

    public bool Remove(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        return panelsByDeviceId.TryRemove(deviceId, out _);
    }

    public IReadOnlyCollection<string> EnumerateDeviceIds()
    {
        return panelsByDeviceId.Keys.ToArray();
    }
}
