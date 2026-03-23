using App.WinUI.Services.Visualizer;
using Device.Protocol.Stream;
using Visual.Win2D.Engine;

namespace Integration.Smoke;

public sealed class Hub75BinsVisualIdentityResolverTests
{
    [Fact]
    public void ResolveFlags_ShouldMapWaveMirrorPresetToDedicatedStyle()
    {
        var flags = Hub75BinsVisualIdentityResolver.ResolveFlags("spectrum-wave-mirror", RendererIds.WaveMirror);

        Assert.Equal(Bins128VisualStyle.WaveMirror, Bins128VisualFlags.GetStyle(flags));
        Assert.Equal(Bins128PaletteFamily.Canonical, Bins128VisualFlags.GetPalette(flags));
    }

    [Fact]
    public void ResolveFlags_ShouldMapAudioMotionPaletteVariants()
    {
        var sunset = Hub75BinsVisualIdentityResolver.ResolveFlags("audiomotion-sunset", RendererIds.AudioMotionClone);
        var arctic = Hub75BinsVisualIdentityResolver.ResolveFlags("audiomotion-arctic", RendererIds.AudioMotionClone);
        var neon = Hub75BinsVisualIdentityResolver.ResolveFlags("audiomotion-neon", RendererIds.AudioMotionClone);

        Assert.Equal(Bins128VisualStyle.MirrorLines, Bins128VisualFlags.GetStyle(sunset));
        Assert.Equal(Bins128PaletteFamily.Sunset, Bins128VisualFlags.GetPalette(sunset));
        Assert.Equal(Bins128PaletteFamily.Arctic, Bins128VisualFlags.GetPalette(arctic));
        Assert.Equal(Bins128PaletteFamily.Neon, Bins128VisualFlags.GetPalette(neon));
    }

    [Fact]
    public void ResolveFlags_ShouldFallbackByRendererForRemainingTechnicalCombo()
    {
        var flags = Hub75BinsVisualIdentityResolver.ResolveFlags(null, RendererIds.VizzyOrbitRings);

        Assert.Equal(Bins128VisualStyle.RadialOrbit, Bins128VisualFlags.GetStyle(flags));
        Assert.Equal(Bins128PaletteFamily.Canonical, Bins128VisualFlags.GetPalette(flags));
    }

    [Fact]
    public void ResolveFlags_ShouldFallbackToLegacyForRetiredVisualizerIdentity()
    {
        var flags = Hub75BinsVisualIdentityResolver.ResolveFlags("spectrum-launchpad-grid", RendererIds.LaunchpadGrid);

        Assert.Equal(Bins128VisualStyle.LegacyFallback, Bins128VisualFlags.GetStyle(flags));
        Assert.Equal(Bins128PaletteFamily.Rainbow, Bins128VisualFlags.GetPalette(flags));
    }

    [Fact]
    public void ResolveFlags_ShouldFallbackToLegacyForUnknownIdentity()
    {
        var flags = Hub75BinsVisualIdentityResolver.ResolveFlags("custom-preset", "custom-renderer");

        Assert.Equal(Bins128VisualStyle.LegacyFallback, Bins128VisualFlags.GetStyle(flags));
        Assert.Equal(Bins128PaletteFamily.Rainbow, Bins128VisualFlags.GetPalette(flags));
    }
}
