using App.WinUI.Services.Visualizer;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class PresetPreviewSignalFactoryTests
{
    [Fact]
    public void CreatePcmFrame_ShouldReturnRequestedSampleCount()
    {
        var preset = CreatePreset();
        var settings = PresetPreviewSettingsSnapshot.Default;

        var frame = PresetPreviewSignalFactory.CreatePcmFrame(preset, settings, 0f, 256, PresetPreviewSignalFactory.SampleRate);

        Assert.Equal(256, frame.SamplesMono.Length);
    }

    [Fact]
    public void CreatePcmFrame_ShouldBeDeterministic_ForSamePresetAndTime()
    {
        var preset = CreatePreset();
        var settings = PresetPreviewSettingsSnapshot.Default;

        var first = PresetPreviewSignalFactory.CreatePcmFrame(preset, settings, 0.75f, 256, PresetPreviewSignalFactory.SampleRate);
        var second = PresetPreviewSignalFactory.CreatePcmFrame(preset, settings, 0.75f, 256, PresetPreviewSignalFactory.SampleRate);

        Assert.Equal(first.SamplesMono, second.SamplesMono);
        Assert.Equal(first.TimestampQpc, second.TimestampQpc);
    }

    [Fact]
    public void CreatePcmFrame_ShouldVaryOverTime()
    {
        var preset = CreatePreset();
        var settings = PresetPreviewSettingsSnapshot.Default;

        var first = PresetPreviewSignalFactory.CreatePcmFrame(preset, settings, 0f, 256, PresetPreviewSignalFactory.SampleRate);
        var second = PresetPreviewSignalFactory.CreatePcmFrame(preset, settings, 1f, 256, PresetPreviewSignalFactory.SampleRate);

        Assert.NotEqual(first.SamplesMono, second.SamplesMono);
    }

    [Fact]
    public void CreatePcmFrame_ShouldClampSamplesBetweenMinusOneAndOne()
    {
        var preset = CreatePreset();
        var settings = PresetPreviewSettingsSnapshot.Default;

        var frame = PresetPreviewSignalFactory.CreatePcmFrame(preset, settings, 2.3f, 256, PresetPreviewSignalFactory.SampleRate);

        Assert.All(frame.SamplesMono, static value => Assert.InRange(value, -1f, 1f));
    }

    private static PresetDefinition CreatePreset()
    {
        return new PresetDefinition
        {
            PresetId = "preview-signal-test",
            Name = "Preview Signal Test",
            RendererId = "bars",
            DisplayBandCount = 38,
        };
    }
}
