using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class BarsRenderer : IRenderer
{
    public string RendererId => RendererIds.Bars;

    public string DisplayName => "Bars";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var slot = context.Width / bands.Length;
        var barFactor = Math.Clamp(context.Param("barWidth", 0.84f), 0.2f, 1f);
        var width = slot * barFactor;

        for (var i = 0; i < bands.Length; i++)
        {
            var value = Math.Clamp(bands[i], 0f, 1f);
            var h = value * context.Height;
            var x = i * slot + ((slot - width) * 0.5f);
            var y = context.Height - h;
            var color = context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1));

            if (context.Preset.EnableGlow)
            {
                ds.FillRectangle(x - 1f, y, width + 2f, h, context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1), 0.25f));
            }

            ds.FillRectangle(x, y, width, h, color);
        }
    }
}
