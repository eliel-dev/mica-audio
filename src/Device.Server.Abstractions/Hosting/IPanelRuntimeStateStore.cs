using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/device-server-protocol.md#runtime-remoto-de-paineis
public interface IPanelRuntimeStateStore
{
    Task<PanelRuntimeStateDocument> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(PanelRuntimeStateDocument document, CancellationToken cancellationToken = default);
}
