using System.Collections.Concurrent;
using Panels.Composition.Models;

namespace Device.Server.Hosting;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/handoffs/2026-05-12-display-state-gif-panels.md
//
// Lightweight in-memory implementation of IServerPanelStore used by tests and
// by callers that do not need cross-restart persistence. Production deployments
// of MicaAudio.Server replace this with FileServerPanelStore.
public sealed class InMemoryServerPanelStore : IServerPanelStore
{
    private readonly ConcurrentDictionary<string, PanelDefinition> panelsByDeviceId =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, byte> clearedDeviceIds =
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
        clearedDeviceIds.TryRemove(deviceId, out _);
    }

    public bool Remove(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        var existed = panelsByDeviceId.TryRemove(deviceId, out _);
        clearedDeviceIds[deviceId] = 0;

        return existed;
    }

    public IReadOnlyCollection<string> EnumerateDeviceIds()
    {
        return panelsByDeviceId.Keys.ToArray();
    }

    public DevicePanelDisplayState GetDisplayState(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return DevicePanelDisplayState.NeverSet;
        }

        if (panelsByDeviceId.ContainsKey(deviceId))
        {
            return DevicePanelDisplayState.Active;
        }

        return clearedDeviceIds.ContainsKey(deviceId)
            ? DevicePanelDisplayState.ExplicitlyCleared
            : DevicePanelDisplayState.NeverSet;
    }
}
