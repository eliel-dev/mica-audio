using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class AnalyzerRuntimeProfileTests
{
    [Fact]
    public void From_ShouldBuildMode0ProfileForCloneRenderer()
    {
        var settings = VisualizerRuntimeSettings.From(new AppSettings
        {
            LinearBoost = 2.2f,
            BarCount = 96,
            FftSize = 4096,
            FftSmoothing = 0.33f,
            WeightingFilter = WeightingFilter.B,
            FrequencyScale = FrequencyScale.Bark,
            FrequencyMinHz = 20f,
            FrequencyMaxHz = 1000f,
        });
        var preset = new PresetDefinition
        {
            PresetId = "clone",
            RendererId = "audiomotion-clone",
            DisplayBandCount = 96,
            RendererParameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["barSpace"] = 0.2f,
            },
        };

        var profile = AnalyzerRuntimeProfile.From(settings, preset, viewportWidthPx: 0f);
        var config = profile.ToAnalyzerConfig();

        Assert.Equal(DisplayMode.AudioMotionMode0, config.DisplayMode);
        Assert.Equal(96, config.DisplayBandCount);
        Assert.Equal(2f, config.DisplayViewportWidthPx);
        Assert.Equal(0.2f, config.BarSpace);
        Assert.Equal(4096, config.FftSize);
        Assert.Equal(0.33f, config.FftSmoothing);
        Assert.Equal(20f, config.MinHz);
        Assert.Equal(1000f, config.MaxHz);
        Assert.Equal(WeightingFilter.B, config.WeightingFilter);
        Assert.Equal(FrequencyScale.Bark, config.FrequencyScale);
    }

    [Fact]
    public void From_ShouldBuildFixedBandProfileForNonCloneRenderer()
    {
        var settings = VisualizerRuntimeSettings.Default;
        var preset = new PresetDefinition
        {
            PresetId = "custom",
            RendererId = "polar-arcs",
            DisplayBandCount = 48,
        };

        var profile = AnalyzerRuntimeProfile.From(settings, preset, viewportWidthPx: 480f, rendererId: "polar-arcs");
        var config = profile.ToAnalyzerConfig();

        Assert.Equal(DisplayMode.FixedBands, config.DisplayMode);
        Assert.Equal(0f, config.DisplayViewportWidthPx);
        Assert.Equal(48, config.DisplayBandCount);
        Assert.Equal(64, config.OutputBandCount);
    }
}
