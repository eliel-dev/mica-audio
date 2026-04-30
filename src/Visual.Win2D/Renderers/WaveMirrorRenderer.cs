using Visual.Win2D.Engine;
using Windows.UI;

namespace Visual.Win2D.Renderers;

// DOCS: docs/wiki/modules/visual-win2d.md#wave-mirror
public sealed class WaveMirrorRenderer : IRenderer, IRendererCapabilitiesProvider
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
        HubTransportMode = RendererHubTransportMode.Bins128,
        UnsupportedControlsHint = "No modo Wave Mirror, a quantidade de ondas e automatica pela largura da tela.",
    };

    private static readonly Color[] RainbowStops =
    [
        Color.FromArgb(255, 255, 36, 24),
        Color.FromArgb(255, 255, 154, 28),
        Color.FromArgb(255, 184, 255, 72),
        Color.FromArgb(255, 48, 255, 116),
        Color.FromArgb(255, 74, 228, 255),
        Color.FromArgb(255, 52, 118, 255),
        Color.FromArgb(255, 255, 84, 196),
    ];

    public string RendererId => RendererIds.WaveMirror;

    public string DisplayName => "Wave Mirror";

    public RendererCapabilities Capabilities => ExplicitCapabilities;

    public void Render(RenderContext context)
    {
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0 || context.Width <= 1f || context.Height <= 1f)
        {
            return;
        }

        var midY = context.Height * 0.5f;
        var maxHalfHeight = midY * Math.Clamp(context.Param("heightScale", 0.82f), 0.15f, 1f);
        var minHalfHeight = Math.Clamp(context.Param("minHalfHeight", 0f), 0f, midY);
        var centerLineThickness = Math.Clamp(context.Param("centerLineThickness", 2.4f), 1f, 10f);
        var waveThickness = Math.Clamp(context.Param("waveThickness", 1.6f), 1f, 6f);
        var edgePaddingPx = Math.Clamp(context.Param("edgePaddingPx", 0f), 0f, Math.Max(0f, context.Width * 0.08f));
        var waveGlowAlpha = Math.Clamp(context.Param("waveGlowAlpha", 0.18f), 0f, 0.85f);
        var leftBound = edgePaddingPx;
        var rightBound = Math.Max(leftBound + 1f, context.Width - edgePaddingPx);

        var knotXs = new float[bands.Length + 2];
        var knotHeights = new float[bands.Length + 2];
        knotXs[0] = leftBound;
        knotHeights[0] = 0f;

        var hasGeometry = context.Frame.DisplayBarX is { Length: > 0 }
            && context.Frame.DisplayBarWidth is { Length: > 0 }
            && context.Frame.DisplayBarX.Length == bands.Length
            && context.Frame.DisplayBarWidth.Length == bands.Length;

        var slot = context.Width / bands.Length;
        var previousCenter = leftBound;
        for (var i = 0; i < bands.Length; i++)
        {
            var smoothed = SmoothBand(bands, i);
            var shaped = MathF.Pow(Math.Clamp(smoothed, 0f, 1f), 0.90f);
            knotHeights[i + 1] = minHalfHeight + (shaped * Math.Max(0f, maxHalfHeight - minHalfHeight));

            float centerX;
            if (hasGeometry)
            {
                var x = context.Frame.DisplayBarX![i] * context.Width;
                var width = Math.Max(1f, context.Frame.DisplayBarWidth![i] * context.Width);
                centerX = x + (width * 0.5f);
            }
            else
            {
                centerX = (i * slot) + (slot * 0.5f);
            }

            centerX = Math.Clamp(centerX, leftBound, rightBound);
            centerX = Math.Max(previousCenter, centerX);
            knotXs[i + 1] = centerX;
            previousCenter = centerX;
        }

        knotXs[^1] = rightBound;
        knotHeights[^1] = 0f;

        var ds = context.DrawingSession;
        var startPixel = Math.Max(0, (int)MathF.Floor(leftBound));
        var endPixel = Math.Max(startPixel + 1, (int)MathF.Ceiling(rightBound));
        var segmentIndex = 0;

        for (var pixel = startPixel; pixel <= endPixel; pixel++)
        {
            var x = Math.Clamp(pixel + 0.5f, leftBound, rightBound);
            while (segmentIndex < knotXs.Length - 2 && x > knotXs[segmentIndex + 1])
            {
                segmentIndex++;
            }

            var x0 = knotXs[segmentIndex];
            var x1 = knotXs[segmentIndex + 1];
            var height0 = knotHeights[segmentIndex];
            var height1 = knotHeights[segmentIndex + 1];
            var t = x1 <= x0 ? 0f : Math.Clamp((x - x0) / (x1 - x0), 0f, 1f);
            var halfHeight = Lerp(height0, height1, t);
            if (halfHeight <= 0.02f)
            {
                continue;
            }

            var colorT = (x - leftBound) / Math.Max(1f, rightBound - leftBound);
            var color = SampleRainbow(colorT);

            if (waveGlowAlpha > 0f)
            {
                ds.DrawLine(x, midY - halfHeight, x, midY + halfHeight, WithOpacity(color, waveGlowAlpha), waveThickness + 1.4f);
            }

            ds.DrawLine(x, midY - halfHeight, x, midY + halfHeight, color, waveThickness);
        }

        for (var pixel = startPixel; pixel < endPixel; pixel++)
        {
            var x0 = Math.Clamp(pixel, leftBound, rightBound);
            var x1 = Math.Clamp(pixel + 1f, leftBound, rightBound);
            var colorT = ((x0 + x1) * 0.5f - leftBound) / Math.Max(1f, rightBound - leftBound);
            var color = MixColors(SampleRainbow(colorT), Color.FromArgb(255, 255, 255, 255), 0.16f);
            if (waveGlowAlpha > 0f)
            {
                ds.DrawLine(x0, midY, x1, midY, WithOpacity(color, Math.Min(0.85f, waveGlowAlpha + 0.10f)), centerLineThickness + 1.2f);
            }

            ds.DrawLine(x0, midY, x1, midY, color, centerLineThickness);
        }
    }

    private static float SmoothBand(float[] bands, int index)
    {
        var current = Math.Clamp(bands[index], 0f, 1f);
        var previous = index > 0 ? Math.Clamp(bands[index - 1], 0f, 1f) : current;
        var next = index + 1 < bands.Length ? Math.Clamp(bands[index + 1], 0f, 1f) : current;
        return (current * 0.58f) + (previous * 0.21f) + (next * 0.21f);
    }

    private static Color SampleRainbow(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var scaled = t * (RainbowStops.Length - 1);
        var leftIndex = Math.Clamp((int)MathF.Floor(scaled), 0, RainbowStops.Length - 1);
        var rightIndex = Math.Clamp(leftIndex + 1, 0, RainbowStops.Length - 1);
        var localT = scaled - leftIndex;
        return MixColors(RainbowStops[leftIndex], RainbowStops[rightIndex], localT);
    }

    private static Color MixColors(Color left, Color right, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (byte)Math.Clamp((int)MathF.Round(left.A + ((right.A - left.A) * t)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(left.R + ((right.R - left.R) * t)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(left.G + ((right.G - left.G) * t)), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(left.B + ((right.B - left.B) * t)), 0, 255));
    }

    private static Color WithOpacity(Color color, float alpha)
        => Color.FromArgb((byte)Math.Clamp((int)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f), 0, 255), color.R, color.G, color.B);

    private static float Lerp(float start, float end, float amount)
        => start + ((end - start) * Math.Clamp(amount, 0f, 1f));
}
