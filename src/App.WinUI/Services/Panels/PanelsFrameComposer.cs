using App.WinUI.Models.Panels;
using Device.Client;
using Device.Protocol.Models;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using MicaAudio.Panels;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-compartilhado
// DOCS: docs/wiki/modules/app-winui.md#remote-only
// DOCS: docs/handoffs/2026-04-30-remote-only-server-panel-runtime.md
internal sealed class PanelsFrameComposer
{
    public const int TargetFps = PanelFrameComposer.TargetFps;

    private readonly PanelFrameComposer inner;

    public PanelsFrameComposer(IDeviceServerClient? deviceServerClient = null)
    {
        IPanelMediaSourceResolver resolver = deviceServerClient is null
            ? new FileSystemPanelMediaSourceResolver()
            : new WinUiPanelMediaSourceResolver(deviceServerClient);
        inner = new PanelFrameComposer(resolver);
    }

    internal static bool SupportsWidgetApp(string? appId) => PanelFrameComposer.SupportsWidgetApp(appId);

    internal async Task<PanelCompositionSession> CreateSessionAsync(PanelDefinition panel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var snapshot = panel.Clone();
        snapshot.Normalize();
        var session = await inner.CreateSessionAsync(ToPanelLibraryItem(snapshot), PanelRenderIntent.Animated, cancellationToken).ConfigureAwait(false);
        return new PanelCompositionSession(snapshot, session);
    }

    internal async Task<PanelPosterResult> CreatePosterAsync(PanelDefinition panel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var snapshot = panel.Clone();
        snapshot.Normalize();
        var result = await inner.CreatePosterAsync(ToPanelLibraryItem(snapshot), cancellationToken).ConfigureAwait(false);
        return new PanelPosterResult(result.Frame, result.WidgetErrors);
    }

    internal sealed class PanelCompositionSession : IDisposable
    {
        private readonly PanelDefinition panel;
        private readonly PanelFrameComposer.PanelCompositionSession inner;

        public PanelCompositionSession(PanelDefinition panel, PanelFrameComposer.PanelCompositionSession inner)
        {
            this.panel = panel;
            this.inner = inner;
        }

        public PanelDefinition Panel => panel.Clone();

        public IReadOnlyDictionary<string, string> GetWidgetErrors() => inner.GetWidgetErrors();

        public RgbaColor[] RenderFrame(DateTimeOffset utcNow) => inner.RenderFrame(utcNow);

        public void RenderFrameInto(DateTimeOffset utcNow, RgbaColor[] targetFrame)
            => inner.RenderFrameInto(utcNow, targetFrame);

        public void Dispose() => inner.Dispose();
    }

    internal sealed record PanelPosterResult(RgbaColor[] Frame, IReadOnlyDictionary<string, string> WidgetErrors);

    internal static PanelLibraryItem ToPanelLibraryItem(PanelDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PanelLibraryItem
        {
            PanelId = source.PanelId,
            Name = source.Name,
            Width = source.Width,
            Height = source.Height,
            IsEnabled = true,
            Widgets = source.Widgets.Select(static widget => new PanelWidgetItem
            {
                WidgetId = widget.WidgetId,
                AppId = widget.AppId,
                X = widget.X,
                Y = widget.Y,
                Width = widget.Width,
                Height = widget.Height,
                ZIndex = widget.ZIndex,
                ConfigValues = new Dictionary<string, string>(widget.ConfigValues, StringComparer.OrdinalIgnoreCase),
                RuntimeState = new Dictionary<string, string>(widget.RuntimeState, StringComparer.OrdinalIgnoreCase),
            }).ToArray(),
        };
    }

    private sealed class WinUiPanelMediaSourceResolver : IPanelMediaSourceResolver
    {
        private readonly IDeviceServerClient deviceServerClient;
        private readonly FileSystemPanelMediaSourceResolver fileSystemResolver = new();

        public WinUiPanelMediaSourceResolver(IDeviceServerClient deviceServerClient)
        {
            this.deviceServerClient = deviceServerClient;
        }

        public async Task<IReadOnlyList<PanelMediaSource>> ResolveAsync(PanelWidgetItem widget, CancellationToken cancellationToken = default)
        {
            var local = await fileSystemResolver.ResolveAsync(widget, cancellationToken).ConfigureAwait(false);
            if (local.Count > 0)
            {
                return local;
            }

            var mediaIds = ResolveMediaIds(widget).ToArray();
            if (mediaIds.Length == 0)
            {
                return Array.Empty<PanelMediaSource>();
            }

            var sources = new List<PanelMediaSource>(mediaIds.Length);
            foreach (var mediaId in mediaIds)
            {
                var payload = await deviceServerClient.DownloadMediaAsync(mediaId, cancellationToken).ConfigureAwait(false);
                if (payload is null || payload.Length == 0)
                {
                    continue;
                }

                sources.Add(new PanelMediaSource(mediaId, payload, ResolveContentType(mediaId), mediaId));
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
}
