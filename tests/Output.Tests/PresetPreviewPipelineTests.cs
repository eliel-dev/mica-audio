using Analyzer.Dsp.Analysis;
using App.WinUI.Services.Visualizer;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace Output.Tests;

public sealed class PresetPreviewPipelineTests
{
    [Fact]
    public void Build_ShouldMirrorCurrentVisualizerSettings_ForAudioMotionClone()
    {
        var preset = CreatePreset(RendererIds.AudioMotionClone, 38);
        var snapshot = new PresetPreviewSettingsSnapshot(
            38,
            2048,
            0.75f,
            1.6f,
            FrequencyScale.Bark,
            WeightingFilter.B,
            20f,
            1000f,
            1280f);

        var config = VisualizerAnalyzerConfigFactory.Build(preset, snapshot, 160f);

        Assert.Equal(PresetPreviewSignalFactory.SampleRate, config.SampleRate);
        Assert.Equal(PresetPreviewSignalFactory.HopSize, config.HopSize);
        Assert.Equal(DisplayMode.AudioMotionMode0, config.DisplayMode);
        Assert.Equal(38, config.DisplayBandCount);
        Assert.Equal(2048, config.FftSize);
        Assert.Equal(1.6f, config.LinearBoost);
        Assert.Equal(0.75f, config.FftSmoothing);
        Assert.Equal(WeightingFilter.B, config.WeightingFilter);
        Assert.Equal(FrequencyScale.Bark, config.FrequencyScale);
        Assert.Equal(20f, config.MinHz);
        Assert.Equal(1000f, config.MaxHz);
        Assert.Equal(-85f, config.MinDecibels);
        Assert.Equal(-25f, config.MaxDecibels);
        Assert.Equal(1280f, config.DisplayViewportWidthPx);
    }

    [Fact]
    public void PreviewPipeline_ShouldProduceAnalyzerFramesWithCloneGeometry()
    {
        var preset = CreatePreset(RendererIds.AudioMotionClone, 38);
        var snapshot = new PresetPreviewSettingsSnapshot(
            38,
            2048,
            0.75f,
            1.6f,
            FrequencyScale.Bark,
            WeightingFilter.B,
            20f,
            1000f,
            1280f);
        var config = VisualizerAnalyzerConfigFactory.Build(preset, snapshot, 160f);
        var analyzer = new SpectrumAnalyzer(config);

        SpectrumFrame? frame = null;
        var hopDuration = PresetPreviewSignalFactory.HopSize / (float)PresetPreviewSignalFactory.SampleRate;
        var time = 0f;

        for (var i = 0; i < 16; i++)
        {
            var pcm = PresetPreviewSignalFactory.CreatePcmFrame(
                preset,
                snapshot,
                time,
                PresetPreviewSignalFactory.HopSize,
                PresetPreviewSignalFactory.SampleRate);
            frame = analyzer.Process(pcm) ?? frame;
            time += hopDuration;
        }

        Assert.NotNull(frame);
        Assert.NotNull(frame!.DisplayBarX);
        Assert.NotNull(frame.DisplayBarWidth);
        Assert.Equal(frame.BandsDisplay.Length, frame.DisplayBarX!.Length);
        Assert.Equal(frame.BandsDisplay.Length, frame.DisplayBarWidth!.Length);
        Assert.Equal(64, frame.Bands64.Length);
        Assert.True(float.IsFinite(frame.Level));
        Assert.InRange(frame.Level, 0f, 4f);
        Assert.True(frame.BandsDisplay.Count(static value => value > 0.02f) >= 8);
        Assert.True(frame.BandsDisplay.Skip(frame.BandsDisplay.Length / 2).Any(static value => value > 0.015f));
    }

    private static PresetDefinition CreatePreset(string rendererId, int bandCount)
    {
        return new PresetDefinition
        {
            PresetId = "preview-pipeline-test",
            Name = "Preview Pipeline Test",
            RendererId = rendererId,
            DisplayBandCount = bandCount,
            Palette = new GradientPalette
            {
                Name = "Smoke",
                Stops =
                [
                    new PaletteStop { Offset = 0f, Color = new RgbaColor(255, 255, 255) },
                    new PaletteStop { Offset = 1f, Color = new RgbaColor(255, 255, 255) },
                ],
            },
        };
    }
}
