using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using App.WinUI.Models.Panels;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using PanelsCompositionServerSide = global::Panels.Composition.ServerSide;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-hub75
// DOCS: docs/handoffs/2026-04-18-panels-webp-batch-pipeline-optimizations.md
[SupportedOSPlatform("windows")]
internal sealed class PanelsFrameComposer
{
    public const int TargetFps = 30;
    private static readonly HashSet<string> SupportedWidgetAppIds =
        new(StringComparer.OrdinalIgnoreCase) { "analogclock", "gifhub75" };
    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp" };
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    private readonly Hub75GifDecoder decoder;
    private readonly PanelsMediaCache mediaCache;

    public PanelsFrameComposer()
        : this(new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames), new PanelsMediaCache())
    {
    }

    internal PanelsFrameComposer(Hub75GifDecoder decoder, PanelsMediaCache mediaCache)
    {
        this.decoder = decoder;
        this.mediaCache = mediaCache;
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
            "gifhub75" => await GifWidgetRuntime.CreateAsync(widget.Clone(), decoder, mediaCache, renderIntent, cancellationToken).ConfigureAwait(false),
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
        private readonly string mostrador;

        public ClockWidgetRuntime(PanelWidgetDefinition widget)
        {
            this.widget = widget;
            widget.Normalize(LedDefaults.MatrixWidth, LedDefaults.MatrixHeight);
            use24Hour = !widget.ConfigValues.TryGetValue("format24h", out var raw24h)
                || !bool.TryParse(raw24h, out var parsed24h)
                || parsed24h;
            mostrador = widget.ConfigValues.TryGetValue("mostrador", out var rawMostrador)
                && !string.IsNullOrWhiteSpace(rawMostrador)
                ? rawMostrador
                : "cyberterminal";
        }

        public string WidgetId => widget.WidgetId;

        public string? ErrorMessage => null;

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
            var now = TimeZoneInfo.ConvertTime(utcNow, BrasiliaTimeZone).DateTime;
            PanelsCompositionServerSide.WatchfaceLibrary.Render(
                mostrador,
                targetFrame,
                panelWidth,
                panelHeight,
                widget.X,
                widget.Y,
                widget.Width,
                widget.Height,
                now,
                use24Hour);
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
        private readonly RenderIntent renderIntent;
        private readonly CancellationTokenSource lifetimeCts = new();
        private readonly List<string> mediaSources = [];
        private readonly object stateGate = new();
        private AnimatedMediaSequence mediaSequence = AnimatedMediaSequence.Empty;
        private DateTimeOffset mediaStartedUtc = DateTimeOffset.UtcNow;
        private DateTimeOffset nextSlideUtc = DateTimeOffset.MaxValue;
        private int sourceIndex;
        private bool slideshow;
        private int slideshowIntervalMs = 10_000;
        private string? errorMessage;
        private bool switchInProgress;

        private GifWidgetRuntime(PanelWidgetDefinition widget, Hub75GifDecoder decoder, PanelsMediaCache mediaCache, RenderIntent renderIntent)
        {
            this.widget = widget;
            this.decoder = decoder;
            this.mediaCache = mediaCache;
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
            RenderIntent renderIntent,
            CancellationToken cancellationToken)
        {
            var runtime = new GifWidgetRuntime(widget, decoder, mediaCache, renderIntent);
            await runtime.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return runtime;
        }

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
            AnimatedMediaSequence localSequence;
            DateTimeOffset localMediaStartedUtc;
            string? nextSlideSource = null;
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
            var sourcePath = widget.RuntimeState.TryGetValue("sourcePath", out var storedPath)
                ? storedPath?.Trim()
                : string.Empty;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                SetError("Widget GIF sem caminho salvo.");
                return;
            }

            slideshow = string.Equals(widget.ConfigValues.TryGetValue("sourceType", out var sourceType) ? sourceType : null, "slideshow", StringComparison.OrdinalIgnoreCase)
                || Directory.Exists(sourcePath);
            slideshowIntervalMs = ParseSlideshowIntervalMs(widget.ConfigValues);

            if (renderIntent == RenderIntent.Poster)
            {
                await InitializePosterAsync(sourcePath, cancellationToken).ConfigureAwait(false);
                return;
            }

            await InitializeAnimatedAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        }

        private async Task InitializePosterAsync(string sourcePath, CancellationToken cancellationToken)
        {
            try
            {
                var posterSource = ResolvePosterSourcePath(sourcePath);
                if (string.IsNullOrWhiteSpace(posterSource))
                {
                    SetError(slideshow ? "Pasta sem imagens compativeis." : "Arquivo GIF/imagem nao encontrado.");
                    return;
                }

                var posterFrame = await LoadPosterFrameAsync(posterSource, cancellationToken).ConfigureAwait(false);
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

        private async Task InitializeAnimatedAsync(string sourcePath, CancellationToken cancellationToken)
        {
            if (slideshow)
            {
                if (!Directory.Exists(sourcePath))
                {
                    SetError("Pasta de slideshow nao encontrada.");
                    return;
                }

                var sources = EnumerateMediaSources(sourcePath);
                if (sources.Count == 0)
                {
                    SetError("Pasta sem imagens compativeis.");
                    return;
                }

                if (ParseShuffle(widget.ConfigValues))
                {
                    sources = sources.OrderBy(static _ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
                }

                mediaSources.AddRange(sources);
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

            if (!File.Exists(sourcePath))
            {
                SetError("Arquivo GIF/imagem nao encontrado.");
                return;
            }

            mediaSources.Add(sourcePath);
            try
            {
                await LoadFramesForSourceAsync(sourcePath, cancellationToken).ConfigureAwait(false);
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

        private async Task LoadFramesForSourceAsync(string sourcePath, CancellationToken cancellationToken)
        {
            if (!SupportedImageExtensions.Contains(Path.GetExtension(sourcePath)))
            {
                throw new InvalidDataException("Arquivo de midia nao suportado.");
            }

            var scaleMode = ParseGifScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);
            var cacheKey = BuildMediaCacheKey(sourcePath, widget.Width, widget.Height, scaleMode, posterOnly: false);
            var loadedFrames = await mediaCache.GetOrCreateAnimatedAsync(
                cacheKey,
                token => DecodeFramesCoreAsync(decoder, sourcePath, widget.Width, widget.Height, scaleMode, token),
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

        private async Task<RgbaColor[]> LoadPosterFrameAsync(string sourcePath, CancellationToken cancellationToken)
        {
            var scaleMode = ParseGifScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);
            var cacheKey = BuildMediaCacheKey(sourcePath, widget.Width, widget.Height, scaleMode, posterOnly: true);
            var posterFrame = await mediaCache.GetOrCreatePosterAsync(
                cacheKey,
                async token =>
                {
                    var sequence = await DecodeFramesCoreAsync(decoder, sourcePath, widget.Width, widget.Height, scaleMode, token, firstFrameOnly: true).ConfigureAwait(false);
                    return sequence.IsEmpty ? Array.Empty<RgbaColor>() : sequence.Frames[0].Pixels;
                },
                cancellationToken).ConfigureAwait(false);
            if (posterFrame.Length == 0)
            {
                throw new InvalidDataException("Midia sem poster valido.");
            }

            return posterFrame;
        }

        private async Task BeginSlideSwitchAsync(int nextIndex, string sourcePath)
        {
            try
            {
                await LoadFramesForSourceAsync(sourcePath, lifetimeCts.Token).ConfigureAwait(false);
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

        private static string? ResolvePosterSourcePath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return null;
            }

            if (Directory.Exists(sourcePath))
            {
                return EnumerateMediaSources(sourcePath).FirstOrDefault();
            }

            return File.Exists(sourcePath) ? sourcePath : null;
        }

        private static List<string> EnumerateMediaSources(string sourcePath)
        {
            return Directory
                .EnumerateFiles(sourcePath)
                .Where(static path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
        string sourcePath,
        int targetWidth,
        int targetHeight,
        GifScaleMode scaleMode,
        CancellationToken cancellationToken,
        bool firstFrameOnly = false)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            if (firstFrameOnly)
            {
                var firstFrame = await Task.Run(() => Hub75GifDecoder.DecodeFirstFrame(bytes, cancellationToken), cancellationToken).ConfigureAwait(false);
                return CreateAnimatedSequence([firstFrame], targetWidth, targetHeight, scaleMode);
            }

            var decodedFrames = await Task.Run(() => decoder.Decode(bytes, cancellationToken), cancellationToken).ConfigureAwait(false);
            return CreateAnimatedSequence(decodedFrames, targetWidth, targetHeight, scaleMode);
        }

        var rawBytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var imageFrame = await Task.Run(() => DecodeStaticImageBytes(rawBytes), cancellationToken).ConfigureAwait(false);
        return CreateAnimatedSequence([imageFrame], targetWidth, targetHeight, scaleMode);
    }

    private static DecodedGifFrame DecodeStaticImageBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var img = System.Drawing.Image.FromStream(ms);
        using var bmp = new System.Drawing.Bitmap(img);
        var lockData = bmp.LockBits(
            new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(lockData.Stride);
            var raw = new byte[stride * bmp.Height];
            Marshal.Copy(lockData.Scan0, raw, 0, raw.Length);
            var pixels = new RgbaColor[bmp.Width * bmp.Height];
            for (var y = 0; y < bmp.Height; y++)
            {
                var rowOffset = lockData.Stride >= 0
                    ? y * stride
                    : (bmp.Height - 1 - y) * stride;

                for (var x = 0; x < bmp.Width; x++)
                {
                    var offset = rowOffset + (x * 4);
                    pixels[(y * bmp.Width) + x] = new RgbaColor(raw[offset + 2], raw[offset + 1], raw[offset], raw[offset + 3]);
                }
            }

            return new DecodedGifFrame(bmp.Width, bmp.Height, pixels);
        }
        finally
        {
            bmp.UnlockBits(lockData);
        }
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
