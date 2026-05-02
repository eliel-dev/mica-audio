using App.WinUI.Models.Panels;
using App.WinUI.Services.Panels;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using System.Reflection;
using SharedPanels = MicaAudio.Panels;

namespace Integration.Smoke;

public sealed class PanelsFrameComposerTests
{
    private const string RedPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY/jPwPAfAAUAAf+mXJtdAAAAAElFTkSuQmCC";
    private const string BluePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY2Bg+P8fAAMCAf/Jsq3uAAAAAElFTkSuQmCC";
    private const string AnimatedDisposalGifBase64 = "R0lGODlhBAAEAPAAAAAAAP8AACH/C05FVFNDQVBFMi4wAwEAAAAh+QQJCgAAACwAAAAABAAEAAACBUwAhstQACH5BAkKAAAALAAAAAAEAAQAAAIFBBKGxlwAOw==";

    [Fact]
    public async Task CreateSessionAsync_ShouldRenderClockWidgetIntoFullFrame()
    {
        var composer = new PanelsFrameComposer();
        var panel = new PanelDefinition
        {
            Name = "Clock",
            Widgets =
            [
                new PanelWidgetDefinition
                {
                    WidgetId = "clock-1",
                    AppId = "analogclock",
                    X = 8,
                    Y = 8,
                    Width = 48,
                    Height = 24,
                },
            ],
        };

        using var session = await composer.CreateSessionAsync(panel);
        var frame = session.RenderFrame(new DateTimeOffset(2026, 3, 9, 18, 15, 0, TimeSpan.Zero));

        Assert.Equal(128 * 64, frame.Length);
        Assert.Contains(frame, static pixel => (pixel.R | pixel.G | pixel.B) != 0);
        Assert.Empty(session.GetWidgetErrors());
    }

