using MicaAudio.Core.Audio;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#galeria-de-presets
internal readonly record struct PresetPreviewSettingsSnapshot(
    int DisplayBandCount,
    int FftSize,
    float FftSmoothing,
    float LinearBoost,
    FrequencyScale FrequencyScale,
    WeightingFilter WeightingFilter,
    float FrequencyMinHz,
    float FrequencyMaxHz,
    float AnalyzerViewportWidthPx)
{
    public static PresetPreviewSettingsSnapshot Default => new(
        38,
        2048,
        0.75f,
        1.6f,
        FrequencyScale.Bark,
        WeightingFilter.B,
        20f,
        1000f,
        1280f);
}
