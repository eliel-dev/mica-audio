using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#navegacao-de-presets-por-teclado
internal static class VisualizerAnalyzerConfigFactory
{
    public static AnalyzerConfig Build(
        PresetDefinition preset,
        VisualizerRuntimeSettings settings,
        float viewportWidthPx,
        string? rendererId = null,
        AnalyzerOutputMode outputMode = AnalyzerOutputMode.DisplayAndOutput)
    {
        return AnalyzerRuntimeProfile
            .From(settings, preset, viewportWidthPx, rendererId, outputMode)
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
        float viewportWidthPx,
        AnalyzerOutputMode outputMode = AnalyzerOutputMode.DisplayAndOutput)
    {
        var settings = VisualizerRuntimeSettings.From(new AppSettings
        {
            LinearBoost = linearBoost,
            BarCount = preset.DisplayBandCount,
            FftSize = VisualizerRuntimeDefaults.DefaultFftSize,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            FrequencyScale = frequencyScale,
            FrequencyMinHz = frequencyMinHz,
            FrequencyMaxHz = frequencyMaxHz,
        });

        return Build(preset, settings, viewportWidthPx, outputMode: outputMode);
    }
}