    [Fact]
    public async Task PanelCompositionSession_RenderFrameInto_ShouldMatchRenderFrame()
    {
        var composer = new PanelsFrameComposer();
        var panel = new PanelDefinition
        {
            Name = "Clock",
            Widgets =
            [
                new PanelWidgetDefinition
                {
                    WidgetId = "clock-1",
                    AppId = "analogclock",
                    X = 8,
                    Y = 8,
                    Width = 48,
                    Height = 24,
                },
            ],
        };

        using var session = await composer.CreateSessionAsync(panel);
        var utcNow = new DateTimeOffset(2026, 3, 9, 18, 15, 0, TimeSpan.Zero);
        var expected = session.RenderFrame(utcNow);
        var target = Enumerable
            .Repeat(new RgbaColor(1, 2, 3, 4), expected.Length)
            .ToArray();

        var renderInto = typeof(PanelsFrameComposer.PanelCompositionSession)
            .GetMethod("RenderFrameInto", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(renderInto);
        renderInto!.Invoke(session, [utcNow, target]);
        Assert.Equal(expected, target);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldKeepGifWidgetBlackWhenSourceIsMissing()
    {
        var composer = new PanelsFrameComposer();
        var panel = new PanelDefinition
        {
            Widgets =
            [
                new PanelWidgetDefinition
                {
                    WidgetId = "gif-missing",
                    AppId = "gifhub75",
                    Width = 24,
                    Height = 24,
                    RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sourcePath"] = Path.Combine(Path.GetTempPath(), "does-not-exist.gif"),
                    },
                },
            ],
        };

        using var session = await composer.CreateSessionAsync(panel);
        var frame = session.RenderFrame(DateTimeOffset.UtcNow);
        var errors = session.GetWidgetErrors();

        Assert.True(errors.ContainsKey("gif-missing"));
        Assert.All(frame, static pixel => Assert.Equal(new RgbaColor(0, 0, 0, 255), pixel));
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldClampWidgetsAndHonorZOrderForGifWidgets()
    {
        var root = CreateTempDirectory();
        try
        {
            var redPath = Path.Combine(root, "red.png");
            var bluePath = Path.Combine(root, "blue.png");
            await File.WriteAllBytesAsync(redPath, Convert.FromBase64String(RedPngBase64));
            await File.WriteAllBytesAsync(bluePath, Convert.FromBase64String(BluePngBase64));

            var composer = new PanelsFrameComposer();
            var panel = new PanelDefinition
            {
                Widgets =
                [
                    new PanelWidgetDefinition
                    {
                        WidgetId = "back",
                        AppId = "gifhub75",
                        X = 120,
                        Y = 60,
                        Width = 16,
                        Height = 8,
                        ZIndex = 1,
                        RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["sourcePath"] = redPath,
                        },
                    },
                    new PanelWidgetDefinition
                    {
                        WidgetId = "front",
                        AppId = "gifhub75",
                        X = 120,
                        Y = 60,
                        Width = 16,
                        Height = 8,
                        ZIndex = 2,
                        RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["sourcePath"] = bluePath,
                        },
                    },
                ],
            };

            using var session = await composer.CreateSessionAsync(panel);
            var snapshot = session.Panel;
            var frame = session.RenderFrame(DateTimeOffset.UtcNow);

            var clampedFront = Assert.Single(snapshot.Widgets, static widget => widget.WidgetId == "front");
            Assert.Equal(112, clampedFront.X);
            Assert.Equal(56, clampedFront.Y);

            var pixelIndex = ((clampedFront.Y + 4) * 128) + (clampedFront.X + 8);
            var pixel = frame[pixelIndex];
            Assert.True(pixel.B > 0);
            Assert.True(pixel.B >= pixel.R);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreatePosterAsync_ShouldRenderStaticPosterForSlideshowFolder()
    {
        var root = CreateTempDirectory();
        try
        {
            var redPath = Path.Combine(root, "red.png");
            var bluePath = Path.Combine(root, "blue.png");
            await File.WriteAllBytesAsync(redPath, Convert.FromBase64String(RedPngBase64));
            await File.WriteAllBytesAsync(bluePath, Convert.FromBase64String(BluePngBase64));

            var composer = new PanelsFrameComposer();
            var panel = new PanelDefinition
            {
                Widgets =
                [
                    new PanelWidgetDefinition
                    {
                        WidgetId = "gif-poster",
                        AppId = "gifhub75",
                        X = 0,
                        Y = 0,
                        Width = 32,
                        Height = 16,
                        ConfigValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["sourceType"] = "slideshow",
                        },
                        RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["sourcePath"] = root,
                        },
                    },
                ],
            };

            var poster = await composer.CreatePosterAsync(panel);

            Assert.Equal(128 * 64, poster.Frame.Length);
            Assert.Empty(poster.WidgetErrors);
            Assert.Contains(poster.Frame, static pixel => (pixel.R | pixel.G | pixel.B) != 0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldNotLeaveGhostTrailForAnimatedGifWithBackgroundDisposal()
    {
        var root = CreateTempDirectory();
        try
        {
            var gifPath = Path.Combine(root, "trail.gif");
            await File.WriteAllBytesAsync(gifPath, Convert.FromBase64String(AnimatedDisposalGifBase64));

            var composer = new PanelsFrameComposer();
            var panel = new PanelDefinition
            {
                Widgets =
                [
                    new PanelWidgetDefinition
                    {
                        WidgetId = "gif-trail",
                        AppId = "gifhub75",
                        X = 0,
                        Y = 0,
                        Width = 4,
                        Height = 4,
                        RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["sourcePath"] = gifPath,
                        },
                    },
                ],
            };

            using var session = await composer.CreateSessionAsync(panel);

            var firstFrame = session.RenderFrame(DateTimeOffset.UtcNow);
            var secondFrame = session.RenderFrame(DateTimeOffset.UtcNow.AddMilliseconds(120));

            Assert.Equal(new RgbaColor(255, 0, 0, 255), firstFrame[0]);
            Assert.Equal(new RgbaColor(0, 0, 0, 255), secondFrame[0]);
            Assert.Equal(new RgbaColor(255, 0, 0, 255), secondFrame[2]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 1)]
    [InlineData(299, 1)]
    [InlineData(300, 2)]
    [InlineData(599, 2)]
    [InlineData(600, 0)]
    [InlineData(650, 0)]
    public void ResolveAnimatedFrameIndex_ShouldRespectFrameDurationsAndWrap(long elapsedMs, int expectedIndex)
    {
        var sequence = new SharedPanels.AnimatedMediaSequence(
            [
                new SharedPanels.AnimatedMediaFrame([new RgbaColor(255, 0, 0, 255)], 100),
                new SharedPanels.AnimatedMediaFrame([new RgbaColor(0, 255, 0, 255)], 200),
                new SharedPanels.AnimatedMediaFrame([new RgbaColor(0, 0, 255, 255)], 300),
            ],
            totalDurationMs: 600);

        var index = SharedPanels.PanelFrameComposer.ResolveAnimatedFrameIndex(sequence, TimeSpan.FromMilliseconds(elapsedMs));

        Assert.Equal(expectedIndex, index);
    }

    [Fact]
    public void FormatToTarget_ShouldReusePixelsWhenFrameAlreadyMatchesWidgetSize()
    {
        var pixels = Enumerable
            .Repeat(new RgbaColor(12, 34, 56, 255), 8 * 4)
            .ToArray();
        var frame = new SharedPanels.DecodedGifFrame(8, 4, pixels);

        var output = SharedPanels.PanelFrameComposer.FormatToTarget(frame, 8, 4, SharedPanels.GifScaleMode.Fit);

        Assert.Same(pixels, output);
    }

    [Fact]
    public void TargetFps_ShouldBeThirtyForGifHub75Playback()
    {
        Assert.Equal(30, PanelsFrameComposer.TargetFps);
    }

    [Fact]
    public void AnimatedWebpEncoder_ShouldEmitWebpContainerAndPreserveBatchMetadata()
    {
        var frameA = Enumerable
            .Repeat(new RgbaColor(255, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
            .ToArray();
        var frameB = Enumerable
            .Repeat(new RgbaColor(0, 0, 255, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
            .ToArray();

        var encoded = SharedPanels.PanelsAnimatedWebpEncoder.Encode(
            [frameA, frameB],
            [33, 67],
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight);

        Assert.Equal(2, encoded.FrameCount);
        Assert.Equal(100, encoded.DurationMs);
        Assert.True(encoded.Payload.Length > 12);
        Assert.Equal("RIFF"u8.ToArray(), encoded.Payload[..4]);
        Assert.Equal("WEBP"u8.ToArray(), encoded.Payload[8..12]);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
