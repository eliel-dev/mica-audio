using App.WinUI.Services.Apps;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

// DOCS: docs/wiki/modules/app-winui.md#integracoes-http-externas
// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal sealed class WeatherPreviewRenderer : IAppPreviewRenderer
{
    private static readonly WeatherPreviewSnapshot UnavailableSnapshot = new()
    {
        CityDisplay = WeatherAppFixedLocation.FixedCityDisplayName,
        State = WeatherPreviewLoadState.Error,
        FailureMessage = "Preview do clima indisponivel sem App.Services.",
    };

    public string Kind => "weather";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        Hub75PreviewHelper.DrawPanel(context, out var ox, out var oy, out var pitch, out var ledSize);

        var snapshot = ResolveSnapshot();
        var cityText = BuildCityLabel(snapshot.CityDisplay, context.Time);
        var temperatureText = snapshot.State == WeatherPreviewLoadState.Error
            ? "ERR"
            : snapshot.Temperature is double temperature
                ? $"{Math.Round(temperature):00}{snapshot.UnitSymbol}"
                : $"--{snapshot.UnitSymbol}";
        var temperatureColor = snapshot.State == WeatherPreviewLoadState.Error
            ? Color.FromArgb(255, 255, 126, 126)
            : Color.FromArgb(255, 255, 220, 140);
        var indicatorColor = snapshot.State switch
        {
            WeatherPreviewLoadState.Live => Color.FromArgb(255, 70, 255, 140),
            WeatherPreviewLoadState.Error => Color.FromArgb(255, 255, 92, 92),
            _ => Color.FromArgb(255, 255, 170, 90),
        };

        Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, 2, 2, cityText, Color.FromArgb(255, 145, 215, 255));
        DrawWeatherIcon(ds, ox, oy, pitch, ledSize, snapshot.WeatherCode ?? -1, 8, 12);
        Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, 34, 19, temperatureText, temperatureColor);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, 62, 1, indicatorColor, glow: false);
    }

    private static string BuildCityLabel(string cityDisplay, float time)
    {
        var city = cityDisplay.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        var normalized = Hub75PreviewHelper.NormalizeForMatrix(city);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "TIMBO";
        }

        const int maxChars = 10;
        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        var loop = normalized + "   " + normalized;
        var offset = (int)MathF.Floor((time * 2f) % (normalized.Length + 3));
        if (offset + maxChars > loop.Length)
        {
            offset = 0;
        }

        return loop.Substring(offset, maxChars);
    }

    private static void DrawWeatherIcon(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int weatherCode, int x, int y)
    {
        if (weatherCode < 0)
        {
            DrawCloud(ds, ox, oy, pitch, ledSize, x, y, stormy: false);
            return;
        }

        if (IsThunderstorm(weatherCode))
        {
            DrawCloud(ds, ox, oy, pitch, ledSize, x, y, stormy: true);
            DrawLightning(ds, ox, oy, pitch, ledSize, x + 6, y + 6);
            return;
        }

        if (IsRain(weatherCode))
        {
            DrawCloud(ds, ox, oy, pitch, ledSize, x, y, stormy: false);
            DrawRain(ds, ox, oy, pitch, ledSize, x + 1, y + 7);
            return;
        }

        if (IsSnow(weatherCode))
        {
            DrawCloud(ds, ox, oy, pitch, ledSize, x, y, stormy: false);
            DrawSnow(ds, ox, oy, pitch, ledSize, x + 2, y + 7);
            return;
        }

        if (IsCloudy(weatherCode))
        {
            DrawCloud(ds, ox, oy, pitch, ledSize, x, y, stormy: false);
            return;
        }

        DrawSun(ds, ox, oy, pitch, ledSize, x + 5, y + 4);
    }

    private static bool IsCloudy(int code) => code is 1 or 2 or 3 or 45 or 48;

    private static bool IsRain(int code) => code is 51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82;

    private static bool IsSnow(int code) => code is 71 or 73 or 75 or 77 or 85 or 86;

    private static bool IsThunderstorm(int code) => code is 95 or 96 or 99;

    private static void DrawSun(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int cx, int cy)
    {
        var sun = Color.FromArgb(255, 255, 210, 70);
        for (var px = -2; px <= 2; px++)
        {
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx + px, cy, sun);
        }

        for (var py = -2; py <= 2; py++)
        {
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx, cy + py, sun);
        }

        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx - 1, cy - 1, sun);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx + 1, cy - 1, sun);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx - 1, cy + 1, sun);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, cx + 1, cy + 1, sun);
    }

    private static void DrawCloud(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y, bool stormy)
    {
        var cloud = stormy ? Color.FromArgb(255, 140, 165, 195) : Color.FromArgb(255, 175, 216, 255);
        for (var px = 0; px < 14; px++)
        {
            for (var py = 0; py < 5; py++)
            {
                if ((py == 0 && (px < 2 || px > 11)) || (py == 1 && (px == 0 || px == 13)))
                {
                    continue;
                }

                Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + px, y + py, cloud);
            }
        }

        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 4, y - 1, cloud);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 8, y - 1, cloud);
    }

    private static void DrawRain(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y)
    {
        var rain = Color.FromArgb(255, 90, 220, 255);
        for (var i = 0; i < 5; i++)
        {
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + (i * 2), y + (i % 2), rain, glow: false);
        }
    }

    private static void DrawSnow(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y)
    {
        var snow = Color.FromArgb(255, 236, 244, 255);
        for (var i = 0; i < 4; i++)
        {
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + (i * 3), y + (i % 2), snow, glow: false);
        }
    }

    private static void DrawLightning(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y)
    {
        var lightning = Color.FromArgb(255, 255, 230, 120);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 2, y, lightning, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 1, y + 1, lightning, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 2, y + 2, lightning, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x + 1, y + 3, lightning, glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, y + 4, lightning, glow: false);
    }

    private static WeatherPreviewSnapshot ResolveSnapshot()
    {
        var dataService = global::App.WinUI.App.Services?.GetService<WeatherPreviewDataService>();
        return dataService?.GetSnapshot() ?? UnavailableSnapshot;
    }
}
