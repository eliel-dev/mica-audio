using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/device-server-protocol.md#runtime-remoto-de-paineis
public interface IPanelRuntimeStatusStore
{
    Task<PanelRuntimeStatusDocument> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(PanelRuntimeStatusDocument document, CancellationToken cancellationToken = default);
}
