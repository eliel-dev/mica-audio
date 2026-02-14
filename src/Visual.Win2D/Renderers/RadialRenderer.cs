using System.Numerics;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class RadialRenderer : IRenderer
{
    public string RendererId => RendererIds.Radial;

    public string DisplayName => "Radial";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length < 2)
        {
            return;
        }

        var center = new Vector2(context.Width * 0.5f, context.Height * 0.5f);
        var innerRadius = MathF.Min(context.Width, context.Height) * 0.19f;
        var outerRadius = MathF.Min(context.Width, context.Height) * 0.44f;

        for (var i = 0; i < bands.Length; i++)
        {
            var t = i / (float)bands.Length;
            var angle = (MathF.PI * 2f * t) - (MathF.PI * 0.5f);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var value = Math.Clamp(bands[i], 0f, 1f);
            var start = center + (dir * innerRadius);
            var end = center + (dir * (innerRadius + ((outerRadius - innerRadius) * value)));
            var color = context.Palette.ColorAt(t);
            ds.DrawLine(start, end, color, 2f);
        }

        ds.DrawCircle(center, innerRadius, context.Palette.ColorAt(0.5f, 0.35f), 2f);
    }
}
