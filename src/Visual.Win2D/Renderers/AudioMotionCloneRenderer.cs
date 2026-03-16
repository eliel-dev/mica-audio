using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
public sealed class AudioMotionCloneRenderer : IRenderer, IRendererCapabilitiesProvider
{
    private static readonly RendererCapabilities ExplicitCapabilities = new()
    {
        UsesAnalyzerPipeline = true,
        Controls = new RendererControlSupport
        {
            SupportsSensitivity = true,
            SupportsLinearBoost = true,
            SupportsBarCount = false,
            SupportsFftSize = true,
            SupportsFftSmoothing = true,
            SupportsWeightingFilter = true,
            SupportsFrequencyScale = true,
            SupportsFrequencyRange = true,
        },
        BarCountMode = RendererBarCountMode.Native,
        IntegrationMode = RendererIntegrationMode.Explicit,
        HubTransportMode = RendererHubTransportMode.Frame128x64,
        UnsupportedControlsHint = "No modo AudioMotion Clone, a quantidade de barras e automatica pela largura da tela.",
    };

    public string RendererId => RendererIds.AudioMotionClone;

    public string DisplayName => "AudioMotion Clone";

    public RendererCapabilities Capabilities => ExplicitCapabilities;

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var midY = context.Height * 0.5f;
        var heightScale = Math.Clamp(context.Param("heightScale", 0.78f), 0.1f, 1f);
        var minHalfHeight = Math.Clamp(context.Param("minHalfHeight", 0f), 0f, midY);
        var maxHalfHeight = midY * heightScale;
        var lineThickness = Math.Clamp(context.Param("lineThickness", 3f), 1f, 12f);
        var edgePaddingPx = Math.Clamp(context.Param("edgePaddingPx", 0f), 0f, Math.Max(0f, context.Width * 0.1f));

        var hasGeometry = context.Frame.DisplayBarX is { Length: > 0 }
            && context.Frame.DisplayBarWidth is { Length: > 0 }
            && context.Frame.DisplayBarX.Length == bands.Length
            && context.Frame.DisplayBarWidth.Length == bands.Length;

        var slot = context.Width / bands.Length;
        for (var i = 0; i < bands.Length; i++)
        {
            var value = Math.Clamp(bands[i], 0f, 1f);
            var halfHeight = minHalfHeight + (value * Math.Max(0f, maxHalfHeight - minHalfHeight));
            var top = midY - halfHeight;
            var bottom = midY + halfHeight;

            float centerX;
            float effectiveThickness;

            if (hasGeometry)
            {
                var x = context.Frame.DisplayBarX![i] * context.Width;
                var width = Math.Max(1f, context.Frame.DisplayBarWidth![i] * context.Width);
                centerX = x + (width * 0.5f);
                effectiveThickness = Math.Clamp(lineThickness, 1f, Math.Max(1f, width));
            }
            else
            {
                centerX = (i * slot) + (slot * 0.5f);
                effectiveThickness = Math.Clamp(lineThickness, 1f, Math.Max(1f, slot));
            }

            var halfThickness = effectiveThickness * 0.5f;
            var leftBound = halfThickness + edgePaddingPx;
            var rightBound = Math.Max(leftBound, context.Width - halfThickness - edgePaddingPx);
            centerX = Math.Clamp(centerX, leftBound, rightBound);

            var t = i / (float)Math.Max(1, bands.Length - 1);
            var color = context.Palette.ColorAt(t);
            ds.DrawLine(centerX, top, centerX, bottom, color, effectiveThickness);
        }
    }
}
