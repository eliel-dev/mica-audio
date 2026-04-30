using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-server-owned
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
public sealed class InMemoryPanelRuntimeDiagnosticsStore : IPanelRuntimeDiagnosticsStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, PanelRuntimeDeviceDiagnostic> diagnosticsByDeviceId =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PanelRuntimeDeviceDiagnostic> CreateSnapshot()
    {
        lock (gate)
        {
            return diagnosticsByDeviceId.Values
                .OrderBy(static diagnostic => diagnostic.DeviceId, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToArray();
        }
    }

    public void Upsert(PanelRuntimeDeviceDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (string.IsNullOrWhiteSpace(diagnostic.DeviceId))
        {
            return;
        }

        lock (gate)
        {
            diagnosticsByDeviceId[diagnostic.DeviceId.Trim()] = Clone(diagnostic);
        }
    }

    public void Remove(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        lock (gate)
        {
            diagnosticsByDeviceId.Remove(deviceId.Trim());
        }
    }

    private static PanelRuntimeDeviceDiagnostic Clone(PanelRuntimeDeviceDiagnostic diagnostic)
    {
        return new PanelRuntimeDeviceDiagnostic
        {
            DeviceId = diagnostic.DeviceId,
            PanelId = diagnostic.PanelId,
            State = diagnostic.State,
            LastBatchSequence = diagnostic.LastBatchSequence,
            LastError = diagnostic.LastError,
            UpdatedAtUtc = diagnostic.UpdatedAtUtc,
        };
    }
}
