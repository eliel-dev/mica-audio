using System.Security.Cryptography;
using App.WinUI.Models.Panels;
using App.WinUI.Services.Gif;
using Device.Protocol.Models;
using ImageMagick;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-hub75
// DOCS: docs/handoffs/2026-04-18-panels-webp-batch-pipeline-optimizations.md
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
internal sealed class PanelsFrameComposer
{
    public const int TargetFps = 30;
    private static readonly HashSet<string> SupportedWidgetAppIds =
        new(StringComparer.OrdinalIgnoreCase) { "analogclock", "gifhub75" };
    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp", ".webp" };
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    private readonly Hub75GifDecoder decoder;
    private readonly PanelsMediaCache mediaCache;
    private readonly IPanelMediaSourceResolver mediaSourceResolver;

    public PanelsFrameComposer()
        : this(new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames), new PanelsMediaCache(), new LocalPanelMediaSourceResolver())
    {
    }

    internal PanelsFrameComposer(
        Hub75GifDecoder decoder,
        PanelsMediaCache mediaCache,
        IPanelMediaSourceResolver? mediaSourceResolver = null)
    {
        this.decoder = decoder;
        this.mediaCache = mediaCache;
        this.mediaSourceResolver = mediaSourceResolver ?? new LocalPanelMediaSourceResolver();
    }

    internal static bool SupportsWidgetApp(string? appId)
    {
        return !string.IsNullOrWhiteSpace(appId) && SupportedWidgetAppIds.Contains(appId.Trim());
    }

    internal async Task<PanelCompositionSession> CreateSessionAsync(PanelDefinition panel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var normalizedPanel = panel.Clone();
        normalizedPanel.Normalize();
        var runtimes = await CreateRuntimesAsync(normalizedPanel, RenderIntent.Animated, cancellationToken).ConfigureAwait(false);
        return new PanelCompositionSession(normalizedPanel, runtimes);
    }

    internal async Task<PanelPosterResult> CreatePosterAsync(PanelDefinition panel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var normalizedPanel = panel.Clone();
        normalizedPanel.Normalize();
        var runtimes = await CreateRuntimesAsync(normalizedPanel, RenderIntent.Poster, cancellationToken).ConfigureAwait(false);
        using var session = new PanelCompositionSession(normalizedPanel, runtimes);
        return new PanelPosterResult(session.RenderFrame(DateTimeOffset.UtcNow), session.GetWidgetErrors());
    }

    private async Task<List<IPanelWidgetRuntime>> CreateRuntimesAsync(PanelDefinition panel, RenderIntent renderIntent, CancellationToken cancellationToken)
    {
        var runtimes = new List<IPanelWidgetRuntime>(panel.Widgets.Count);
        foreach (var widget in panel.Widgets.OrderBy(static widget => widget.ZIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtimes.Add(await CreateRuntimeAsync(widget, renderIntent, cancellationToken).ConfigureAwait(false));
        }

        return runtimes;
    }

    private async Task<IPanelWidgetRuntime> CreateRuntimeAsync(PanelWidgetDefinition widget, RenderIntent renderIntent, CancellationToken cancellationToken)
    {
        return widget.AppId.Trim().ToLowerInvariant() switch
        {
            "analogclock" => new ClockWidgetRuntime(widget.Clone()),
            "gifhub75" => await GifWidgetRuntime.CreateAsync(widget.Clone(), decoder, mediaCache, mediaSourceResolver, renderIntent, cancellationToken).ConfigureAwait(false),
            _ => new EmptyWidgetRuntime(widget.Clone()),
        };
    }

    internal sealed class PanelCompositionSession : IDisposable
    {
        private readonly PanelDefinition panel;
        private readonly List<IPanelWidgetRuntime> widgetRuntimes;

        internal PanelCompositionSession(PanelDefinition panel, List<IPanelWidgetRuntime> widgetRuntimes)
        {
            this.panel = panel;
            this.widgetRuntimes = widgetRuntimes;
        }

        public PanelDefinition Panel => panel.Clone();

        public IReadOnlyDictionary<string, string> GetWidgetErrors()
        {
            return widgetRuntimes
                .Where(static runtime => !string.IsNullOrWhiteSpace(runtime.ErrorMessage))
                .ToDictionary(runtime => runtime.WidgetId, runtime => runtime.ErrorMessage!, StringComparer.OrdinalIgnoreCase);
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

    internal sealed record PanelPosterResult(RgbaColor[] Frame, IReadOnlyDictionary<string, string> WidgetErrors);

    internal interface IPanelWidgetRuntime : IDisposable
    {
        string WidgetId { get; }

        string? ErrorMessage { get; }

        void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight);
    }

    private enum RenderIntent
    {
        Poster,
        Animated,
    }

    private sealed class EmptyWidgetRuntime : IPanelWidgetRuntime
    {
        public EmptyWidgetRuntime(PanelWidgetDefinition widget)
        {
            WidgetId = widget.WidgetId;
        }

        public string WidgetId { get; }

        public string? ErrorMessage => "App sem renderer HUB75 no compositor de paineis.";

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ClockWidgetRuntime : IPanelWidgetRuntime
    {
        private readonly PanelWidgetDefinition widget;
        private readonly bool use24Hour;
        private readonly RgbaColor color;

        public ClockWidgetRuntime(PanelWidgetDefinition widget)
        {
            this.widget = widget;
            widget.Normalize(LedDefaults.MatrixWidth, LedDefaults.MatrixHeight);
            use24Hour = !widget.ConfigValues.TryGetValue("format24h", out var raw24h)
                || !bool.TryParse(raw24h, out var parsed24h)
                || parsed24h;
            color = ResolveClockColor(widget.ConfigValues.TryGetValue("fontColor", out var rawColor) ? rawColor : null);
        }

        public string WidgetId => widget.WidgetId;

        public string? ErrorMessage => null;

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

    private sealed class GifWidgetRuntime : IPanelWidgetRuntime
    {
        private readonly PanelWidgetDefinition widget;
        private readonly Hub75GifDecoder decoder;
        private readonly PanelsMediaCache mediaCache;
        private readonly IPanelMediaSourceResolver mediaSourceResolver;
        private readonly RenderIntent renderIntent;
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

        private GifWidgetRuntime(
            PanelWidgetDefinition widget,
            Hub75GifDecoder decoder,
            PanelsMediaCache mediaCache,
            IPanelMediaSourceResolver mediaSourceResolver,
            RenderIntent renderIntent)
        {
            this.widget = widget;
            this.decoder = decoder;
            this.mediaCache = mediaCache;
            this.mediaSourceResolver = mediaSourceResolver;
            this.renderIntent = renderIntent;
            widget.Normalize(LedDefaults.MatrixWidth, LedDefaults.MatrixHeight);
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

        public static async Task<GifWidgetRuntime> CreateAsync(
            PanelWidgetDefinition widget,
            Hub75GifDecoder decoder,
            PanelsMediaCache mediaCache,
            IPanelMediaSourceResolver mediaSourceResolver,
            RenderIntent renderIntent,
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
                if (renderIntent == RenderIntent.Animated
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
                SetError("Widget GIF sem midia resolvida.");
                return;
            }

            slideshow = string.Equals(widget.ConfigValues.TryGetValue("sourceType", out var sourceType) ? sourceType : null, "slideshow", StringComparison.OrdinalIgnoreCase)
                || sources.Count > 1;
            slideshowIntervalMs = ParseSlideshowIntervalMs(widget.ConfigValues);

            if (renderIntent == RenderIntent.Poster)
            {
                await InitializePosterAsync(sources[0], cancellationToken).ConfigureAwait(false);
                return;
            }

            await InitializeAnimatedAsync(sources, cancellationToken).ConfigureAwait(false);
        }

        private async Task InitializePosterAsync(PanelMediaSource source, CancellationToken cancellationToken)
        {
            try
            {
                var posterFrame = await LoadPosterFrameAsync(source, cancellationToken).ConfigureAwait(false);
                lock (stateGate)
                {
                    mediaSequence = posterFrame.Length == 0
                        ? AnimatedMediaSequence.Empty
                        : new AnimatedMediaSequence([new AnimatedMediaFrame(posterFrame, Hub75GifDecoder.DefaultFrameDurationMs)], Hub75GifDecoder.DefaultFrameDurationMs);
                    mediaStartedUtc = DateTimeOffset.UtcNow;
                    errorMessage = mediaSequence.IsEmpty ? "Midia sem poster valido." : null;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                SetError("Falha ao gerar poster do widget GIF.");
            }
        }

        private async Task InitializeAnimatedAsync(IReadOnlyList<PanelMediaSource> sources, CancellationToken cancellationToken)
        {
            if (slideshow)
            {
                if (sources.Count == 0)
                {
                    SetError("Slideshow sem imagens compativeis.");
                    return;
                }

                var orderedSources = sources.ToList();
                if (ParseShuffle(widget.ConfigValues))
                {
                    orderedSources = orderedSources.OrderBy(static _ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
                }

                mediaSources.AddRange(orderedSources);
                try
                {
                    await LoadFramesForSourceAsync(mediaSources[0], cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    SetError("Falha ao abrir a primeira midia do slideshow.");
                    return;
                }

                lock (stateGate)
                {
                    nextSlideUtc = DateTimeOffset.UtcNow.AddMilliseconds(slideshowIntervalMs);
                }

                return;
            }

            mediaSources.Add(sources[0]);
            try
            {
                await LoadFramesForSourceAsync(sources[0], cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                SetError("Falha ao abrir a midia do widget GIF.");
            }
        }

        private async Task LoadFramesForSourceAsync(PanelMediaSource source, CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(source.FileName);
            if (!SupportedImageExtensions.Contains(extension))
            {
                throw new InvalidDataException("Arquivo de midia nao suportado.");
            }

            var scaleMode = ParseGifScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);
            var cacheKey = BuildMediaCacheKey(source.Key, widget.Width, widget.Height, scaleMode, posterOnly: false);
            var loadedFrames = await mediaCache.GetOrCreateAnimatedAsync(
                cacheKey,
                token => DecodeFramesCoreAsync(decoder, source.Payload, extension, widget.Width, widget.Height, scaleMode, token),
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
            }
        }

        private async Task<RgbaColor[]> LoadPosterFrameAsync(PanelMediaSource source, CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(source.FileName);
            var scaleMode = ParseGifScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);
            var cacheKey = BuildMediaCacheKey(source.Key, widget.Width, widget.Height, scaleMode, posterOnly: true);
            var posterFrame = await mediaCache.GetOrCreatePosterAsync(
                cacheKey,
                async token =>
                {
                    var sequence = await DecodeFramesCoreAsync(decoder, source.Payload, extension, widget.Width, widget.Height, scaleMode, token, firstFrameOnly: true).ConfigureAwait(false);
                    return sequence.IsEmpty ? Array.Empty<RgbaColor>() : sequence.Frames[0].Pixels;
                },
                cancellationToken).ConfigureAwait(false);
            if (posterFrame.Length == 0)
            {
                throw new InvalidDataException("Midia sem poster valido.");
            }

            return posterFrame;
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

        private void SetError(string message, bool clearFrames = true)
        {
            lock (stateGate)
            {
                errorMessage = message;
                if (clearFrames)
                {
                    mediaSequence = AnimatedMediaSequence.Empty;
                }
            }
        }
    }

    private static async Task<AnimatedMediaSequence> DecodeFramesCoreAsync(
        Hub75GifDecoder decoder,
        byte[] sourceBytes,
        string extension,
        int targetWidth,
        int targetHeight,
        GifScaleMode scaleMode,
        CancellationToken cancellationToken,
        bool firstFrameOnly = false)
    {
        if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
        {
            if (firstFrameOnly)
            {
                var firstFrame = await Task.Run(() => Hub75GifDecoder.DecodeFirstFrame(sourceBytes, cancellationToken), cancellationToken).ConfigureAwait(false);
                return CreateAnimatedSequence([firstFrame], targetWidth, targetHeight, scaleMode);
            }

            var decodedFrames = await Task.Run(() => decoder.Decode(sourceBytes, cancellationToken), cancellationToken).ConfigureAwait(false);
            return CreateAnimatedSequence(decodedFrames, targetWidth, targetHeight, scaleMode);
        }

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var imageFrame = await Task.Run(() => DecodeStaticImageBytes(sourceBytes), cancellationToken).ConfigureAwait(false);
        return CreateAnimatedSequence([imageFrame], targetWidth, targetHeight, scaleMode);
    }

    private static DecodedGifFrame DecodeStaticImageBytes(byte[] bytes)
    {
        using var image = new MagickImage(bytes);
        if (image.Width == 0 || image.Height == 0)
        {
            throw new InvalidDataException("Imagem sem dimensoes validas.");
        }

        var pixelBytes = image.GetPixels().ToByteArray(PixelMapping.RGBA)
            ?? throw new InvalidDataException("Falha ao extrair pixels RGBA da imagem.");
        var width = (int)image.Width;
        var height = (int)image.Height;
        var pixels = new RgbaColor[width * height];
        var expectedByteCount = pixels.Length * 4;
        if (pixelBytes.Length < expectedByteCount)
        {
            throw new InvalidDataException("Imagem retornou menos bytes RGBA do que o esperado.");
        }

        for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
        {
            var offset = pixelIndex * 4;
            pixels[pixelIndex] = new RgbaColor(
                pixelBytes[offset],
                pixelBytes[offset + 1],
                pixelBytes[offset + 2],
                pixelBytes[offset + 3]);
        }

        return new DecodedGifFrame(width, height, pixels);
    }

    internal static int ResolveAnimatedFrameIndex(AnimatedMediaSequence sequence, TimeSpan elapsed)
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

    internal static RgbaColor[] FormatToTarget(DecodedGifFrame sourceFrame, int targetWidth, int targetHeight, GifScaleMode scaleMode)
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

    private static string BuildMediaCacheKey(string sourcePath, int width, int height, GifScaleMode scaleMode, bool posterOnly)
    {
        return $"{sourcePath.Trim()}|{width}x{height}|{scaleMode}|{(posterOnly ? "poster" : "animated")}";
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
