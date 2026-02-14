using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class NeonGlowBarsRenderer : IRenderer
{
    public string RendererId => RendererIds.NeonGlow;

    public string DisplayName => "Neon Glow Bars";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var slot = context.Width / bands.Length;
        var width = slot * 0.64f;

        for (var i = 0; i < bands.Length; i++)
        {
            var value = Math.Clamp(bands[i], 0f, 1f);
            var h = value * context.Height;
            var x = i * slot + ((slot - width) * 0.5f);
            var y = context.Height - h;
            var t = i / (float)Math.Max(1, bands.Length - 1);

            ds.FillRectangle(x - 2f, y - 1f, width + 4f, h + 2f, context.Palette.ColorAt(t, 0.25f));
            ds.FillRectangle(x, y, width, h, context.Palette.ColorAt(t, 0.92f));
        }
    }
}
