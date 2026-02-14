using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class ReflectionRenderer : IRenderer
{
    public string RendererId => RendererIds.Reflection;

    public string DisplayName => "Reflection";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var slot = context.Width / bands.Length;
        var width = slot * 0.82f;
        var horizon = context.Height * 0.62f;

        for (var i = 0; i < bands.Length; i++)
        {
            var value = Math.Clamp(bands[i], 0f, 1f);
            var topHeight = value * horizon;
            var reflectionHeight = topHeight * 0.45f;
            var x = i * slot + ((slot - width) * 0.5f);
            var color = context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1));

            ds.FillRectangle(x, horizon - topHeight, width, topHeight, color);
            ds.FillRectangle(x, horizon, width, reflectionHeight, context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1), 0.35f));
        }
    }
}
