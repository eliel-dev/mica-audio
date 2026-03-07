using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class VisualizerRuntimeSettingsTests
{
    [Fact]
    public void From_ShouldNormalizeInvalidSettingsIntoSupportedRuntimeValues()
    {
        var settings = new AppSettings
        {
            ActivePresetId = "",
            SelectedRendererId = "",
            LinearBoost = float.NaN,
            BarCount = 0,
            FftSize = 123,
            FftSmoothing = 9f,
            WeightingFilter = (WeightingFilter)999,
            FrequencyScale = (FrequencyScale)999,
            FrequencyMinHz = 8f,
            FrequencyMaxHz = 30f,
        };

        var runtime = VisualizerRuntimeSettings.From(settings);

        Assert.Equal("audiomotion-clone", runtime.ActivePresetId);
        Assert.Equal("audiomotion-clone", runtime.SelectedRendererId);
        Assert.Equal(1.6f, runtime.LinearBoost);
        Assert.Equal(38, runtime.DisplayBandCount);
        Assert.Equal(1024, runtime.FftSize);
        Assert.Equal(0.99f, runtime.FftSmoothing);
        Assert.Equal(WeightingFilter.Off, runtime.WeightingFilter);
        Assert.Equal(FrequencyScale.Bark, runtime.FrequencyScale);
        Assert.Equal(16f, runtime.FrequencyMinHz);
        Assert.Equal(250f, runtime.FrequencyMaxHz);
    }

    [Fact]
    public void From_ShouldPreserveValidSettings()
    {
        var settings = new AppSettings
        {
            ActivePresetId = "custom-preset",
            SelectedRendererId = "polar-arcs",
            LinearBoost = 2.4f,
            BarCount = 64,
            FftSize = 4096,
            FftSmoothing = 0.45f,
            WeightingFilter = WeightingFilter.C,
            FrequencyScale = FrequencyScale.Logarithmic,
            FrequencyMinHz = 40f,
            FrequencyMaxHz = 4000f,
        };

        var runtime = VisualizerRuntimeSettings.From(settings);

        Assert.Equal("custom-preset", runtime.ActivePresetId);
        Assert.Equal("polar-arcs", runtime.SelectedRendererId);
        Assert.Equal(2.4f, runtime.LinearBoost);
        Assert.Equal(64, runtime.DisplayBandCount);
        Assert.Equal(4096, runtime.FftSize);
        Assert.Equal(0.45f, runtime.FftSmoothing);
        Assert.Equal(WeightingFilter.C, runtime.WeightingFilter);
        Assert.Equal(FrequencyScale.Logarithmic, runtime.FrequencyScale);
        Assert.Equal(40f, runtime.FrequencyMinHz);
        Assert.Equal(4000f, runtime.FrequencyMaxHz);
    }
}
