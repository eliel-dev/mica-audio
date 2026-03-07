using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;

namespace MicaAudio.Core.Config;

// DOCS: docs/wiki/modules/analyzer-dsp.md#fluxo-de-execucao
internal sealed class AnalyzerRuntimeProfile
{
    private AnalyzerRuntimeProfile(
        string rendererId,
        int displayBandCount,
        DisplayMode displayMode,
        float displayViewportWidthPx,
        float barSpace,
        VisualizerRuntimeSettings settings)
    {
        RendererId = rendererId;
        DisplayBandCount = displayBandCount;
        DisplayMode = displayMode;
        DisplayViewportWidthPx = displayViewportWidthPx;
        BarSpace = barSpace;
        Settings = settings;
    }

    public string RendererId { get; }

    public int DisplayBandCount { get; }

    public DisplayMode DisplayMode { get; }

    public float DisplayViewportWidthPx { get; }

    public float BarSpace { get; }

    public VisualizerRuntimeSettings Settings { get; }

    public static AnalyzerRuntimeProfile From(
        VisualizerRuntimeSettings settings,
        PresetDefinition preset,
        float viewportWidthPx,
        string? rendererId = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(preset);

        var resolvedRendererId = string.IsNullOrWhiteSpace(rendererId)
            ? VisualizerRuntimeSettings.NormalizeRendererId(preset.RendererId)
            : VisualizerRuntimeSettings.NormalizeRendererId(rendererId);
        var cloneMode = string.Equals(
            resolvedRendererId,
            VisualizerRuntimeDefaults.DefaultRendererId,
            StringComparison.OrdinalIgnoreCase);
        var barSpace = preset.RendererParameters.TryGetValue("barSpace", out var configuredBarSpace)
            ? configuredBarSpace
            : 0.10f;
        var displayBandCount = preset.DisplayBandCount > 0
            ? preset.DisplayBandCount
            : settings.DisplayBandCount;

        return new AnalyzerRuntimeProfile(
            resolvedRendererId,
            Math.Clamp(displayBandCount, 8, 256),
            cloneMode ? DisplayMode.AudioMotionMode0 : DisplayMode.FixedBands,
            cloneMode ? MathF.Max(2f, viewportWidthPx > 1f ? viewportWidthPx : 2f) : 0f,
            Math.Clamp(barSpace, 0f, 0.95f),
            settings);
    }

    public AnalyzerConfig ToAnalyzerConfig()
    {
        return new AnalyzerConfig
        {
            SampleRate = VisualizerRuntimeDefaults.DefaultSampleRate,
            FftSize = Settings.FftSize,
            HopSize = VisualizerRuntimeDefaults.DefaultHopSize,
            DisplayBandCount = DisplayBandCount,
            DisplayMode = DisplayMode,
            DisplayViewportWidthPx = DisplayViewportWidthPx,
            BarSpace = BarSpace,
            OutputBandCount = VisualizerRuntimeDefaults.OutputBandCount,
            MinHz = Settings.FrequencyMinHz,
            MaxHz = Settings.FrequencyMaxHz,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = Settings.FrequencyScale,
            FftSmoothing = Settings.FftSmoothing,
            WeightingFilter = Settings.WeightingFilter,
            UseLinearAmplitude = true,
            LinearBoost = Settings.LinearBoost,
            MinDecibels = VisualizerRuntimeDefaults.DefaultMinDecibels,
            MaxDecibels = VisualizerRuntimeDefaults.DefaultMaxDecibels,
            DbFloor = VisualizerRuntimeDefaults.DefaultMinDecibels,
            DbCeiling = VisualizerRuntimeDefaults.DefaultMaxDecibels,
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
