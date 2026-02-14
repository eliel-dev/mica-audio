using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class CenteredBarsRenderer : IRenderer
{
    public string RendererId => RendererIds.CenterBars;

    public string DisplayName => "Centered Bars";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        var slot = context.Width / bands.Length;
        var barFactor = Math.Clamp(context.Param("barWidth", 0.84f), 0.02f, 1f);
        var gapFactor = Math.Clamp(context.Param("gap", 0.16f), 0f, 0.95f);
        var widthByBarFactor = slot * barFactor;
        var widthByGap = slot * (1f - gapFactor);
        var width = Math.Clamp(MathF.Min(widthByBarFactor, widthByGap), 1f, slot);
        var lineMode = context.Param("lineMode", 0f) >= 0.5f;
        var lineThickness = Math.Clamp(context.Param("lineThickness", width), 1f, Math.Max(1f, width));
        var heightScale = Math.Clamp(context.Param("heightScale", 1f), 0.1f, 1f);
        var minHalfHeight = Math.Clamp(context.Param("minHalfHeight", 0f), 0f, context.Height * 0.5f);
        var midY = context.Height * 0.5f;
        var maxHalfHeight = midY * heightScale;

        for (var i = 0; i < bands.Length; i++)
        {
            var value = Math.Clamp(bands[i], 0f, 1f);
            var halfHeight = minHalfHeight + (value * Math.Max(0f, maxHalfHeight - minHalfHeight));
            var x = i * slot + ((slot - width) * 0.5f);
            var centerX = x + (width * 0.5f);
            var yTop = midY - halfHeight;
            var yBottom = midY + halfHeight;
            var t = i / (float)Math.Max(1, bands.Length - 1);
            var color = context.Palette.ColorAt(t);

            if (context.Preset.EnableGlow)
            {
                var glow = context.Palette.ColorAt(t, 0.25f);
                if (lineMode)
                {
                    ds.DrawLine(centerX, yTop, centerX, yBottom, glow, lineThickness + 2f);
                }
                else
                {
                    ds.FillRectangle(x - 1f, yTop, width + 2f, halfHeight, glow);
                    ds.FillRectangle(x - 1f, midY, width + 2f, halfHeight, glow);
                }
            }

            if (lineMode)
            {
                ds.DrawLine(centerX, yTop, centerX, yBottom, color, lineThickness);
            }
            else
            {
                ds.FillRectangle(x, yTop, width, halfHeight, color);
                ds.FillRectangle(x, midY, width, halfHeight, color);
            }
        }
    }
}
