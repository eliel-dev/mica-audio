using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class LineRenderer : IRenderer
{
    public string RendererId => RendererIds.Line;

    public string DisplayName => "Line";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length < 2)
        {
            return;
        }

        var thickness = Math.Clamp(context.Param("lineThickness", 2f), 1f, 8f);
        var step = context.Width / (bands.Length - 1f);

        for (var i = 1; i < bands.Length; i++)
        {
            var x0 = (i - 1) * step;
            var y0 = context.Height - (Math.Clamp(bands[i - 1], 0f, 1f) * context.Height);
            var x1 = i * step;
            var y1 = context.Height - (Math.Clamp(bands[i], 0f, 1f) * context.Height);
            var color = context.Palette.ColorAt(i / (float)(bands.Length - 1));

            ds.DrawLine(x0, y0, x1, y1, color, thickness);
        }
    }
}
