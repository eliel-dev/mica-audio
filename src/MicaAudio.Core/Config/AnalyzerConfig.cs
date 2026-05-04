using MicaAudio.Core.Audio;

namespace MicaAudio.Core.Config;

// DOCS: docs/wiki/modules/analyzer-dsp.md#otimizacao-2026-03---fase-dsp-1-buffers-planos-e-output-only
public sealed class AnalyzerConfig
{
    public int SampleRate { get; init; } = 48_000;

    public int FftSize { get; init; } = VisualizerRuntimeDefaults.DefaultFftSize;

    public int HopSize { get; init; } = 256;

    public int DisplayBandCount { get; init; } = 38;

    public DisplayMode DisplayMode { get; init; } = DisplayMode.FixedBands;

    public float DisplayViewportWidthPx { get; init; }

    public float BarSpace { get; init; } = 0.1f;

    public int OutputBandCount { get; init; } = 64;

    public AnalyzerOutputMode OutputMode { get; init; } = AnalyzerOutputMode.DisplayAndOutput;

    public float MinHz { get; init; } = 30f;

    public float MaxHz { get; init; } = 16_000f;

    public ScaleMode ScaleMode { get; init; } = ScaleMode.Linear;

    public FrequencyScale FrequencyScale { get; init; } = FrequencyScale.Logarithmic;

    // Matches the behavior of WebAudio's AnalyserNode.smoothingTimeConstant (0..0.99).
    public float FftSmoothing { get; init; }

    public WeightingFilter WeightingFilter { get; init; } = WeightingFilter.Off;

    public bool UseLinearAmplitude { get; init; } = true;

    // Boost applied after linear amplitude normalization, similar to audioMotion's linearBoost.
    public float LinearBoost { get; init; } = 1f;

    public float MinDecibels { get; init; } = -85f;

    public float MaxDecibels { get; init; } = -25f;

    public float DisplaySmoothingRise { get; init; } = 0.82f;

    public float DisplaySmoothingFall { get; init; } = 0.06f;

    public float DisplayMotionDamping { get; init; } = 0.30f;

    public float OutputSmoothingRise { get; init; } = 0.82f;

    public float OutputSmoothingFall { get; init; } = 0.06f;

    public float OutputMotionDamping { get; init; } = 0.30f;

    public float LevelCompression { get; init; } = 1.2f;

    public float InputGain { get; init; } = 1f;
}
