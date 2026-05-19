namespace Device.Server.Hosting;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
//
// Persistence boundary for per-device media files (GIF, PNG, JPG, BMP) uploaded
// by the WinUI client so the server-side compositor can render gifhub75 widgets
// autonomously after the desktop process exits.
//
// Files are addressed by (deviceId, mediaId) where:
//   deviceId — the device's registered identifier.
//   mediaId  — a stable filename chosen by the client; typically
//              "{sanitizedWidgetId}{extension}" (e.g. "abc123def456.gif").
//
// Implementations may store files anywhere; the canonical production
// implementation is FileServerMediaStore which writes to
// {StorageRoot}/media/{deviceId}/{mediaId}.
public interface IServerMediaStore
{
    /// <summary>
    /// Returns the full path to the media file for the given device and mediaId,
    /// or null when the file does not exist.
    /// </summary>
    string? TryResolvePath(string deviceId, string mediaId);

    /// <summary>
    /// Returns the directory that contains all media files for the device.
    /// The directory may not exist yet when no media has been uploaded.
    /// </summary>
    string GetMediaDirectory(string deviceId);

    /// <summary>
    /// Persists <paramref name="content"/> as the media for (deviceId, mediaId).
    /// Overwrites an existing file with the same mediaId (idempotent).
    /// </summary>
    void Save(string deviceId, string mediaId, byte[] content);

    /// <summary>
    /// Removes the media file for (deviceId, mediaId).
    /// Returns true if the file existed and was deleted, false if it was already absent.
    /// </summary>
    bool Remove(string deviceId, string mediaId);

    /// <summary>
    /// Removes all media files for the device.
    /// Returns the number of files deleted.
    /// </summary>
    int RemoveAll(string deviceId);

    /// <summary>
    /// Returns the mediaIds (filenames) of all media files stored for the device.
    /// Returns an empty list when the device has no media.
    /// </summary>
    IReadOnlyList<string> ListMediaIds(string deviceId);

    /// <summary>
    /// Returns a map of mediaId → file size in bytes for every media file stored
    /// for the device. Returns an empty dictionary when the device has no media.
    /// </summary>
    IReadOnlyDictionary<string, long> GetMediaSizes(string deviceId);
}
