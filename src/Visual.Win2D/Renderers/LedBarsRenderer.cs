using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class LedBarsRenderer : IRenderer
{
    public string RendererId => RendererIds.LedBars;

    public string DisplayName => "LED Bars";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var segments = 24;
        var slot = context.Width / bands.Length;
        var width = slot * 0.78f;
        var segmentHeight = context.Height / segments;

        for (var i = 0; i < bands.Length; i++)
        {
            var lit = (int)MathF.Round(Math.Clamp(bands[i], 0f, 1f) * segments);
            var x = i * slot + ((slot - width) * 0.5f);

            for (var s = 0; s < segments; s++)
            {
                var y = context.Height - ((s + 1) * segmentHeight);
                var active = s < lit;
                var t = (s + 1) / (float)segments;
                var color = active
                    ? context.Palette.ColorAt(t)
                    : context.Palette.ColorAt(t, 0.12f);

                ds.FillRectangle(x, y + 1f, width, Math.Max(1f, segmentHeight - 2f), color);
            }
        }
    }
}
