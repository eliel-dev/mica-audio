using System.Security.Cryptography;
using System.Text.Json;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace MicaAudio.Server;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public sealed class StandaloneMediaLibraryStore : IMediaLibraryStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim ioGate = new(1, 1);
    private readonly string mediaRoot;
    private readonly string indexPath;
    private readonly string tempIndexPath;
    private readonly string backupIndexPath;

    public StandaloneMediaLibraryStore(string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        mediaRoot = Path.Combine(storageRoot, "media");
        indexPath = Path.Combine(mediaRoot, "media-index.json");
        tempIndexPath = indexPath + ".tmp";
        backupIndexPath = indexPath + ".bak";
    }

    public async Task<IReadOnlyList<MediaAssetInfo>> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadIndexCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task<MediaAssetInfo> SaveAsync(
        string fileName,
        string contentType,
        byte[] payload,
        long maxUploadBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.LongLength == 0)
        {
            throw new InvalidDataException("media_payload_empty");
        }

        if (payload.LongLength > maxUploadBytes)
        {
            throw new InvalidDataException("media_payload_too_large");
        }

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(mediaRoot);

            var sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            var extension = NormalizeExtension(fileName, contentType);
            var mediaId = $"{sha256}{extension}";
            var blobPath = GetBlobPath(mediaId);
            var index = (await LoadIndexCoreAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var existing = index.FirstOrDefault(info => string.Equals(info.MediaId, mediaId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && File.Exists(blobPath))
            {
                return existing;
            }

            if (!File.Exists(blobPath))
            {
                var tempBlobPath = blobPath + ".tmp";
                if (File.Exists(tempBlobPath))
                {
                    File.Delete(tempBlobPath);
                }

                await File.WriteAllBytesAsync(tempBlobPath, payload, cancellationToken).ConfigureAwait(false);
                File.Move(tempBlobPath, blobPath);
            }

            var info = existing ?? new MediaAssetInfo
            {
                MediaId = mediaId,
                FileName = NormalizeFileName(fileName, mediaId),
                ContentType = NormalizeContentType(contentType, extension),
                Extension = extension,
                SizeBytes = payload.LongLength,
                Sha256 = sha256,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };

            index.RemoveAll(candidate => string.Equals(candidate.MediaId, mediaId, StringComparison.OrdinalIgnoreCase));
            index.Add(info);
            await SaveIndexCoreAsync(index, cancellationToken).ConfigureAwait(false);
            return info;
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task<byte[]?> ReadBytesAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return null;
        }

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = GetBlobPath(mediaId.Trim());
            return File.Exists(path)
                ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
                : null;
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return false;
        }

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = mediaId.Trim();
            var index = (await LoadIndexCoreAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var removed = index.RemoveAll(info => string.Equals(info.MediaId, key, StringComparison.OrdinalIgnoreCase)) > 0;
            var path = GetBlobPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
                removed = true;
            }

            if (removed)
            {
                await SaveIndexCoreAsync(index, cancellationToken).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            ioGate.Release();
        }
    }

    private async Task<IReadOnlyList<MediaAssetInfo>> LoadIndexCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(indexPath))
        {
            return Array.Empty<MediaAssetInfo>();
        }

        try
        {
            await using var stream = File.OpenRead(indexPath);
            var loaded = await JsonSerializer.DeserializeAsync<List<MediaAssetInfo>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (loaded is null)
            {
                return Array.Empty<MediaAssetInfo>();
            }

            return loaded;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Array.Empty<MediaAssetInfo>();
        }
    }

    private async Task SaveIndexCoreAsync(IReadOnlyList<MediaAssetInfo> index, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(mediaRoot);
        TryDeleteTempIndex();
        await using (var stream = new FileStream(tempIndexPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, index, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(indexPath))
        {
            File.Replace(tempIndexPath, indexPath, backupIndexPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempIndexPath, indexPath);
        }
    }

    private string GetBlobPath(string mediaId)
        => Path.Combine(mediaRoot, Path.GetFileName(mediaId));

    private void TryDeleteTempIndex()
    {
        try
        {
            if (File.Exists(tempIndexPath))
            {
                File.Delete(tempIndexPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string NormalizeFileName(string? fileName, string fallback)
    {
        var leaf = Path.GetFileName(fileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(leaf) ? fallback : leaf.Trim();
    }

    private static string NormalizeExtension(string? fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.Trim().ToLowerInvariant();
        }

        return NormalizeContentType(contentType, string.Empty) switch
        {
            "image/gif" => ".gif",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".bin",
        };
    }

    private static string NormalizeContentType(string? contentType, string extension)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            return contentType.Trim().ToLowerInvariant();
        }

        return extension.ToLowerInvariant() switch
        {
            ".gif" => "image/gif",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
    }

    public void Dispose()
    {
        ioGate.Dispose();
    }
}
