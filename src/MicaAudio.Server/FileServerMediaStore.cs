using Device.Server.Hosting;

namespace MicaAudio.Server;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
//
// Filesystem-backed media store. Files are stored under:
//   {StorageRoot}/media/{deviceId}/{mediaId}
//
// The directory layout mirrors the panel store convention so both are
// rooted in the same configurable StorageRoot. No in-memory cache is kept:
// ServerGifWidgetRuntime decodes the file once at compositor creation time
// and holds the decoded frames in memory, so reads here only happen when a
// new compositor is built (panel change or server restart).
public sealed class FileServerMediaStore : IServerMediaStore
{
    private readonly string mediaRoot;

    public FileServerMediaStore(string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        mediaRoot = Path.Combine(storageRoot, "media");
        Directory.CreateDirectory(mediaRoot);
    }

    public string? TryResolvePath(string deviceId, string mediaId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(mediaId))
        {
            return null;
        }

        var path = ResolvePath(deviceId, mediaId);
        return File.Exists(path) ? path : null;
    }

    public string GetMediaDirectory(string deviceId)
    {
        return Path.Combine(mediaRoot, SanitizeSegment(deviceId));
    }

    public void Save(string deviceId, string mediaId, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaId);
        ArgumentNullException.ThrowIfNull(content);

        var dir = GetMediaDirectory(deviceId);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, SanitizeSegment(mediaId));
        File.WriteAllBytes(path, content);
    }

    public bool Remove(string deviceId, string mediaId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(mediaId))
        {
            return false;
        }

        var path = ResolvePath(deviceId, mediaId);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public int RemoveAll(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return 0;
        }

        var dir = GetMediaDirectory(deviceId);
        if (!Directory.Exists(dir))
        {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
            count++;
        }

        try
        {
            // Remove the directory if now empty.
            Directory.Delete(dir, recursive: false);
        }
        catch (IOException)
        {
            // Not empty or in use — leave it.
        }

        return count;
    }

    private string ResolvePath(string deviceId, string mediaId)
    {
        return Path.Combine(mediaRoot, SanitizeSegment(deviceId), SanitizeSegment(mediaId));
    }

    private static string SanitizeSegment(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
        }

        var sanitized = sb.ToString().TrimStart('.');
        return string.IsNullOrEmpty(sanitized) ? "device" : sanitized;
    }
}
