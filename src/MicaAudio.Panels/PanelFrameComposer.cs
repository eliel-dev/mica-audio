using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Device.Protocol.Models;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace MicaAudio.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-compartilhado
// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
public sealed class PanelFrameComposer
{
    public const int TargetFps = 30;

    private static readonly HashSet<string> SupportedWidgetAppIds =
        new(StringComparer.OrdinalIgnoreCase) { "analogclock", "gifhub75", "weather", "accuweather", "status" };
    private static readonly HashSet<string> ClientOnlyWidgetAppIds =
        new(StringComparer.OrdinalIgnoreCase) { "pcmetrics", "metrics", "audiovisualizer", "visualizer", "spectrum", "hub75visualizer" };
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    private readonly Hub75GifDecoder decoder;
    private readonly PanelsMediaCache mediaCache;
    private readonly IPanelMediaSourceResolver mediaSourceResolver;

    public PanelFrameComposer()
        : this(new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames), new PanelsMediaCache(), new FileSystemPanelMediaSourceResolver())
    {
    }

    public PanelFrameComposer(IPanelMediaSourceResolver mediaSourceResolver)
        : this(new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames), new PanelsMediaCache(), mediaSourceResolver)
    {
    }

    public PanelFrameComposer(Hub75GifDecoder decoder, PanelsMediaCache mediaCache, IPanelMediaSourceResolver mediaSourceResolver)
    {
        this.decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        this.mediaCache = mediaCache ?? throw new ArgumentNullException(nameof(mediaCache));
        this.mediaSourceResolver = mediaSourceResolver ?? throw new ArgumentNullException(nameof(mediaSourceResolver));
    }

    public static bool SupportsWidgetApp(string? appId)
        => !string.IsNullOrWhiteSpace(appId) && SupportedWidgetAppIds.Contains(appId.Trim());

    public static bool IsClientOnlyWidgetApp(string? appId)
        => !string.IsNullOrWhiteSpace(appId) && ClientOnlyWidgetAppIds.Contains(appId.Trim());

    public Task<PanelCompositionSession> CreateSessionAsync(PanelLibraryItem panel, CancellationToken cancellationToken = default)
        => CreateSessionAsync(panel, PanelRenderIntent.Animated, cancellationToken);

    public async Task<PanelCompositionSession> CreateSessionAsync(
        PanelLibraryItem panel,
        PanelRenderIntent renderIntent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var normalizedPanel = NormalizePanel(panel);
        var runtimes = new List<IPanelWidgetRuntime>(normalizedPanel.Widgets.Count);
        foreach (var widget in normalizedPanel.Widgets.OrderBy(static widget => widget.ZIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtimes.Add(await CreateRuntimeAsync(widget, renderIntent, cancellationToken).ConfigureAwait(false));
        }

        return new PanelCompositionSession(normalizedPanel, runtimes);
    }

    public async Task<PanelPosterResult> CreatePosterAsync(PanelLibraryItem panel, CancellationToken cancellationToken = default)
    {
        using var session = await CreateSessionAsync(panel, PanelRenderIntent.Poster, cancellationToken).ConfigureAwait(false);
        return new PanelPosterResult(session.RenderFrame(DateTimeOffset.UtcNow), session.GetWidgetErrors(), session.GetSkippedWidgets());
    }

    private async Task<IPanelWidgetRuntime> CreateRuntimeAsync(PanelWidgetItem widget, PanelRenderIntent renderIntent, CancellationToken cancellationToken)
    {
        return widget.AppId.Trim().ToLowerInvariant() switch
        {
            "analogclock" => new ClockWidgetRuntime(widget),
            "gifhub75" => await GifWidgetRuntime.CreateAsync(widget, decoder, mediaCache, mediaSourceResolver, renderIntent, cancellationToken).ConfigureAwait(false),
            "weather" or "accuweather" => new WeatherWidgetRuntime(widget),
            "status" => new StatusWidgetRuntime(widget),
            _ when IsClientOnlyWidgetApp(widget.AppId) => new SkippedWidgetRuntime(widget, "Widget depende de dados do cliente WinUI."),
            _ => new SkippedWidgetRuntime(widget, "App sem renderer server-side no compositor de paineis."),
        };
    }

    private static PanelLibraryItem NormalizePanel(PanelLibraryItem source)
    {
        var panelId = string.IsNullOrWhiteSpace(source.PanelId) ? Guid.NewGuid().ToString("N") : source.PanelId.Trim();
        var width = source.Width <= 0 ? LedDefaults.MatrixWidth : source.Width;
        var height = source.Height <= 0 ? LedDefaults.MatrixHeight : source.Height;
        return new PanelLibraryItem
        {
            PanelId = panelId,
            Name = string.IsNullOrWhiteSpace(source.Name) ? "Painel" : source.Name.Trim(),
            Width = width,
            Height = height,
            IsEnabled = source.IsEnabled,
            Widgets = source.Widgets
                .Where(static widget => !string.IsNullOrWhiteSpace(widget.WidgetId))
                .Select(widget => NormalizeWidget(widget, width, height))
                .OrderBy(static widget => widget.ZIndex)
                .ThenBy(static widget => widget.WidgetId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };
    }

    private static PanelWidgetItem NormalizeWidget(PanelWidgetItem source, int panelWidth, int panelHeight)
    {
        var width = Math.Clamp(source.Width <= 0 ? 64 : source.Width, 1, Math.Max(1, panelWidth));
        var height = Math.Clamp(source.Height <= 0 ? 32 : source.Height, 1, Math.Max(1, panelHeight));
        return new PanelWidgetItem
        {
            WidgetId = string.IsNullOrWhiteSpace(source.WidgetId) ? Guid.NewGuid().ToString("N") : source.WidgetId.Trim(),
            AppId = string.IsNullOrWhiteSpace(source.AppId) ? string.Empty : source.AppId.Trim(),
            X = Math.Clamp(source.X, 0, Math.Max(0, panelWidth - width)),
            Y = Math.Clamp(source.Y, 0, Math.Max(0, panelHeight - height)),
            Width = width,
            Height = height,
            ZIndex = source.ZIndex,
            ConfigValues = new Dictionary<string, string>(source.ConfigValues ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
            RuntimeState = new Dictionary<string, string>(source.RuntimeState ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
        };
    }

    public sealed class PanelCompositionSession : IDisposable
    {
        private readonly PanelLibraryItem panel;
        private readonly List<IPanelWidgetRuntime> widgetRuntimes;

        internal PanelCompositionSession(PanelLibraryItem panel, List<IPanelWidgetRuntime> widgetRuntimes)
        {
            this.panel = panel;
            this.widgetRuntimes = widgetRuntimes;
        }

        public PanelLibraryItem Panel => panel;

        public IReadOnlyDictionary<string, string> GetWidgetErrors()
        {
            return widgetRuntimes
                .Where(static runtime => !string.IsNullOrWhiteSpace(runtime.ErrorMessage))
                .ToDictionary(runtime => runtime.WidgetId, runtime => runtime.ErrorMessage!, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<string> GetSkippedWidgets()
        {
            return widgetRuntimes
                .Where(static runtime => runtime.IsSkipped)
                .Select(static runtime => runtime.WidgetId)
                .ToArray();
        }

        public RgbaColor[] RenderFrame(DateTimeOffset utcNow)
        {
            var frame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
            RenderFrameInto(utcNow, frame);
            return frame;
        }

        public void RenderFrameInto(DateTimeOffset utcNow, RgbaColor[] targetFrame)
        {
            ArgumentNullException.ThrowIfNull(targetFrame);
            if (targetFrame.Length != LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
            {
                throw new ArgumentException(
                    $"Target frame has {targetFrame.Length} pixels but expected {LedDefaults.MatrixWidth * LedDefaults.MatrixHeight}.",
                    nameof(targetFrame));
            }

            PanelsMatrixDrawHelpers.Clear(targetFrame);
            foreach (var runtime in widgetRuntimes)
            {
                runtime.Render(utcNow, targetFrame, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight);
            }
        }

        public void Dispose()
        {
            foreach (var runtime in widgetRuntimes)
            {
                runtime.Dispose();
            }

            widgetRuntimes.Clear();
        }
    }

    public sealed record PanelPosterResult(
        RgbaColor[] Frame,
        IReadOnlyDictionary<string, string> WidgetErrors,
        IReadOnlyList<string> SkippedWidgets);

    internal interface IPanelWidgetRuntime : IDisposable
    {
        string WidgetId { get; }

        string? ErrorMessage { get; }

        bool IsSkipped { get; }

        void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight);
    }

    private sealed class SkippedWidgetRuntime : IPanelWidgetRuntime
    {
        private readonly string reason;

        public SkippedWidgetRuntime(PanelWidgetItem widget, string reason)
        {
            WidgetId = widget.WidgetId;
            this.reason = reason;
        }

        public string WidgetId { get; }

        public string? ErrorMessage => reason;

        public bool IsSkipped => true;

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ClockWidgetRuntime : IPanelWidgetRuntime
    {
        private readonly PanelWidgetItem widget;
        private readonly bool use24Hour;
        private readonly RgbaColor color;

        public ClockWidgetRuntime(PanelWidgetItem widget)
        {
            this.widget = widget;
            use24Hour = !widget.ConfigValues.TryGetValue("format24h", out var raw24h)
                || !bool.TryParse(raw24h, out var parsed24h)
                || parsed24h;
            color = ResolveClockColor(widget.ConfigValues.TryGetValue("fontColor", out var rawColor) ? rawColor : null);
        }

        public string WidgetId => widget.WidgetId;

        public string? ErrorMessage => null;

        public bool IsSkipped => false;

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
            var now = TimeZoneInfo.ConvertTime(utcNow, BrasiliaTimeZone).DateTime;
            var format = use24Hour ? "HH:mm" : "hh:mm";
            var timeText = now.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            var timeWidth = Math.Max(1, (timeText.Length * 6) - 1);
            var timeX = widget.X + Math.Max(0, (widget.Width - timeWidth) / 2);
            var timeY = widget.Y + Math.Max(0, Math.Min(widget.Height - 7, Math.Max(0, (widget.Height / 2) - 7)));

            PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, timeX, timeY, timeText, color);

            if (!use24Hour && widget.Height >= 16)
            {
                var period = now.ToString("tt", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
                var periodWidth = Math.Max(1, (period.Length * 6) - 1);
                var periodX = widget.X + Math.Max(0, widget.Width - periodWidth - 1);
                PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, periodX, widget.Y + 1, period, new RgbaColor(192, 204, 228, 255));
            }

            if (widget.Height >= 20)
            {
                PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, widget.X + 1, widget.Y + widget.Height - 8, "BRT", new RgbaColor(150, 185, 225, 255));
            }

            if (widget.Height >= 10 && widget.Width >= 12)
            {
                var progressWidth = Math.Clamp((int)Math.Round(((now.Second + 1) / 60d) * Math.Max(1, widget.Width - 2)), 0, Math.Max(1, widget.Width - 2));
                for (var offset = 0; offset < progressWidth; offset++)
                {
                    PanelsMatrixDrawHelpers.DrawPixel(
                        targetFrame,
                        panelWidth,
                        panelHeight,
                        widget.X + 1 + offset,
                        widget.Y + widget.Height - 1,
                        ResolveSpectrumColor(offset / (float)Math.Max(1, widget.Width - 2)));
                }
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class WeatherWidgetRuntime : IPanelWidgetRuntime
    {
        private readonly PanelWidgetItem widget;
        private readonly string temperatureText;
        private readonly string conditionText;

        public WeatherWidgetRuntime(PanelWidgetItem widget)
        {
            this.widget = widget;
            temperatureText = ResolveValue(widget, "temperature", "temp", "--C");
            conditionText = ResolveValue(widget, "condition", "summary", "CLIMA");
        }

        public string WidgetId => widget.WidgetId;

        public string? ErrorMessage => null;

        public bool IsSkipped => false;

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
            PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, widget.X + 1, widget.Y + 1, conditionText, new RgbaColor(90, 210, 255, 255));
            if (widget.Height >= 18)
            {
                PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, widget.X + 1, widget.Y + 11, temperatureText, new RgbaColor(255, 230, 120, 255));
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class StatusWidgetRuntime : IPanelWidgetRuntime
    {
        private readonly PanelWidgetItem widget;

        public StatusWidgetRuntime(PanelWidgetItem widget)
        {
            this.widget = widget;
        }

        public string WidgetId => widget.WidgetId;

        public string? ErrorMessage => null;

        public bool IsSkipped => false;

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
            var now = TimeZoneInfo.ConvertTime(utcNow, BrasiliaTimeZone).DateTime;
            PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, widget.X + 1, widget.Y + 1, "SERVER", new RgbaColor(135, 255, 170, 255));
            if (widget.Height >= 18)
            {
                PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, widget.X + 1, widget.Y + 11, now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), new RgbaColor(230, 235, 255, 255));
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class GifWidgetRuntime : IPanelWidgetRuntime
    {
        private readonly PanelWidgetItem widget;
        private readonly Hub75GifDecoder decoder;
        private readonly PanelsMediaCache mediaCache;
        private readonly IPanelMediaSourceResolver mediaSourceResolver;
        private readonly PanelRenderIntent renderIntent;
        private readonly CancellationTokenSource lifetimeCts = new();
        private readonly List<PanelMediaSource> mediaSources = [];
        private readonly object stateGate = new();
        private AnimatedMediaSequence mediaSequence = AnimatedMediaSequence.Empty;
        private DateTimeOffset mediaStartedUtc = DateTimeOffset.UtcNow;
        private DateTimeOffset nextSlideUtc = DateTimeOffset.MaxValue;
        private int sourceIndex;
        private bool slideshow;
        private int slideshowIntervalMs = 10_000;
        private string? errorMessage;
        private bool switchInProgress;
        private bool skipped;

        private GifWidgetRuntime(
            PanelWidgetItem widget,
            Hub75GifDecoder decoder,
            PanelsMediaCache mediaCache,
            IPanelMediaSourceResolver mediaSourceResolver,
            PanelRenderIntent renderIntent)
        {
            this.widget = widget;
            this.decoder = decoder;
            this.mediaCache = mediaCache;
            this.mediaSourceResolver = mediaSourceResolver;
            this.renderIntent = renderIntent;
        }

        public string WidgetId => widget.WidgetId;

        public string? ErrorMessage
        {
            get
            {
                lock (stateGate)
                {
                    return errorMessage;
                }
            }
        }

        public bool IsSkipped
        {
            get
            {
                lock (stateGate)
                {
                    return skipped;
                }
            }
        }

        public static async Task<GifWidgetRuntime> CreateAsync(
            PanelWidgetItem widget,
            Hub75GifDecoder decoder,
            PanelsMediaCache mediaCache,
            IPanelMediaSourceResolver mediaSourceResolver,
            PanelRenderIntent renderIntent,
            CancellationToken cancellationToken)
        {
            var runtime = new GifWidgetRuntime(widget, decoder, mediaCache, mediaSourceResolver, renderIntent);
            await runtime.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return runtime;
        }

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
            AnimatedMediaSequence localSequence;
            DateTimeOffset localMediaStartedUtc;
            PanelMediaSource? nextSlideSource = null;
            int nextSlideIndex = -1;

            lock (stateGate)
            {
                if (renderIntent == PanelRenderIntent.Animated
                    && slideshow
                    && mediaSources.Count > 1
                    && utcNow >= nextSlideUtc
                    && !switchInProgress)
                {
                    nextSlideIndex = (sourceIndex + 1) % mediaSources.Count;
                    nextSlideSource = mediaSources[nextSlideIndex];
                    switchInProgress = true;
                }

                localSequence = mediaSequence;
                localMediaStartedUtc = mediaStartedUtc;
                if (localSequence.IsEmpty)
                {
                    return;
                }
            }

            if (nextSlideSource is not null && nextSlideIndex >= 0)
            {
                _ = BeginSlideSwitchAsync(nextSlideIndex, nextSlideSource);
            }

            var index = ResolveAnimatedFrameIndex(localSequence, utcNow - localMediaStartedUtc);

            PanelsMatrixDrawHelpers.Blit(
                localSequence.Frames[index].Pixels,
                widget.Width,
                widget.Height,
                targetFrame,
                panelWidth,
                panelHeight,
                widget.X,
                widget.Y);
        }

        public void Dispose()
        {
            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var sources = await mediaSourceResolver.ResolveAsync(widget, cancellationToken).ConfigureAwait(false);
            if (sources.Count == 0)
            {
                SetError("Widget GIF sem midia server-safe.", markSkipped: true);
                return;
            }

            mediaSources.AddRange(sources);
            slideshow = string.Equals(widget.ConfigValues.TryGetValue("sourceType", out var sourceType) ? sourceType : null, "slideshow", StringComparison.OrdinalIgnoreCase)
                || sources.Count > 1;
            slideshowIntervalMs = ParseSlideshowIntervalMs(widget.ConfigValues);

            if (slideshow && ParseShuffle(widget.ConfigValues))
            {
                var shuffled = mediaSources.OrderBy(static _ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray();
                mediaSources.Clear();
                mediaSources.AddRange(shuffled);
            }

            try
            {
                if (renderIntent == PanelRenderIntent.Poster)
                {
                    await LoadPosterFrameAsync(mediaSources[0], cancellationToken).ConfigureAwait(false);
                    return;
                }

                await LoadFramesForSourceAsync(mediaSources[0], cancellationToken).ConfigureAwait(false);
                lock (stateGate)
                {
                    nextSlideUtc = DateTimeOffset.UtcNow.AddMilliseconds(slideshowIntervalMs);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                SetError("Falha ao abrir midia do widget GIF.", markSkipped: true);
            }
        }

        private async Task LoadFramesForSourceAsync(PanelMediaSource source, CancellationToken cancellationToken)
        {
            var scaleMode = ParseGifScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);
            var cacheKey = BuildMediaCacheKey(source.CacheKey, widget.Width, widget.Height, scaleMode, posterOnly: false);
            var loadedFrames = await mediaCache.GetOrCreateAnimatedAsync(
                cacheKey,
                token => DecodeFramesCoreAsync(decoder, source, widget.Width, widget.Height, scaleMode, token),
                cancellationToken).ConfigureAwait(false);
            if (loadedFrames.IsEmpty)
            {
                throw new InvalidDataException("Midia sem frames validos.");
            }

            lock (stateGate)
            {
                mediaSequence = loadedFrames;
                mediaStartedUtc = DateTimeOffset.UtcNow;
                errorMessage = null;
                skipped = false;
            }
        }

        private async Task LoadPosterFrameAsync(PanelMediaSource source, CancellationToken cancellationToken)
        {
            var scaleMode = ParseGifScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);
            var cacheKey = BuildMediaCacheKey(source.CacheKey, widget.Width, widget.Height, scaleMode, posterOnly: true);
            var posterFrame = await mediaCache.GetOrCreatePosterAsync(
                cacheKey,
                async token =>
                {
                    var sequence = await DecodeFramesCoreAsync(decoder, source, widget.Width, widget.Height, scaleMode, token, firstFrameOnly: true).ConfigureAwait(false);
                    return sequence.IsEmpty ? Array.Empty<RgbaColor>() : sequence.Frames[0].Pixels;
                },
                cancellationToken).ConfigureAwait(false);
            if (posterFrame.Length == 0)
            {
                throw new InvalidDataException("Midia sem poster valido.");
            }

            lock (stateGate)
            {
                mediaSequence = new AnimatedMediaSequence([new AnimatedMediaFrame(posterFrame, Hub75GifDecoder.DefaultFrameDurationMs)], Hub75GifDecoder.DefaultFrameDurationMs);
                mediaStartedUtc = DateTimeOffset.UtcNow;
                errorMessage = null;
                skipped = false;
            }
        }

        private async Task BeginSlideSwitchAsync(int nextIndex, PanelMediaSource source)
        {
            try
            {
                await LoadFramesForSourceAsync(source, lifetimeCts.Token).ConfigureAwait(false);
                lock (stateGate)
                {
                    sourceIndex = nextIndex;
                    nextSlideUtc = DateTimeOffset.UtcNow.AddMilliseconds(slideshowIntervalMs);
                    switchInProgress = false;
                }
            }
            catch (OperationCanceledException)
            {
                lock (stateGate)
                {
                    switchInProgress = false;
                }
            }
            catch
            {
                lock (stateGate)
                {
                    switchInProgress = false;
                }

                SetError("Falha ao alternar midia do slideshow.", clearFrames: false);
            }
        }

        private void SetError(string message, bool clearFrames = true, bool markSkipped = false)
        {
            lock (stateGate)
            {
                errorMessage = message;
                skipped = markSkipped;
                if (clearFrames)
                {
                    mediaSequence = AnimatedMediaSequence.Empty;
                }
            }
        }
    }

    private static async Task<AnimatedMediaSequence> DecodeFramesCoreAsync(
        Hub75GifDecoder decoder,
        PanelMediaSource source,
        int targetWidth,
        int targetHeight,
        GifScaleMode scaleMode,
        CancellationToken cancellationToken,
        bool firstFrameOnly = false)
    {
        if (IsGif(source))
        {
            if (firstFrameOnly)
            {
                var firstFrame = await Task.Run(() => Hub75GifDecoder.DecodeFirstFrame(source.Payload, cancellationToken), cancellationToken).ConfigureAwait(false);
                return CreateAnimatedSequence([firstFrame], targetWidth, targetHeight, scaleMode);
            }

            var decodedFrames = await Task.Run(() => decoder.Decode(source.Payload, cancellationToken), cancellationToken).ConfigureAwait(false);
            return CreateAnimatedSequence(decodedFrames, targetWidth, targetHeight, scaleMode);
        }

        var imageFrame = await Task.Run(() => Hub75GifDecoder.DecodeStaticImage(source.Payload, cancellationToken), cancellationToken).ConfigureAwait(false);
        return CreateAnimatedSequence([imageFrame], targetWidth, targetHeight, scaleMode);
    }

    private static bool IsGif(PanelMediaSource source)
    {
        return string.Equals(source.ContentType, "image/gif", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(source.FileName), ".gif", StringComparison.OrdinalIgnoreCase)
            || source.Payload is { Length: >= 6 }
                && source.Payload[0] == (byte)'G'
                && source.Payload[1] == (byte)'I'
                && source.Payload[2] == (byte)'F';
    }

    public static int ResolveAnimatedFrameIndex(AnimatedMediaSequence sequence, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        if (sequence.Count <= 1 || sequence.TotalDurationMs <= 0)
        {
            return 0;
        }

        var elapsedMs = Math.Max(0L, (long)elapsed.TotalMilliseconds);
        var timeInLoopMs = elapsedMs % sequence.TotalDurationMs;
        long cumulativeMs = 0;
        for (var i = 0; i < sequence.Count; i++)
        {
            cumulativeMs += Math.Max(1, sequence.Frames[i].DurationMs);
            if (timeInLoopMs < cumulativeMs)
            {
                return i;
            }
        }

        return sequence.Count - 1;
    }

    public static RgbaColor[] FormatToTarget(DecodedGifFrame sourceFrame, int targetWidth, int targetHeight, GifScaleMode scaleMode)
    {
        var safeWidth = Math.Max(1, targetWidth);
        var safeHeight = Math.Max(1, targetHeight);
        var target = new RgbaColor[safeWidth * safeHeight];
        if (sourceFrame.Width <= 0 || sourceFrame.Height <= 0)
        {
            PanelsMatrixDrawHelpers.Clear(target);
            return target;
        }

        if (sourceFrame.Width == safeWidth && sourceFrame.Height == safeHeight)
        {
            return sourceFrame.Pixels;
        }

        PanelsMatrixDrawHelpers.Clear(target);

        if (scaleMode == GifScaleMode.Stretch)
        {
            BlitScaled(sourceFrame.Pixels, sourceFrame.Width, sourceFrame.Height, target, safeWidth, safeHeight, 0f, 0f, safeWidth, safeHeight);
            return target;
        }

        var widthScale = safeWidth / (float)sourceFrame.Width;
        var heightScale = safeHeight / (float)sourceFrame.Height;
        var scale = scaleMode == GifScaleMode.Fill
            ? MathF.Max(widthScale, heightScale)
            : MathF.Min(widthScale, heightScale);
        scale = MathF.Max(scale, 0.0001f);

        var drawWidth = Math.Max(1, (int)MathF.Round(sourceFrame.Width * scale));
        var drawHeight = Math.Max(1, (int)MathF.Round(sourceFrame.Height * scale));
        var offsetX = (safeWidth - drawWidth) / 2f;
        var offsetY = (safeHeight - drawHeight) / 2f;
        BlitScaled(sourceFrame.Pixels, sourceFrame.Width, sourceFrame.Height, target, safeWidth, safeHeight, offsetX, offsetY, drawWidth, drawHeight);
        return target;
    }

    private static AnimatedMediaSequence CreateAnimatedSequence(
        IReadOnlyList<DecodedGifFrame> decodedFrames,
        int targetWidth,
        int targetHeight,
        GifScaleMode scaleMode)
    {
        if (decodedFrames.Count == 0)
        {
            return AnimatedMediaSequence.Empty;
        }

        var frames = new AnimatedMediaFrame[decodedFrames.Count];
        var totalDurationMs = 0;
        for (var i = 0; i < decodedFrames.Count; i++)
        {
            var decodedFrame = decodedFrames[i];
            var durationMs = Math.Max(1, decodedFrame.DurationMs);
            frames[i] = new AnimatedMediaFrame(FormatToTarget(decodedFrame, targetWidth, targetHeight, scaleMode), durationMs);
            totalDurationMs += durationMs;
        }

        return new AnimatedMediaSequence(frames, totalDurationMs);
    }

    private static void BlitScaled(
        RgbaColor[] sourcePixels,
        int sourceWidth,
        int sourceHeight,
        RgbaColor[] targetPixels,
        int targetWidth,
        int targetHeight,
        float drawOffsetX,
        float drawOffsetY,
        int drawWidth,
        int drawHeight)
    {
        for (var targetY = 0; targetY < targetHeight; targetY++)
        {
            var localY = targetY - drawOffsetY;
            if (localY < 0f || localY >= drawHeight)
            {
                continue;
            }

            var sourceY = Math.Clamp((int)MathF.Floor((localY * sourceHeight) / drawHeight), 0, sourceHeight - 1);
            for (var targetX = 0; targetX < targetWidth; targetX++)
            {
                var localX = targetX - drawOffsetX;
                if (localX < 0f || localX >= drawWidth)
                {
                    continue;
                }

                var sourceX = Math.Clamp((int)MathF.Floor((localX * sourceWidth) / drawWidth), 0, sourceWidth - 1);
                targetPixels[(targetY * targetWidth) + targetX] = sourcePixels[(sourceY * sourceWidth) + sourceX];
            }
        }
    }

    private static RgbaColor ResolveClockColor(string? colorKey)
    {
        return colorKey?.Trim().ToLowerInvariant() switch
        {
            "white" => new RgbaColor(255, 255, 255, 255),
            "green" => new RgbaColor(70, 255, 135, 255),
            "yellow" => new RgbaColor(255, 225, 85, 255),
            "orange" => new RgbaColor(255, 153, 67, 255),
            "magenta" => new RgbaColor(255, 95, 230, 255),
            "cyan" => new RgbaColor(74, 222, 255, 255),
            _ => new RgbaColor(74, 222, 255, 255),
        };
    }

    private static RgbaColor ResolveSpectrumColor(float fraction)
    {
        var t = Math.Clamp(fraction, 0f, 1f);
        var r = (byte)Math.Clamp(65f + (170f * t), 0f, 255f);
        var g = (byte)Math.Clamp(150f + (95f * (1f - MathF.Abs((2f * t) - 1f))), 0f, 255f);
        var b = (byte)Math.Clamp(45f + (170f * (1f - t)), 0f, 255f);
        return new RgbaColor(r, g, b, 255);
    }

    private static GifScaleMode ParseGifScaleMode(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "fill" => GifScaleMode.Fill,
            "stretch" => GifScaleMode.Stretch,
            _ => GifScaleMode.Fit,
        };
    }

    private static int ParseSlideshowIntervalMs(IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue("slideshowInterval", out var raw);
        return raw?.Trim().ToLowerInvariant() switch
        {
            "5s" => 5_000,
            "30s" => 30_000,
            "1min" => 60_000,
            "5min" => 300_000,
            "10min" => 600_000,
            _ => 10_000,
        };
    }

    private static bool ParseShuffle(IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue("slideshowShuffle", out var raw);
        return bool.TryParse(raw, out var parsed) && parsed;
    }

    private static string BuildMediaCacheKey(string sourceKey, int width, int height, GifScaleMode scaleMode, bool posterOnly)
    {
        return $"{sourceKey.Trim()}|{width}x{height}|{scaleMode}|{(posterOnly ? "poster" : "animated")}";
    }

    private static string ResolveValue(PanelWidgetItem widget, string configKey, string runtimeKey, string fallback)
    {
        if (TryGetValue(widget.RuntimeState, runtimeKey, out var runtimeValue))
        {
            return runtimeValue;
        }

        return TryGetValue(widget.ConfigValues, configKey, out var configValue)
            ? configValue
            : fallback;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, string> values, string key, [NotNullWhen(true)] out string? value)
    {
        if (values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            value = raw.Trim();
            return true;
        }

        value = null;
        return false;
    }

    private static TimeZoneInfo ResolveBrasiliaTimeZone()
    {
        foreach (var candidate in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(candidate, out var windowsId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }

                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}

public enum PanelRenderIntent
{
    Poster,
    Animated,
}
