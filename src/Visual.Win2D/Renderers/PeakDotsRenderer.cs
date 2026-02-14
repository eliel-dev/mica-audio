using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class PeakDotsRenderer : IRenderer
{
    public string RendererId => RendererIds.Peaks;

    public string DisplayName => "Peak Dots";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var slot = context.Width / bands.Length;
        var width = slot * 0.76f;

        for (var i = 0; i < bands.Length; i++)
        {
            var value = Math.Clamp(bands[i], 0f, 1f);
            var peak = i < context.Peaks.Length ? Math.Clamp(context.Peaks[i], 0f, 1f) : value;
            var h = value * context.Height;
            var x = i * slot + ((slot - width) * 0.5f);
            var y = context.Height - h;
            var peakY = context.Height - (peak * context.Height);
            var color = context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1));

            ds.FillRectangle(x, y, width, h, context.Palette.ColorAt(i / (float)Math.Max(1, bands.Length - 1), 0.65f));
            ds.FillCircle(x + (width * 0.5f), peakY, Math.Max(1.8f, width * 0.32f), color);
        }
    }
}
