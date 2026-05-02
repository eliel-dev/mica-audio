using Device.Protocol.Models;
using Device.Server.Hosting;
using MicaAudio.Panels;

namespace MicaAudio.Server;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/handoffs/2026-04-30-remote-only-server-panel-runtime.md
public sealed class ServerPanelMediaSourceResolver : IPanelMediaSourceResolver
{
    private readonly IMediaLibraryStore mediaLibraryStore;

    public ServerPanelMediaSourceResolver(IMediaLibraryStore mediaLibraryStore)
    {
        this.mediaLibraryStore = mediaLibraryStore;
    }

    public async Task<IReadOnlyList<PanelMediaSource>> ResolveAsync(PanelWidgetItem widget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);

        var mediaIds = ResolveMediaIds(widget).ToArray();
        if (mediaIds.Length == 0)
        {
            return Array.Empty<PanelMediaSource>();
        }

        var index = await mediaLibraryStore.LoadIndexAsync(cancellationToken).ConfigureAwait(false);
        var infoById = index.ToDictionary(static info => info.MediaId, StringComparer.OrdinalIgnoreCase);
        var sources = new List<PanelMediaSource>(mediaIds.Length);
        foreach (var mediaId in mediaIds)
        {
            var payload = await mediaLibraryStore.ReadBytesAsync(mediaId, cancellationToken).ConfigureAwait(false);
            if (payload is null || payload.Length == 0)
            {
                continue;
            }

            infoById.TryGetValue(mediaId, out var info);
            sources.Add(new PanelMediaSource(
                CacheKey: mediaId,
                Payload: payload,
                ContentType: info?.ContentType ?? ResolveContentType(mediaId),
                FileName: info?.FileName ?? mediaId));
        }

        return sources;
    }

    private static IEnumerable<string> ResolveMediaIds(PanelWidgetItem widget)
    {
        if (widget.RuntimeState.TryGetValue("mediaIds", out var mediaIds) && !string.IsNullOrWhiteSpace(mediaIds))
        {
            foreach (var itemMediaId in mediaIds.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return itemMediaId;
            }
        }

        if (widget.RuntimeState.TryGetValue("mediaId", out var mediaId) && !string.IsNullOrWhiteSpace(mediaId))
        {
            yield return mediaId.Trim();
        }
    }

    private static string ResolveContentType(string mediaId)
    {
        return Path.GetExtension(mediaId).ToLowerInvariant() switch
        {
            ".gif" => "image/gif",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }
}
