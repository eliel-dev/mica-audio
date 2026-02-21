using Microsoft.Graphics.Canvas;
using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal enum ClockWatchfaceStyle
{
    Pixel,
    Segment,
    Rounded,
}

internal static class ClockFontRenderer
{
    public static ClockWatchfaceStyle ResolveStyle(string? style)
    {
        if (string.Equals(style, "segment", StringComparison.OrdinalIgnoreCase))
        {
            return ClockWatchfaceStyle.Segment;
        }

        if (string.Equals(style, "rounded", StringComparison.OrdinalIgnoreCase))
        {
            return ClockWatchfaceStyle.Rounded;
        }

        return ClockWatchfaceStyle.Pixel;
    }

    public static Color ResolveColor(string? colorKey, bool selected)
    {
        var fallback = selected ? Color.FromArgb(255, 72, 255, 220) : Color.FromArgb(255, 74, 222, 255);
        return colorKey?.Trim().ToLowerInvariant() switch
        {
            "white" => Color.FromArgb(255, 255, 255, 255),
            "green" => Color.FromArgb(255, 70, 255, 135),
            "yellow" => Color.FromArgb(255, 255, 225, 85),
            "orange" => Color.FromArgb(255, 255, 153, 67),
            "magenta" => Color.FromArgb(255, 255, 95, 230),
            "cyan" => Color.FromArgb(255, 74, 222, 255),
            _ => fallback,
        };
    }

    public static void DrawTime(
        CanvasDrawingSession ds,
        float ox,
        float oy,
        float pitch,
        float ledSize,
        string text,
        ClockWatchfaceStyle style,
        Color color)
    {
        switch (style)
        {
            case ClockWatchfaceStyle.Segment:
                DrawSegmentClock(ds, ox, oy, pitch, ledSize, text, color, rounded: false);
                break;
            case ClockWatchfaceStyle.Rounded:
                DrawSegmentClock(ds, ox, oy, pitch, ledSize, text, color, rounded: true);
                break;
            default:
                DrawPixelClock(ds, ox, oy, pitch, ledSize, text, color);
                break;
        }
    }

    private static void DrawPixelClock(CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, string text, Color color)
    {
        var textWidth = (text.Length * 6) - 1;
        var startX = Math.Max(1, (Hub75PreviewHelper.PanelWidth - textWidth) / 2);
        Hub75PreviewHelper.DrawText5x7(ds, ox, oy, pitch, ledSize, startX, 10, text, color);
    }

    private static void DrawSegmentClock(CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, string text, Color color, bool rounded)
    {
        var widths = text.Select(ch => ch == ':' ? 4 : 9).ToArray();
        var totalWidth = widths.Sum() + Math.Max(0, widths.Length - 1);
        var x = Math.Max(1, (Hub75PreviewHelper.PanelWidth - totalWidth) / 2);

        foreach (var ch in text)
        {
            if (ch == ':')
            {
                DrawColon(ds, ox, oy, pitch, ledSize, x, rounded ? 11 : 10, color, rounded);
                x += 5;
                continue;
            }

            DrawDigit(ds, ox, oy, pitch, ledSize, x, rounded ? 8 : 7, ch, color, rounded);
            x += 10;
        }
    }

    private static void DrawColon(CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y, Color color, bool rounded)
    {
        DrawSegmentPixel(ds, ox, oy, pitch, ledSize, x + 1, y + 2, color, rounded);
        DrawSegmentPixel(ds, ox, oy, pitch, ledSize, x + 1, y + 6, color, rounded);
    }

    private static void DrawDigit(CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y, char digit, Color color, bool rounded)
    {
        if (!TryGetSegments(digit, out var segments))
        {
            return;
        }

        DrawHorizontalSegment(ds, ox, oy, pitch, ledSize, x + 1, y, segments[0], color, rounded);
        DrawVerticalSegment(ds, ox, oy, pitch, ledSize, x + 7, y + 1, segments[1], color, rounded);
        DrawVerticalSegment(ds, ox, oy, pitch, ledSize, x + 7, y + 6, segments[2], color, rounded);
        DrawHorizontalSegment(ds, ox, oy, pitch, ledSize, x + 1, y + 11, segments[3], color, rounded);
        DrawVerticalSegment(ds, ox, oy, pitch, ledSize, x, y + 6, segments[4], color, rounded);
        DrawVerticalSegment(ds, ox, oy, pitch, ledSize, x, y + 1, segments[5], color, rounded);
        DrawHorizontalSegment(ds, ox, oy, pitch, ledSize, x + 1, y + 5, segments[6], color, rounded);
    }

    private static bool TryGetSegments(char digit, out bool[] segments)
    {
        segments = digit switch
        {
            '0' => [true, true, true, true, true, true, false],
            '1' => [false, true, true, false, false, false, false],
            '2' => [true, true, false, true, true, false, true],
            '3' => [true, true, true, true, false, false, true],
            '4' => [false, true, true, false, false, true, true],
            '5' => [true, false, true, true, false, true, true],
            '6' => [true, false, true, true, true, true, true],
            '7' => [true, true, true, false, false, false, false],
            '8' => [true, true, true, true, true, true, true],
            '9' => [true, true, true, true, false, true, true],
            _ => [],
        };

        return segments.Length == 7;
    }

    private static void DrawHorizontalSegment(
        CanvasDrawingSession ds,
        float ox,
        float oy,
        float pitch,
        float ledSize,
        int startX,
        int y,
        bool isOn,
        Color color,
        bool rounded)
    {
        if (!isOn)
        {
            return;
        }

        for (var i = 0; i < 5; i++)
        {
            DrawSegmentPixel(ds, ox, oy, pitch, ledSize, startX + i, y, color, rounded);
        }
    }

    private static void DrawVerticalSegment(
        CanvasDrawingSession ds,
        float ox,
        float oy,
        float pitch,
        float ledSize,
        int x,
        int startY,
        bool isOn,
        Color color,
        bool rounded)
    {
        if (!isOn)
        {
            return;
        }

        for (var i = 0; i < 4; i++)
        {
            DrawSegmentPixel(ds, ox, oy, pitch, ledSize, x, startY + i, color, rounded);
        }
    }

    private static void DrawSegmentPixel(
        CanvasDrawingSession ds,
        float ox,
        float oy,
        float pitch,
        float ledSize,
        int x,
        int y,
        Color color,
        bool rounded)
    {
        if (rounded)
        {
            var glowColor = Color.FromArgb((byte)Math.Min(160, (int)color.A), color.R, color.G, color.B);
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, y, glowColor, glow: true);
            return;
        }

        Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, y, color, glow: false);
    }
}
