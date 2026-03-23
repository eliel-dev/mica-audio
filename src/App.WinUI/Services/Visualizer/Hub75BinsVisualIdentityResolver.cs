using Device.Protocol.Stream;
using Visual.Win2D.Engine;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#hub75-transport-policy
// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128
internal static class Hub75BinsVisualIdentityResolver
{
    public static byte ResolveFlags(string? presetId, string? rendererId)
    {
        var style = ResolveStyle(presetId, rendererId);
        var palette = ResolvePalette(presetId, rendererId, style);
        return Bins128VisualFlags.Create(style, palette);
    }

    internal static Bins128VisualStyle ResolveStyle(string? presetId, string? rendererId)
    {
        if (!string.IsNullOrWhiteSpace(presetId))
        {
            switch (presetId.Trim())
            {
                case "spectrum-wave-mirror":
                    return Bins128VisualStyle.WaveMirror;
                case "audiomotion-clone":
                case "audiomotion-sunset":
                case "audiomotion-arctic":
                case "audiomotion-neon":
                case "spectrum-centered-lines":
                    return Bins128VisualStyle.MirrorLines;
                case "spectrum-centered-blocks":
                case "spectrum-mirror":
                case "spectrum-reflection":
                    return Bins128VisualStyle.MirrorBlocks;
                case "spectrum-bars":
                case "spectrum-led":
                case "spectrum-peaks":
                case "spectrum-neon-glow":
                    return Bins128VisualStyle.ClassicBars;
                case "spectrum-line":
                case "spectrum-area":
                    return Bins128VisualStyle.FlowLine;
                case "spectrum-radial":
                case "spectrum-vizzy-orbit-rings":
                    return Bins128VisualStyle.RadialOrbit;
            }
        }

        return ResolveRendererFallbackStyle(rendererId);
    }

    internal static Bins128PaletteFamily ResolvePalette(
        string? presetId,
        string? rendererId,
        Bins128VisualStyle resolvedStyle)
    {
        if (!string.IsNullOrWhiteSpace(presetId))
        {
            switch (presetId.Trim())
            {
                case "audiomotion-clone":
                case "spectrum-bars":
                case "spectrum-centered-lines":
                case "spectrum-mirror":
                case "spectrum-radial":
                    return Bins128PaletteFamily.Rainbow;
                case "audiomotion-sunset":
                case "spectrum-centered-blocks":
                case "spectrum-area":
                    return Bins128PaletteFamily.Sunset;
                case "audiomotion-arctic":
                case "spectrum-line":
                case "spectrum-reflection":
                    return Bins128PaletteFamily.Arctic;
                case "audiomotion-neon":
                case "spectrum-peaks":
                case "spectrum-neon-glow":
                    return Bins128PaletteFamily.Neon;
                case "spectrum-wave-mirror":
                case "spectrum-vizzy-orbit-rings":
                    return Bins128PaletteFamily.Canonical;
            }
        }

        return ResolveRendererFallbackPalette(rendererId, resolvedStyle);
    }

    private static Bins128VisualStyle ResolveRendererFallbackStyle(string? rendererId)
    {
        return rendererId switch
        {
            RendererIds.WaveMirror => Bins128VisualStyle.WaveMirror,
            RendererIds.AudioMotionClone or RendererIds.CenterBars => Bins128VisualStyle.MirrorLines,
            RendererIds.Mirror or RendererIds.Reflection => Bins128VisualStyle.MirrorBlocks,
            RendererIds.Bars or RendererIds.LedBars or RendererIds.Peaks or RendererIds.NeonGlow => Bins128VisualStyle.ClassicBars,
            RendererIds.Line or RendererIds.Area => Bins128VisualStyle.FlowLine,
            RendererIds.Radial or RendererIds.VizzyOrbitRings => Bins128VisualStyle.RadialOrbit,
            _ => Bins128VisualStyle.LegacyFallback,
        };
    }

    private static Bins128PaletteFamily ResolveRendererFallbackPalette(string? rendererId, Bins128VisualStyle resolvedStyle)
    {
        return rendererId switch
        {
            RendererIds.Line or RendererIds.Reflection => Bins128PaletteFamily.Arctic,
            RendererIds.Area => Bins128PaletteFamily.Sunset,
            RendererIds.Peaks or RendererIds.NeonGlow => Bins128PaletteFamily.Neon,
            RendererIds.WaveMirror or RendererIds.VizzyOrbitRings => Bins128PaletteFamily.Canonical,
            RendererIds.Bars or RendererIds.AudioMotionClone or RendererIds.CenterBars or RendererIds.Mirror or RendererIds.Radial or RendererIds.LedBars => Bins128PaletteFamily.Rainbow,
            _ => resolvedStyle switch
            {
                Bins128VisualStyle.WaveMirror or Bins128VisualStyle.RadialOrbit => Bins128PaletteFamily.Canonical,
                _ => Bins128PaletteFamily.Rainbow,
            },
        };
    }
}
