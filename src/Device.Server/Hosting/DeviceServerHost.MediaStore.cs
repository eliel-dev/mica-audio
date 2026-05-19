using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/handoffs/2026-05-12-display-state-gif-panels.md
//
// Admin media-storage endpoints. The WinUI client uploads GIF/PNG/JPG/BMP files
// that gifhub75 widgets reference so that, after the desktop process exits,
// MicaAudio.Server's PanelCompositorHostedService can render those widgets
// autonomously.
//
// Routes (registered in DeviceServerHost.Routes.cs):
//   GET    /api/v1/admin/devices/{deviceId}/media/{mediaId}  - download a media file
//   PUT    /api/v1/admin/devices/{deviceId}/media/{mediaId}  — upsert a media file
//   DELETE /api/v1/admin/devices/{deviceId}/media/{mediaId}  — remove one file
//   DELETE /api/v1/admin/devices/{deviceId}/media            — remove all media for device
//
// Media files are accepted as raw binary bodies (not multipart). Content-Type is
// recorded for logging but not enforced; the server stores whatever bytes it receives.
// Maximum body size is enforced by MaxMediaBodyBytes (default 8 MiB).
public sealed partial class DeviceServerHost
{
    // 8 MiB default — large enough for a typical HUB75 GIF loop.
    public const long MaxMediaBodyBytes = 8 * 1024 * 1024;

    private IServerMediaStore? mediaStore;

    private static readonly HashSet<string> AllowedMediaExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp" };

    /// <summary>
    /// Allows MicaAudio.Server to register its media store after construction.
    /// Kept as a setter (rather than a ctor parameter) to remain backwards-compatible
    /// with callers that do not need media upload support.
    /// </summary>
    public void AttachMediaStore(IServerMediaStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        mediaStore = store;
    }

    public IServerMediaStore? MediaStore => mediaStore;

    private IResult HandleAdminListMedia(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (mediaStore is null)
        {
            return Results.Json(
                new { error = "media_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
        }

        var mediaIds = mediaStore.ListMediaIds(deviceId);
        var fileSizes = mediaStore.GetMediaSizes(deviceId);
        var totalBytes = fileSizes.Values.Sum();
        return Results.Ok(new { deviceId, mediaIds, fileSizes, totalBytes });
    }

    private IResult HandleAdminDownloadMedia(HttpContext ctx, string deviceId, string mediaId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (mediaStore is null)
        {
            return Results.Json(
                new { error = "media_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
        }

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return Results.BadRequest(new { error = "missing_media_id" });
        }

        var sanitizedMediaId = SanitizeMediaFileName(mediaId);
        if (string.IsNullOrEmpty(sanitizedMediaId))
        {
            return Results.BadRequest(new { error = "invalid_media_id" });
        }

        var mediaPath = mediaStore.TryResolvePath(deviceId, sanitizedMediaId);
        if (string.IsNullOrEmpty(mediaPath))
        {
            return Results.NotFound(new { error = "media_not_found" });
        }

        return Results.File(
            mediaPath,
            contentType: GetMediaContentType(sanitizedMediaId),
            fileDownloadName: sanitizedMediaId,
            enableRangeProcessing: true);
    }

    private async Task<IResult> HandleAdminUploadMediaAsync(HttpContext ctx, string deviceId, string mediaId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (mediaStore is null)
        {
            return Results.Json(
                new { error = "media_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
        }

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return Results.BadRequest(new { error = "missing_media_id" });
        }

        var sanitizedMediaId = SanitizeMediaFileName(mediaId);
        if (string.IsNullOrEmpty(sanitizedMediaId))
        {
            return Results.BadRequest(new { error = "invalid_media_id" });
        }

        var ext = Path.GetExtension(sanitizedMediaId);
        if (!AllowedMediaExtensions.Contains(ext))
        {
            return Results.BadRequest(new { error = "unsupported_media_type", allowedExtensions = AllowedMediaExtensions });
        }

        var contentLength = ctx.Request.ContentLength;
        if (contentLength is > MaxMediaBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        byte[] bytes;
        try
        {
            using var ms = new System.IO.MemoryStream(capacity: contentLength.HasValue
                ? (int)Math.Min(contentLength.Value, MaxMediaBodyBytes)
                : 65536);

            var buffer = new byte[81920];
            var totalRead = 0L;
            int read;
            while ((read = await ctx.Request.Body.ReadAsync(buffer, ctx.RequestAborted).ConfigureAwait(false)) > 0)
            {
                totalRead += read;
                if (totalRead > MaxMediaBodyBytes)
                {
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                }

                ms.Write(buffer, 0, read);
            }

            bytes = ms.ToArray();
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }

        if (bytes.Length == 0)
        {
            return Results.BadRequest(new { error = "empty_body" });
        }

        mediaStore.Save(deviceId, sanitizedMediaId, bytes);
        Log($"Media uploaded for device {deviceId}: {sanitizedMediaId} ({bytes.Length:N0} bytes).");

        return Results.Ok(new
        {
            deviceId,
            mediaId = sanitizedMediaId,
            bytes = bytes.Length,
        });
    }

    private IResult HandleAdminDeleteMedia(HttpContext ctx, string deviceId, string mediaId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (mediaStore is null)
        {
            return Results.Json(
                new { error = "media_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
        }

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return Results.BadRequest(new { error = "missing_media_id" });
        }

        var sanitized = SanitizeMediaFileName(mediaId);
        var removed = !string.IsNullOrEmpty(sanitized) && mediaStore.Remove(deviceId, sanitized);
        Log($"Media removal for device {deviceId}: {mediaId} (existed={removed}).");
        return Results.Ok(new { deviceId, mediaId, removed });
    }

    private IResult HandleAdminDeleteAllMedia(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (mediaStore is null)
        {
            return Results.Json(
                new { error = "media_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
        }

        var count = mediaStore.RemoveAll(deviceId);
        Log($"All media removed for device {deviceId} ({count} files).");
        return Results.Ok(new { deviceId, filesRemoved = count });
    }

    private static string SanitizeMediaFileName(string raw)
    {
        // Allow [A-Za-z0-9._-] only. Reject leading dots to prevent hidden files
        // and directory traversal ("../secrets"). Preserve the extension dot.
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_')
            {
                sb.Append(ch);
            }
        }

        var sanitized = sb.ToString().TrimStart('.');
        return string.IsNullOrEmpty(sanitized) ? string.Empty : sanitized;
    }

    private static string GetMediaContentType(string mediaId)
    {
        return Path.GetExtension(mediaId).ToLowerInvariant() switch
        {
            ".gif" => "image/gif",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }
}
