using Microsoft.Graphics.Canvas.Geometry;
using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class AreaFillRenderer : IRenderer
{
    public string RendererId => RendererIds.Area;

    public string DisplayName => "Area Fill";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length < 2)
        {
            return;
        }

        var step = context.Width / (bands.Length - 1f);
        using var path = new CanvasPathBuilder(ds);
        path.BeginFigure(0f, context.Height);
        for (var i = 0; i < bands.Length; i++)
        {
            var x = i * step;
            var y = context.Height - (Math.Clamp(bands[i], 0f, 1f) * context.Height);
            path.AddLine(x, y);
        }

        path.AddLine(context.Width, context.Height);
        path.EndFigure(CanvasFigureLoop.Closed);

        using var geometry = CanvasGeometry.CreatePath(path);
        ds.FillGeometry(geometry, context.Palette.ColorAt(0.55f, 0.45f));

        for (var i = 1; i < bands.Length; i++)
        {
            var x0 = (i - 1) * step;
            var y0 = context.Height - (Math.Clamp(bands[i - 1], 0f, 1f) * context.Height);
            var x1 = i * step;
            var y1 = context.Height - (Math.Clamp(bands[i], 0f, 1f) * context.Height);
            ds.DrawLine(x0, y0, x1, y1, context.Palette.ColorAt(i / (float)(bands.Length - 1)), 1.5f);
        }
    }
}
