using Visual.Win2D.Engine;

namespace Integration.Smoke;

public sealed class RendererIntegrationContractSmokeTests
{
    [Fact]
    public void VisualizerEngine_ShouldReturnExplicitCapabilities_ForAudioMotionClone()
    {
        var engine = new VisualizerEngine();
        var capabilities = engine.GetCapabilities(RendererIds.AudioMotionClone);

        Assert.Equal(RendererIntegrationMode.Explicit, capabilities.IntegrationMode);
        Assert.Equal(RendererBarCountMode.Native, capabilities.BarCountMode);
        Assert.Equal(RendererHubTransportMode.Bins128, capabilities.HubTransportMode);
        Assert.True(capabilities.UsesAnalyzerPipeline);
        Assert.False(capabilities.Controls.SupportsBarCount);
    }

    [Fact]
    public void VisualizerEngine_ShouldReturnExplicitCapabilities_ForWaveMirror()
    {
        var engine = new VisualizerEngine();
        var capabilities = engine.GetCapabilities(RendererIds.WaveMirror);

        Assert.Equal(RendererIntegrationMode.Explicit, capabilities.IntegrationMode);
        Assert.Equal(RendererBarCountMode.Native, capabilities.BarCountMode);
        Assert.Equal(RendererHubTransportMode.Bins128, capabilities.HubTransportMode);
        Assert.True(capabilities.UsesAnalyzerPipeline);
        Assert.False(capabilities.Controls.SupportsBarCount);
    }

    [Fact]
    public void VisualizerEngine_ShouldReturnExplicitCapabilities_ForPolarArcs()
    {
        var engine = new VisualizerEngine();
        var capabilities = engine.GetCapabilities(RendererIds.PolarArcs);

        Assert.Equal(RendererIntegrationMode.Explicit, capabilities.IntegrationMode);
        Assert.Equal(RendererBarCountMode.Resampled, capabilities.BarCountMode);
        Assert.Equal(RendererHubTransportMode.Bins128, capabilities.HubTransportMode);
        Assert.True(capabilities.UsesAnalyzerPipeline);
        Assert.True(capabilities.Controls.SupportsBarCount);
    }

    [Fact]
    public void VisualizerEngine_ShouldReturnExplicitCapabilities_ForLaunchpadGrid()
    {
        var engine = new VisualizerEngine();
        var capabilities = engine.GetCapabilities(RendererIds.LaunchpadGrid);

        Assert.Equal(RendererIntegrationMode.Explicit, capabilities.IntegrationMode);
        Assert.Equal(RendererBarCountMode.Fixed, capabilities.BarCountMode);
        Assert.Equal(RendererHubTransportMode.Bins128, capabilities.HubTransportMode);
        Assert.Equal(64, capabilities.FixedVisualElementCount);
        Assert.True(capabilities.UsesAnalyzerPipeline);
        Assert.False(capabilities.Controls.SupportsBarCount);
        Assert.Equal("Launchpad Grid usa uma grade fixa de 64 pads.", capabilities.UnsupportedControlsHint);
    }

    [Fact]
    public void VisualizerEngine_ShouldFallbackToLegacyCapabilities_ForLegacyRenderers()
    {
        var engine = new VisualizerEngine();
        var capabilities = engine.GetCapabilities(RendererIds.Bars);

        Assert.Equal(RendererIntegrationMode.LegacyAssumed, capabilities.IntegrationMode);
        Assert.Equal(RendererBarCountMode.Native, capabilities.BarCountMode);
        Assert.Equal(RendererHubTransportMode.Bins128, capabilities.HubTransportMode);
        Assert.True(capabilities.UsesAnalyzerPipeline);
        Assert.True(capabilities.Controls.SupportsBarCount);
    }
}
