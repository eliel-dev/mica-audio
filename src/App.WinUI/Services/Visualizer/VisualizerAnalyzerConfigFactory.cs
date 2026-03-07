using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#navegacao-de-presets-por-teclado
internal static class VisualizerAnalyzerConfigFactory
{
    public static AnalyzerConfig Build(
        PresetDefinition preset,
        VisualizerRuntimeSettings settings,
        float viewportWidthPx,
        string? rendererId = null)
    {
        return AnalyzerRuntimeProfile
            .From(settings, preset, viewportWidthPx, rendererId)
            .ToAnalyzerConfig();
    }

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
        var settings = VisualizerRuntimeSettings.From(new AppSettings
        {
            LinearBoost = linearBoost,
            BarCount = preset.DisplayBandCount,
            FftSize = fftSize,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            FrequencyScale = frequencyScale,
            FrequencyMinHz = frequencyMinHz,
            FrequencyMaxHz = frequencyMaxHz,
        });

        return Build(preset, settings, viewportWidthPx);
    }
}




