using System.Globalization;
using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal sealed class ClockPreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "clock";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        Hub75PreviewHelper.DrawPanel(context, out var ox, out var oy, out var pitch, out var ledSize);

        var timezoneId = context.GetConfigValue("timezone");
        var use24h = !bool.TryParse(context.GetConfigValue("format24h"), out var parsedFormat24) || parsedFormat24;

        var now = GetClockTime(timezoneId);
        var showColon = (now.Second % 2) == 0;
        var format = use24h ? "HH:mm" : "hh:mm";
        var timeText = now.ToString(showColon ? format : format.Replace(':', ' '), CultureInfo.InvariantCulture);

        var textWidth = (timeText.Length * 6) - 1;
        var startX = Math.Max(1, (Hub75PreviewHelper.PanelWidth - textWidth) / 2);
        var mainColor = context.IsSelected ? Color.FromArgb(255, 42, 241, 200) : Color.FromArgb(255, 74, 222, 255);
        Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, startX, 9, timeText, mainColor);

        var zoneLabel = BuildTimezoneLabel(timezoneId);
        if (!string.IsNullOrWhiteSpace(zoneLabel))
        {
            ds.DrawText(zoneLabel, ox + 4f, oy + (Hub75PreviewHelper.PanelHeight - 9f) * pitch, Color.FromArgb(235, 182, 198, 220));
        }

        var sec = now.Second;
        var progress = (int)Math.Round(((sec + 1) / 60f) * (Hub75PreviewHelper.PanelWidth - 4));
        for (var x = 2; x < 2 + progress; x++)
        {
            var hue = (x - 2) / (float)Math.Max(1, Hub75PreviewHelper.PanelWidth - 4);
            var color = AppPreviewDrawHelpers.RainbowByFraction(hue);
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, 28, color, glow: false);
        }

        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, 1, 1, Color.FromArgb(255, 255, 80, 80), glow: false);
        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, 62, 1, Color.FromArgb(255, 80, 255, 120), glow: false);
    }

    private static DateTime GetClockTime(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return DateTime.Now;
        }

        try
        {
            var tz = ResolveTimeZone(timezoneId);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timezoneId)
    {
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timezoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }

        return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
    }

    private static string BuildTimezoneLabel(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return string.Empty;
        }

        var segment = timezoneId.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? timezoneId;
        return segment.Replace('_', ' ');
    }
}
