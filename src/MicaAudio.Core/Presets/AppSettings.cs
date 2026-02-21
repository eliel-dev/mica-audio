using MicaAudio.Core.Audio;

namespace MicaAudio.Core.Presets;

public sealed class AppSettings
{
    public string ActivePresetId { get; init; } = "audiomotion-clone";

    public string SelectedRendererId { get; init; } = "audiomotion-clone";

    public bool Hub75PreviewEnabled { get; init; }

    public float Brightness { get; init; } = 0.9f;

    public float Sensitivity { get; init; } = -25f;

    public float SensitivityMinDb { get; init; } = -85f;

    public float SensitivityMaxDb { get; init; } = -25f;

    public float LinearBoost { get; init; } = 1.6f;

    public int BarCount { get; init; } = 38;

    public FrequencyScale FrequencyScale { get; init; } = FrequencyScale.Bark;

    public float FrequencyMinHz { get; init; } = 20f;

    public float FrequencyMaxHz { get; init; } = 1000f;

    public int FftSize { get; init; } = 2048;

    public float FftSmoothing { get; init; } = 0.75f;

    public WeightingFilter WeightingFilter { get; init; } = WeightingFilter.B;

    public int WindowWidth { get; init; }

    public int WindowHeight { get; init; }
}
