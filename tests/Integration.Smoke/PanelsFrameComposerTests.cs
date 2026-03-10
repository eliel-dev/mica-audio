using App.WinUI.Models.Panels;
using App.WinUI.Services.Panels;
using MicaAudio.Core.Presets;

namespace Integration.Smoke;

public sealed class PanelsFrameComposerTests
{
    private const string RedPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY/jPwPAfAAUAAf+mXJtdAAAAAElFTkSuQmCC";
    private const string BluePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY2Bg+P8fAAMCAf/Jsq3uAAAAAElFTkSuQmCC";

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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
