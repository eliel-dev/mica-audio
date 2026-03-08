using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;

namespace MicaAudio.Core.Config;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#modulo-settings-presets-e-persistencia
internal sealed class VisualizerRuntimeSettings
{
    public static VisualizerRuntimeSettings Default { get; } = From(new AppSettings());

    public required string ActivePresetId { get; init; }

    public required string SelectedRendererId { get; init; }

    public required float LinearBoost { get; init; }

    public required int DisplayBandCount { get; init; }

    public required int FftSize { get; init; }

    public required float FftSmoothing { get; init; }

    public required WeightingFilter WeightingFilter { get; init; }

    public required FrequencyScale FrequencyScale { get; init; }

    public required float FrequencyMinHz { get; init; }

    public required float FrequencyMaxHz { get; init; }

    public static VisualizerRuntimeSettings From(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var minHz = NormalizeFrequencyMin(settings.FrequencyMinHz);
        var maxHz = NormalizeFrequencyMax(settings.FrequencyMaxHz);
        if (maxHz <= minHz)
        {
            minHz = VisualizerRuntimeDefaults.DefaultFrequencyMinHz;
            maxHz = VisualizerRuntimeDefaults.DefaultFrequencyMaxHz;
        }

        return new VisualizerRuntimeSettings
        {
            ActivePresetId = NormalizePresetId(settings.ActivePresetId),
            SelectedRendererId = NormalizeRendererId(settings.SelectedRendererId),
            LinearBoost = NormalizeLinearBoost(settings.LinearBoost),
            DisplayBandCount = NormalizeDisplayBandCount(settings.BarCount),
            FftSize = NormalizeFftSize(settings.FftSize),
            FftSmoothing = NormalizeFftSmoothing(settings.FftSmoothing),
            WeightingFilter = NormalizeWeightingFilter(settings.WeightingFilter),
            FrequencyScale = NormalizeFrequencyScale(settings.FrequencyScale),
            FrequencyMinHz = minHz,
            FrequencyMaxHz = maxHz,
        };
    }

    public static string NormalizePresetId(string? presetId)
        => string.IsNullOrWhiteSpace(presetId) ? VisualizerRuntimeDefaults.DefaultPresetId : presetId;

    public static string NormalizeRendererId(string? rendererId)
        => string.IsNullOrWhiteSpace(rendererId) ? VisualizerRuntimeDefaults.DefaultRendererId : rendererId;

    public static float NormalizeLinearBoost(float value)
        => VisualizerRuntimeDefaults.DefaultLinearBoost;

    public static int NormalizeDisplayBandCount(int value)
        => value <= 0 ? VisualizerRuntimeDefaults.DefaultBarCount : Math.Clamp(value, 8, 128);

    public static int NormalizeFftSize(int value)
        => FftSizePolicy.CanonicalSize;

    public static float NormalizeFftSmoothing(float value)
        => Math.Clamp(value, 0f, 0.99f);

    public static WeightingFilter NormalizeWeightingFilter(WeightingFilter value)
        => Enum.IsDefined(value) ? value : WeightingFilter.Off;

    public static FrequencyScale NormalizeFrequencyScale(FrequencyScale value)
        => Enum.IsDefined(value) ? value : VisualizerRuntimeDefaults.DefaultFrequencyScale;

    public static float NormalizeFrequencyMin(float value)
        => Math.Clamp(value, 16f, 10_000f);

    public static float NormalizeFrequencyMax(float value)
        => Math.Clamp(value, 250f, 20_000f);
}
