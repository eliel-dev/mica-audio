using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-server-owned
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
public interface IPanelRuntimeDiagnosticsSource
{
    IReadOnlyList<PanelRuntimeDeviceDiagnostic> CreateSnapshot();
}

public interface IPanelRuntimeDiagnosticsStore : IPanelRuntimeDiagnosticsSource
{
    void Upsert(PanelRuntimeDeviceDiagnostic diagnostic);

    void Remove(string deviceId);
}
