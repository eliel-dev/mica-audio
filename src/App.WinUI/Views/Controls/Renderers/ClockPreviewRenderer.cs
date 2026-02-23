using System.Globalization;
using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-relogio
internal sealed class ClockPreviewRenderer : IAppPreviewRenderer
{
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    public string Kind => "clock";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        Hub75PreviewHelper.DrawPanel(context, out var ox, out var oy, out var pitch, out var ledSize);

        var use24h = !bool.TryParse(context.GetConfigValue("format24h"), out var parsedFormat24) || parsedFormat24;
        var watchface = ClockFontRenderer.ResolveStyle(context.GetConfigValue("watchfaceStyle"));
        var mainColor = ClockFontRenderer.ResolveColor(context.GetConfigValue("fontColor"), context.IsSelected);

        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, BrasiliaTimeZone).DateTime;
        var showColon = (now.Second % 2) == 0;
        var format = use24h ? "HH:mm" : "hh:mm";
        var timeText = now.ToString(showColon ? format : format.Replace(':', ' '), CultureInfo.InvariantCulture);

        ClockFontRenderer.DrawTime(ds, ox, oy, pitch, ledSize, timeText, watchface, mainColor);

        if (!use24h)
        {
            var period = now.ToString("tt", CultureInfo.InvariantCulture).ToUpperInvariant();
            Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, 51, 2, period, Color.FromArgb(255, 192, 204, 228));
        }

        Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, 2, 24, "BRT", Color.FromArgb(255, 150, 185, 225));

        var sec = now.Second;
        var progress = (int)Math.Round(((sec + 1) / 60f) * (Hub75PreviewHelper.PanelWidth - 4));
        for (var x = 2; x < 2 + progress; x++)
        {
            var hue = (x - 2) / (float)Math.Max(1, Hub75PreviewHelper.PanelWidth - 4);
            var color = AppPreviewDrawHelpers.RainbowByFraction(hue);
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, 30, color, glow: false);
        }
    }

    private static TimeZoneInfo ResolveBrasiliaTimeZone()
    {
        var candidates = new[]
        {
            "America/Sao_Paulo",
            "E. South America Standard Time",
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(candidate, out var windowsId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }

                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch
            {
                // tenta o próximo candidato
            }
        }

        return TimeZoneInfo.Local;
    }
}
