using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal sealed class WeatherPreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "weather";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        Hub75PreviewHelper.DrawPanel(context, out var ox, out var oy, out var pitch, out var ledSize);

        var cityRaw = context.GetConfigValue("city");
        var city = ExtractCityName(cityRaw);
        var units = context.GetConfigValue("units");
        var symbol = string.Equals(units, "imperial", StringComparison.OrdinalIgnoreCase) ? "F" : "C";

        var wobble = (int)Math.Round(MathF.Sin(context.Time * 0.8f));
        DrawSun(ds, ox, oy, pitch, ledSize, 12 + wobble, 9);
        DrawCloud(ds, ox, oy, pitch, ledSize, 8 - wobble, 14);

        var tempBase = string.Equals(symbol, "F", StringComparison.OrdinalIgnoreCase) ? 73 : 23;
        var temp = tempBase + (int)Math.Round(MathF.Sin(context.Time * 0.55f) * 3f);
        var tempText = $"{temp:00}{symbol}";
        Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, 36, 11, tempText, Color.FromArgb(255, 255, 220, 140));

        var cityLabel = city.Length > 14 ? city[..14] : city;
        ds.DrawText(cityLabel, ox + 4f, oy + (Hub75PreviewHelper.PanelHeight - 9f) * pitch, Color.FromArgb(235, 120, 220, 255));

        if (MathF.Sin(context.Time * 1.3f) > 0.35f)
        {
            DrawRain(ds, ox, oy, pitch, ledSize, 20, 22);
        }
    }

    private static string ExtractCityName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "São Paulo";
        }

        var beforePipe = raw.Split('|', 2, StringSplitOptions.TrimEntries)[0];
        var city = beforePipe.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        return string.IsNullOrWhiteSpace(city) ? "São Paulo" : city;
    }

    private static void DrawSun(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int cx, int cy)
    {
        var sun = Color.FromArgb(255, 255, 205, 60);
        var rays = Color.FromArgb(255, 255, 160, 40);

        var sunPixels = new (int x, int y)[]
        {
            (0,-2),(-1,-1),(0,-1),(1,-1),(-2,0),(-1,0),(0,0),(1,0),(2,0),(-1,1),(0,1),(1,1),(0,2),
        };

        foreach (var (x, y) in sunPixels)
        {
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx + x, cy + y, sun);
        }

        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx, cy - 4, rays, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx, cy + 4, rays, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx - 4, cy, rays, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx + 4, cy, rays, glow: false);
    }

    private static void DrawCloud(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y)
    {
        var cloud = Color.FromArgb(255, 170, 210, 255);

        for (var px = 0; px < 15; px++)
        {
            for (var py = 0; py < 6; py++)
            {
                if ((py < 2 && (px < 2 || px > 12)) || (py == 0 && (px < 4 || px > 10)))
                {
                    continue;
                }

                Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + px, y + py, cloud);
            }
        }

        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 4, y - 1, cloud);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 8, y - 2, cloud);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 11, y - 1, cloud);
    }

    private static void DrawRain(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y)
    {
        var rain = Color.FromArgb(255, 80, 220, 255);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, y, rain, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 3, y + 1, rain, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 6, y, rain, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 9, y + 1, rain, glow: false);
    }
}
