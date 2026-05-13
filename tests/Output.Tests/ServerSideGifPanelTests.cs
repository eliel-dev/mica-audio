using ImageMagick;
using MicaAudio.Core.Presets;
using Panels.Composition.Models;
using Panels.Composition.ServerSide;

namespace Output.Tests;

public sealed class ServerSideGifPanelTests
{
    [Fact]
    public void CapabilityClassifier_ShouldTreatGifWithUploadedMediaAndClockAsServerCapable()
    {
        var panel = CreateGifPanel(includeClock: true);

        var capability = PanelServerCapabilityClassifier.Classify(panel);

        Assert.Equal(PanelServerCapability.ServerCapable, capability);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerSideCompositor_ShouldRenderGifPanelFromPersistedMedia(bool includeClock)
    {
        var mediaDirectory = CreateTempMediaDirectory();
        try
        {
            WriteSolidGif(Path.Combine(mediaDirectory, "gif-000.gif"), MagickColors.Red);
            var panel = CreateGifPanel(includeClock);

            using var compositor = ServerSidePanelCompositor.TryCreate(panel, mediaDirectory);

            Assert.NotNull(compositor);
            var frame = compositor!.RenderFrame(DateTimeOffset.UtcNow);
            Assert.Contains(frame, static pixel => pixel.R > 200 && pixel.G < 50 && pixel.B < 50);
        }
        finally
        {
            if (Directory.Exists(mediaDirectory))
            {
                Directory.Delete(mediaDirectory, recursive: true);
            }
        }
    }

    private static PanelDefinition CreateGifPanel(bool includeClock)
    {
        var panel = new PanelDefinition
        {
            PanelId = includeClock ? "gif-clock-panel" : "gif-panel",
            Name = includeClock ? "GIF + Clock" : "GIF",
            Widgets =
            [
                new PanelWidgetDefinition
                {
                    WidgetId = "gif",
                    AppId = "gifhub75",
                    Width = 128,
                    Height = 64,
                    ZIndex = 0,
                    ConfigValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["scaleMode"] = "Stretch",
                    },
                    RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mediaId"] = "gif-000.gif",
                    },
                },
            ],
        };

        if (includeClock)
        {
            panel.Widgets.Add(new PanelWidgetDefinition
            {
                WidgetId = "clock",
                AppId = "analogclock",
                X = 88,
                Y = 0,
                Width = 40,
                Height = 20,
                ZIndex = 1,
            });
        }

        return panel;
    }

    private static string CreateTempMediaDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mica-audio-server-gif-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteSolidGif(string path, IMagickColor<byte> color)
    {
        using var image = new MagickImage(color, 4, 4)
        {
            Format = MagickFormat.Gif,
        };
        image.Write(path);
    }
}
