namespace Visual.Win2D.Engine;

public sealed class RendererControlSupport
{
    public static RendererControlSupport AnalyzerDefaults { get; } = new();

    public bool SupportsBarCount { get; init; } = true;
}
