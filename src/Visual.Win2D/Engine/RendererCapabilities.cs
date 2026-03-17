namespace Visual.Win2D.Engine;

public sealed class RendererCapabilities
{
    public bool UsesAnalyzerPipeline { get; init; } = true;

    public RendererControlSupport Controls { get; init; } = RendererControlSupport.AnalyzerDefaults;

    public RendererBarCountMode BarCountMode { get; init; } = RendererBarCountMode.Native;

    public RendererIntegrationMode IntegrationMode { get; init; } = RendererIntegrationMode.LegacyAssumed;

    public RendererHubTransportMode HubTransportMode { get; init; } = RendererHubTransportMode.Bins128;

    public int? FixedVisualElementCount { get; init; }

    public string? UnsupportedControlsHint { get; init; }

    public static RendererCapabilities CreateLegacyAssumed()
    {
        return new RendererCapabilities
        {
            Controls = RendererControlSupport.AnalyzerDefaults,
            BarCountMode = RendererBarCountMode.Native,
            IntegrationMode = RendererIntegrationMode.LegacyAssumed,
            HubTransportMode = RendererHubTransportMode.Bins128,
        };
    }
}
