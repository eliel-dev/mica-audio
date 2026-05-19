using System.Globalization;

namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawFlipClock(in Ctx c)
    {
        // Dark warm background with subtle amber noise
        FillGradient(c, Rgb(8, 4, 0), Rgb(16, 7, 0));
        ApplyMatrixTexture(c, Rgb(32, 14, 0), 6);

        // Two flip panels: hours (left) and minutes (right)
        DrawFlipPanel(c,  8, 7, 53, 40);
        DrawFlipPanel(c, 67, 7, 53, 40);

        // Large amber digits
        var hour   = c.Now.ToString(c.Use24Hour ? "HH" : "hh", CultureInfo.InvariantCulture);
        var minute = c.Now.ToString("mm", CultureInfo.InvariantCulture);
        DrawDotText(c, 16, 14, hour,   Amber, 4, 3, glow: true);
        DrawDotText(c, 75, 14, minute, Amber, 4, 3, glow: true);

        // Horizontal mid-line that "flips" — animates during second transition
        var flipOffset = c.Tick < 400 ? c.Tick / 66 : 0;   // 0..5 in first 400ms, then stable
        c.HLine(10, 27 + flipOffset, 49, Rgb(88, 40, 4));
        c.HLine(69, 27 + flipOffset, 49, Rgb(88, 40, 4));

        // Subtle horizontal scan shimmer across each panel
        var shimmerY = 8 + (c.Tick / 100 % 38);
        c.HLine(10, shimmerY,     49, Rgb(60, 25, 2));
        c.HLine(69, shimmerY,     49, Rgb(60, 25, 2));

        // Colon dots between panels (pulsing)
        var colonBright = (c.Tick / 500 % 2) == 0;
        var colonColor  = colonBright ? Amber : Rgb(180, 100, 14);
        c.FillRect(61, 22, 4, 4, colonColor);
        c.FillRect(61, 32, 4, 4, colonColor);

        // Row of rivet dots along top and bottom of each panel
        for (var x = 11; x < 58; x += 5)
        {
            c.Px(x, 8,  Rgb(76, 40, 6));
            c.Px(x, 45, Rgb(76, 40, 6));
            c.Px(x + 59, 8,  Rgb(76, 40, 6));
            c.Px(x + 59, 45, Rgb(76, 40, 6));
        }

        // Date bar at bottom
        var date = $"{DayPt(c.Now.DayOfWeek)} {c.Now:dd} {MonthEn(c.Now.Month)} {c.Now:yyyy}";
        var dw   = TextWidth(date);
        var bx   = (128 - dw) / 2 - 3;
        c.FillRect(bx, 52, dw + 6, 10, Rgb(14, 6, 0));
        c.Rect(bx, 52, dw + 6, 10, Rgb(90, 50, 8));
        c.Text(bx + 3, 54, date, Amber);
    }

    private static void DrawFlipPanel(in Ctx c, int x, int y, int w, int h)
    {
        // Panel body (dark interior)
        c.FillRect(x + 2, y + 2, w - 4, h - 4, Rgb(15, 7, 0));
        // Outer frame
        c.Rect(x, y, w, h, Rgb(130, 68, 10));
        // Inner bevel
        c.Rect(x + 2, y + 2, w - 4, h - 4, Rgb(68, 34, 4));
        // Mute corner pixels
        c.Px(x + 1, y + 1, Black); c.Px(x + w - 2, y + 1, Black);
        c.Px(x + 1, y + h - 2, Black); c.Px(x + w - 2, y + h - 2, Black);
        // Inner highlight (subtle top edge glow)
        c.HLine(x + 3, y + 3, w - 6, Rgb(28, 14, 1));
    }
}
