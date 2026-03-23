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
    public void VisualizerEngine_ShouldNotExposeRetiredRendererCapabilities_InActiveRuntime()
    {
        var engine = new VisualizerEngine();
        var rendererIds = engine.Renderers.Select(static renderer => renderer.RendererId).ToArray();

        Assert.DoesNotContain(RendererIds.AuroraRibbon, rendererIds);
        Assert.DoesNotContain(RendererIds.LaunchpadGrid, rendererIds);
        Assert.DoesNotContain(RendererIds.PlasmaPulse, rendererIds);
        Assert.DoesNotContain(RendererIds.PolarArcs, rendererIds);
        Assert.DoesNotContain(RendererIds.Pulse, rendererIds);
        Assert.DoesNotContain(RendererIds.Spectrogram, rendererIds);
        Assert.DoesNotContain(RendererIds.VizzyBlobNeon, rendererIds);
        Assert.DoesNotContain(RendererIds.Waterfall, rendererIds);
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
