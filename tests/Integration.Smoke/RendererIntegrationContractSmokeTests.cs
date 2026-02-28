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
        Assert.True(capabilities.UsesAnalyzerPipeline);
        Assert.True(capabilities.Controls.SupportsBarCount);
    }

    [Fact]
    public void VisualizerEngine_ShouldFallbackToLegacyCapabilities_ForLegacyRenderers()
    {
        var engine = new VisualizerEngine();
        var capabilities = engine.GetCapabilities(RendererIds.Bars);

        Assert.Equal(RendererIntegrationMode.LegacyAssumed, capabilities.IntegrationMode);
        Assert.Equal(RendererBarCountMode.Native, capabilities.BarCountMode);
        Assert.True(capabilities.UsesAnalyzerPipeline);
        Assert.True(capabilities.Controls.SupportsBarCount);
    }
}
