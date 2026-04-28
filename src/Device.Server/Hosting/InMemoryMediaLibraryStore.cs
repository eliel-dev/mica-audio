using System.Security.Cryptography;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
internal sealed class InMemoryMediaLibraryStore : IMediaLibraryStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, MediaAssetInfo> index = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> blobs = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<MediaAssetInfo>> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<MediaAssetInfo>>(index.Values.OrderBy(info => info.FileName, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    public Task<MediaAssetInfo> SaveAsync(
        string fileName,
        string contentType,
        byte[] payload,
        long maxUploadBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        if (payload.LongLength == 0)
        {
            throw new InvalidDataException("media_payload_empty");
        }

        if (payload.LongLength > maxUploadBytes)
        {
            throw new InvalidDataException("media_payload_too_large");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var extension = MediaLibraryStoreHelpers.NormalizeExtension(fileName, contentType);
        var mediaId = $"{sha256}{extension}";
        var normalizedContentType = MediaLibraryStoreHelpers.NormalizeContentType(contentType, extension);

        lock (gate)
        {
            if (index.TryGetValue(mediaId, out var existing))
            {
                return Task.FromResult(existing);
            }

            var info = new MediaAssetInfo
            {
                MediaId = mediaId,
                FileName = MediaLibraryStoreHelpers.NormalizeFileName(fileName, mediaId),
                ContentType = normalizedContentType,
                Extension = extension,
                SizeBytes = payload.LongLength,
                Sha256 = sha256,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            index[mediaId] = info;
            blobs[mediaId] = payload.ToArray();
            return Task.FromResult(info);
        }
    }

    public Task<byte[]?> ReadBytesAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(
                !string.IsNullOrWhiteSpace(mediaId) && blobs.TryGetValue(mediaId.Trim(), out var payload)
                    ? payload.ToArray()
                    : null);
        }
    }

    public Task<bool> DeleteAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(mediaId))
            {
                return Task.FromResult(false);
            }

            var key = mediaId.Trim();
            var removed = index.Remove(key);
            blobs.Remove(key);
            return Task.FromResult(removed);
        }
    }
}
