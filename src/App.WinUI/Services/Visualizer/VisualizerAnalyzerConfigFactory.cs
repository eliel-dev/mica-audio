using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#navegacao-de-presets-por-teclado
internal static class VisualizerAnalyzerConfigFactory
{
    private const float DefaultMinDecibels = -85f;
    private const float DefaultMaxDecibels = -25f;
    private const int DefaultSampleRate = 48_000;
    private const int DefaultHopSize = 256;

    public static AnalyzerConfig Build(
        PresetDefinition preset,
        int fftSize,
        float fftSmoothing,
        float linearBoost,
        FrequencyScale frequencyScale,
        WeightingFilter weightingFilter,
        float frequencyMinHz,
        float frequencyMaxHz,
        float viewportWidthPx)
    {
        var cloneMode = string.Equals(preset.RendererId, RendererIds.AudioMotionClone, StringComparison.OrdinalIgnoreCase);
        var barSpace = preset.RendererParameters.TryGetValue("barSpace", out var configuredBarSpace)
            ? configuredBarSpace
            : 0.10f;
        var analyzerViewportWidth = cloneMode
            ? MathF.Max(2f, viewportWidthPx > 1f ? viewportWidthPx : 2f)
            : 0f;

        return new AnalyzerConfig
        {
            SampleRate = DefaultSampleRate,
            FftSize = fftSize,
            HopSize = DefaultHopSize,
            DisplayBandCount = Math.Clamp(preset.DisplayBandCount, 8, 256),
            DisplayMode = cloneMode ? DisplayMode.AudioMotionMode0 : DisplayMode.FixedBands,
            DisplayViewportWidthPx = analyzerViewportWidth,
            BarSpace = Math.Clamp(barSpace, 0f, 0.95f),
            // SpectrumAnalyzer ainda valida rigidamente 64 bands no output interno.
            // O caminho HUB75 continua indo para 128 colunas via resample posterior.
            OutputBandCount = 64,
            MinHz = frequencyMinHz,
            MaxHz = frequencyMaxHz,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = frequencyScale,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            UseLinearAmplitude = true,
            LinearBoost = linearBoost,
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




