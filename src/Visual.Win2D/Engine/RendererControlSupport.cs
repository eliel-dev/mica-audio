namespace Visual.Win2D.Engine;

public sealed class RendererControlSupport
{
    public static RendererControlSupport AnalyzerDefaults { get; } = new();

    public bool SupportsSensitivity { get; init; } = true;

    public bool SupportsLinearBoost { get; init; } = true;

    public bool SupportsBarCount { get; init; } = true;

    public bool SupportsFftSize { get; init; } = true;

    public bool SupportsFftSmoothing { get; init; } = true;

    public bool SupportsWeightingFilter { get; init; } = true;

    public bool SupportsFrequencyScale { get; init; } = true;

    public bool SupportsFrequencyRange { get; init; } = true;
}
