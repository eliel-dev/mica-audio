using System.Numerics;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class PulseBackgroundRenderer : IRenderer
{
    public string RendererId => RendererIds.Pulse;

    public string DisplayName => "Pulse Background";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        var lowBandCount = Math.Max(1, bands.Length / 8);
        var lowSum = 0f;
        for (var i = 0; i < lowBandCount; i++)
        {
            lowSum += bands[i];
        }

        var lowEnergy = lowSum / lowBandCount;
        var pulse = Math.Clamp((context.Frame.Level * 0.65f) + (lowEnergy * 0.35f), 0f, 1f);
        var center = new Vector2(context.Width * 0.5f, context.Height * 0.5f);
        var baseRadius = MathF.Min(context.Width, context.Height) * 0.15f;
        var pulseRadius = baseRadius + (pulse * MathF.Min(context.Width, context.Height) * 0.35f);

        ds.FillCircle(center, pulseRadius, context.Palette.ColorAt(0.4f, 0.22f));
        ds.FillCircle(center, pulseRadius * 0.62f, context.Palette.ColorAt(0.85f, 0.28f));

        var step = context.Width / Math.Max(1, bands.Length - 1);
        for (var i = 1; i < bands.Length; i++)
        {
            var x0 = (i - 1) * step;
            var y0 = context.Height - (Math.Clamp(bands[i - 1], 0f, 1f) * context.Height * 0.45f) - (context.Height * 0.1f);
            var x1 = i * step;
            var y1 = context.Height - (Math.Clamp(bands[i], 0f, 1f) * context.Height * 0.45f) - (context.Height * 0.1f);
            ds.DrawLine(x0, y0, x1, y1, context.Palette.ColorAt(i / (float)(bands.Length - 1)), 2f);
        }
    }
}
