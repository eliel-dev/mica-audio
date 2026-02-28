using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#galeria-de-presets
internal static class VisualizerAnalyzerConfigFactory
{
    private const float DefaultMinDecibels = -85f;
    private const float DefaultMaxDecibels = -25f;

    public static AnalyzerConfig Build(PresetDefinition preset, PresetPreviewSettingsSnapshot settings, float viewportWidthPx)
    {
        var cloneMode = string.Equals(preset.RendererId, RendererIds.AudioMotionClone, StringComparison.OrdinalIgnoreCase);
        var barSpace = preset.RendererParameters.TryGetValue("barSpace", out var configuredBarSpace)
            ? configuredBarSpace
            : 0.10f;
        var analyzerViewportWidth = cloneMode
            ? MathF.Max(2f, settings.AnalyzerViewportWidthPx > 1f ? settings.AnalyzerViewportWidthPx : viewportWidthPx)
            : 0f;

        return new AnalyzerConfig
        {
            SampleRate = PresetPreviewSignalFactory.SampleRate,
            FftSize = settings.FftSize,
            HopSize = PresetPreviewSignalFactory.HopSize,
            DisplayBandCount = Math.Clamp(preset.DisplayBandCount, 8, 256),
            DisplayMode = cloneMode ? DisplayMode.AudioMotionMode0 : DisplayMode.FixedBands,
            DisplayViewportWidthPx = analyzerViewportWidth,
            BarSpace = Math.Clamp(barSpace, 0f, 0.95f),
            OutputBandCount = LedDefaults.MatrixWidth,
            MinHz = settings.FrequencyMinHz,
            MaxHz = settings.FrequencyMaxHz,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = settings.FrequencyScale,
            FftSmoothing = settings.FftSmoothing,
            WeightingFilter = settings.WeightingFilter,
            UseLinearAmplitude = true,
            LinearBoost = settings.LinearBoost,
            MinDecibels = DefaultMinDecibels,
            MaxDecibels = DefaultMaxDecibels,
            DbFloor = DefaultMinDecibels,
            DbCeiling = DefaultMaxDecibels,
            DisplaySmoothingRise = 0.82f,
            DisplaySmoothingFall = 0.06f,
            DisplayMotionDamping = 0.30f,
            OutputSmoothingRise = 0.82f,
            OutputSmoothingFall = 0.06f,
            OutputMotionDamping = 0.30f,
            InputGain = 1f,
        };
    }
}
