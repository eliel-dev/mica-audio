using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class MirrorRenderer : IRenderer
{
    public string RendererId => RendererIds.Mirror;

    public string DisplayName => "Mirror";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var midY = context.Height * 0.5f;
        var slot = context.Width / bands.Length;
        var width = slot * 0.84f;

        for (var i = 0; i < bands.Length; i++)
        {
            var value = Math.Clamp(bands[i], 0f, 1f);
            var halfHeight = value * midY;
            var x = i * slot + ((slot - width) * 0.5f);
            var color = context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1));

            ds.FillRectangle(x, midY - halfHeight, width, halfHeight, color);
            ds.FillRectangle(x, midY, width, halfHeight, context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1), 0.7f));
        }
    }
}
