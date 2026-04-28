using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public interface IPanelLibraryStore
{
    Task<PanelLibraryDocument> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(PanelLibraryDocument document, CancellationToken cancellationToken = default);
}
