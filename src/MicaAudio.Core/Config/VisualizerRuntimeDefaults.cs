using MicaAudio.Core.Audio;

namespace MicaAudio.Core.Config;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#pontos-de-alteracao-frequente
internal static class VisualizerRuntimeDefaults
{
    public const string DefaultPresetId = "audiomotion-clone";
    public const string DefaultRendererId = "audiomotion-clone";
    public const int DefaultSampleRate = 48_000;
    public const int DefaultHopSize = 256;
    public const float DefaultMinDecibels = -85f;
    public const float DefaultMaxDecibels = -25f;
    public const float DefaultLinearBoost = 1.3f;
    public const int DefaultBarCount = 38;
    public const int DefaultFftSize = 2048;
    public const float DefaultFftSmoothing = 0.75f;
    public const WeightingFilter DefaultWeightingFilter = WeightingFilter.B;
    public const FrequencyScale DefaultFrequencyScale = FrequencyScale.Bark;
    public const float DefaultFrequencyMinHz = 20f;
    public const float DefaultFrequencyMaxHz = 1000f;
    public const int OutputBandCount = 64;
}
