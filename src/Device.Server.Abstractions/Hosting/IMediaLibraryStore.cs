using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public interface IMediaLibraryStore
{
    Task<IReadOnlyList<MediaAssetInfo>> LoadIndexAsync(CancellationToken cancellationToken = default);

    Task<MediaAssetInfo> SaveAsync(
        string fileName,
        string contentType,
        byte[] payload,
        long maxUploadBytes,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadBytesAsync(string mediaId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string mediaId, CancellationToken cancellationToken = default);
}
