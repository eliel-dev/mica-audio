using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using App.WinUI.Models.Panels;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-hub75
[SupportedOSPlatform("windows")]
internal sealed class PanelsFrameComposer
{
    public const int TargetFps = 12;
    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp" };
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1000d / TargetFps);
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    private readonly Hub75GifDecoder decoder;

    public PanelsFrameComposer()
        : this(new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames))
    {
    }

    internal PanelsFrameComposer(Hub75GifDecoder decoder)
    {
        this.decoder = decoder;
    }

    internal async Task<PanelCompositionSession> CreateSessionAsync(PanelDefinition panel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var normalizedPanel = panel.Clone();
        normalizedPanel.Normalize();

        var runtimes = new List<IPanelWidgetRuntime>(normalizedPanel.Widgets.Count);
        foreach (var widget in normalizedPanel.Widgets.OrderBy(static widget => widget.ZIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtimes.Add(await CreateRuntimeAsync(widget, cancellationToken).ConfigureAwait(false));
        }

        return new PanelCompositionSession(normalizedPanel, runtimes);
    }

    private async Task<IPanelWidgetRuntime> CreateRuntimeAsync(PanelWidgetDefinition widget, CancellationToken cancellationToken)
    {
        return widget.AppId.Trim().ToLowerInvariant() switch
        {
            "analogclock" => new ClockWidgetRuntime(widget.Clone()),
            "gifhub75" => await GifWidgetRuntime.CreateAsync(widget.Clone(), decoder, cancellationToken).ConfigureAwait(false),
            _ => new EmptyWidgetRuntime(widget.Clone()),
        };
    }

    internal sealed class PanelCompositionSession : IDisposable
    {
        private readonly PanelDefinition panel;
        private readonly List<IPanelWidgetRuntime> widgetRuntimes;
        private readonly RgbaColor[] scratch = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];

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
            PanelsMatrixDrawHelpers.Clear(scratch);
            foreach (var runtime in widgetRuntimes)
            {
                runtime.Render(utcNow, scratch, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight);
            }

            return scratch.ToArray();
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

    internal interface IPanelWidgetRuntime : IDisposable
    {
        string WidgetId { get; }

        string? ErrorMessage { get; }

        void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight);
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
        private readonly List<string> mediaSources = [];
        private readonly object stateGate = new();
        private RgbaColor[][] frames = [];
        private DateTimeOffset mediaStartedUtc = DateTimeOffset.UtcNow;
        private DateTimeOffset nextSlideUtc = DateTimeOffset.MaxValue;
        private int sourceIndex;
        private bool slideshow;
        private int slideshowIntervalMs = 10_000;
        private string? errorMessage;

        private GifWidgetRuntime(PanelWidgetDefinition widget, Hub75GifDecoder decoder)
        {
            this.widget = widget;
            this.decoder = decoder;
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

        public static async Task<GifWidgetRuntime> CreateAsync(PanelWidgetDefinition widget, Hub75GifDecoder decoder, CancellationToken cancellationToken)
        {
            var runtime = new GifWidgetRuntime(widget, decoder);
            await runtime.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return runtime;
        }

        public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
        {
            string? pendingSlideSource = null;
            RgbaColor[][] localFrames;
            DateTimeOffset localMediaStartedUtc;
            lock (stateGate)
            {
                if (slideshow && mediaSources.Count > 1 && utcNow >= nextSlideUtc)
                {
                    sourceIndex = (sourceIndex + 1) % mediaSources.Count;
                    pendingSlideSource = mediaSources[sourceIndex];
                    nextSlideUtc = utcNow.AddMilliseconds(slideshowIntervalMs);
                }

                localFrames = frames;
                localMediaStartedUtc = mediaStartedUtc;
                if (localFrames.Length == 0)
                {
                    return;
                }
            }

            if (pendingSlideSource is not null)
            {
                try
                {
                    LoadFramesForSourceAsync(pendingSlideSource, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch
                {
                    SetError("Falha ao alternar midia do slideshow.");
                }

                lock (stateGate)
                {
                    localFrames = frames;
                    localMediaStartedUtc = mediaStartedUtc;
                    if (localFrames.Length == 0)
                    {
                        return;
                    }
                }
            }

            var index = localFrames.Length == 1
                ? 0
                : (int)Math.Floor(((utcNow - localMediaStartedUtc).TotalMilliseconds / FrameInterval.TotalMilliseconds) % localFrames.Length);
            if (index < 0)
            {
                index = 0;
            }

            PanelsMatrixDrawHelpers.Blit(
                localFrames[index],
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

            if (slideshow)
            {
                if (!Directory.Exists(sourcePath))
                {
                    SetError("Pasta de slideshow nao encontrada.");
                    return;
                }

                var sources = Directory
                    .EnumerateFiles(sourcePath)
                    .Where(static path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
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
            catch
            {
                SetError("Falha ao abrir a midia do widget GIF.");
            }
        }

        private async Task LoadFramesForSourceAsync(string sourcePath, CancellationToken cancellationToken)
        {
            if (!SupportedImageExtensions.Contains(Path.GetExtension(sourcePath)))
            {
                SetError("Arquivo de midia nao suportado.");
                return;
            }

            var scaleMode = ParseGifScaleMode(widget.ConfigValues.TryGetValue("scaleMode", out var rawScale) ? rawScale : null);
            var extension = Path.GetExtension(sourcePath);
            RgbaColor[][] loadedFrames;

            if (string.Equals(extension, ".gif", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
                var decodedFrames = await Task.Run(() => decoder.Decode(bytes, cancellationToken), cancellationToken).ConfigureAwait(false);
                loadedFrames = decodedFrames.Select(frame => FormatToTarget(frame, widget.Width, widget.Height, scaleMode)).ToArray();
            }
            else
            {
                var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
                var frame = await Task.Run(() => DecodeStaticImageBytes(bytes), cancellationToken).ConfigureAwait(false);
                loadedFrames = [FormatToTarget(frame, widget.Width, widget.Height, scaleMode)];
            }

            lock (stateGate)
            {
                frames = loadedFrames.Length == 0 ? Array.Empty<RgbaColor[]>() : loadedFrames;
                mediaStartedUtc = DateTimeOffset.UtcNow;
                errorMessage = frames.Length == 0 ? "Midia sem frames validos." : null;
            }
        }

        private void SetError(string message)
        {
            lock (stateGate)
            {
                errorMessage = message;
                frames = Array.Empty<RgbaColor[]>();
            }
        }
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

    private static RgbaColor[] FormatToTarget(DecodedGifFrame sourceFrame, int targetWidth, int targetHeight, GifScaleMode scaleMode)
    {
        var safeWidth = Math.Max(1, targetWidth);
        var safeHeight = Math.Max(1, targetHeight);
        var target = new RgbaColor[safeWidth * safeHeight];
        PanelsMatrixDrawHelpers.Clear(target);

        if (sourceFrame.Width <= 0 || sourceFrame.Height <= 0)
        {
            return target;
        }

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
