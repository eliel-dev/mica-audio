using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal sealed class GifPreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "gif";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        Hub75PreviewHelper.DrawPanel(context, out var ox, out var oy, out var pitch, out var ledSize);

        for (var y = 0; y < Hub75PreviewHelper.PanelHeight; y++)
        {
            for (var x = 0; x < Hub75PreviewHelper.PanelWidth; x++)
            {
                var t = context.Time * 1.8f;
                var wave = MathF.Sin((x * 0.25f) + t) * MathF.Cos((y * 0.22f) - (t * 0.7f));
                var glow = Math.Clamp((wave + 1f) * 0.5f, 0f, 1f);
                if (glow < 0.52f)
                {
                    continue;
                }

                var r = (byte)Math.Clamp(30f + (225f * glow), 0f, 255f);
                var g = (byte)Math.Clamp(15f + (190f * (1f - MathF.Abs((x / 63f) - 0.5f) * 2f)), 0f, 255f);
                var b = (byte)Math.Clamp(40f + (210f * (y / 31f)), 0f, 255f);
                Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, y, Color.FromArgb(220, r, g, b), glow: false);
            }
        }

        Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, 3, 12, "GIF APP", Color.FromArgb(255, 255, 255, 255));
    }
}
