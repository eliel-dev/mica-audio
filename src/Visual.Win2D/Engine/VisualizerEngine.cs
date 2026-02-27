using Microsoft.Graphics.Canvas;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;
using Visual.Win2D.Renderers;
using Windows.UI;

namespace Visual.Win2D.Engine;

// DOCS: docs/wiki/modules/visual-win2d.md#modulo-visualwin2d
public sealed class VisualizerEngine
{
    private readonly Dictionary<string, IRenderer> renderers;
    private readonly PeakTracker peakTracker = new();

    public VisualizerEngine()
    {
        renderers = new Dictionary<string, IRenderer>(StringComparer.OrdinalIgnoreCase)
        {
            [RendererIds.Bars] = new BarsRenderer(),
            [RendererIds.CenterBars] = new CenteredBarsRenderer(),
            [RendererIds.LedBars] = new LedBarsRenderer(),
            [RendererIds.Line] = new LineRenderer(),
            [RendererIds.Area] = new AreaFillRenderer(),
            [RendererIds.Mirror] = new MirrorRenderer(),
            [RendererIds.Reflection] = new ReflectionRenderer(),
            [RendererIds.Peaks] = new PeakDotsRenderer(),
            [RendererIds.Radial] = new RadialRenderer(),
            [RendererIds.Spectrogram] = new SpectrogramRenderer(),
            [RendererIds.Pulse] = new PulseBackgroundRenderer(),
            [RendererIds.Waterfall] = new WaterfallRenderer(),
            [RendererIds.NeonGlow] = new NeonGlowBarsRenderer(),
            [RendererIds.AudioMotionClone] = new AudioMotionCloneRenderer(),
            [RendererIds.VizzyBlobNeon] = new VizzyBlobNeonRenderer(),
            [RendererIds.VizzyOrbitRings] = new VizzyOrbitRingsRenderer(),
            [RendererIds.VizzyHyperTunnel] = new VizzyHyperTunnelRenderer(),
        };
    }

    public IReadOnlyCollection<IRenderer> Renderers => renderers.Values.ToArray();

    public IReadOnlyList<string> RendererKeys => renderers.Keys.OrderBy(x => x).ToArray();

    public void Render(CanvasDrawingSession drawingSession, float width, float height, SpectrumFrame frame, PresetDefinition preset, float deltaSeconds)
    {
        // DOCS: docs/wiki/modules/visual-win2d.md#fluxo-de-execucao
        if (width <= 1f || height <= 1f)
        {
            return;
        }

        var renderer = ResolveRenderer(preset.RendererId);
        var palette = new PaletteSampler(preset.Palette);
        var holdMs = preset.RendererParameters.TryGetValue("peakHoldMs", out var v) ? v : 250f;
        var decay = preset.RendererParameters.TryGetValue("decay", out var d) ? MathF.Max(0.1f, 3.5f - (3f * d)) : 1.2f;
        var peaks = peakTracker.Update(frame.BandsDisplay, Math.Clamp(deltaSeconds, 0.001f, 0.5f), holdMs, decay);

        drawingSession.Clear(Color.FromArgb(255, 0, 0, 0));
        renderer.Render(new RenderContext
        {
            DrawingSession = drawingSession,
            Frame = frame,
            Preset = preset,
            Palette = palette,
            Peaks = peaks,
            Width = width,
            Height = height,
            DeltaSeconds = deltaSeconds,
        });
    }

    public bool TrySetRenderer(string rendererId, out string normalizedRendererId)
    {
        if (renderers.ContainsKey(rendererId))
        {
            normalizedRendererId = rendererId;
            return true;
        }

        normalizedRendererId = Engine.RendererIds.Bars;
        return false;
    }

    private IRenderer ResolveRenderer(string rendererId)
    {
        if (renderers.TryGetValue(rendererId, out var renderer))
        {
            return renderer;
        }

        return renderers[Engine.RendererIds.Bars];
    }
}
