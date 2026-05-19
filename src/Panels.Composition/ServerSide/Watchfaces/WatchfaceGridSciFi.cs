namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawGridSciFi(in Ctx c)
    {
        // Near-black space background
        FillGridNoise(c, Rgb(0, 0, 8), Rgb(1, 5, 20), 9);

        var vpX = 64;   // vanishing point X
        var vpY = 30;   // vanishing point Y (upper area)
        var gridLine   = Rgb(16, 72, 160);
        var gridBright = Rgb(38, 200, 240);
        var gridMid    = Rgb(22, 110, 200);

        // Outer border
        c.Rect(0, 0, 128, 64, gridLine);

        // Corner-to-vanishingpoint diagonal lines
        c.Line(0,   0,  vpX, vpY, gridLine);
        c.Line(127, 0,  vpX, vpY, gridLine);
        c.Line(0,   63, vpX, vpY, gridLine);
        c.Line(127, 63, vpX, vpY, gridLine);

        // Vertical fan lines (left half then right half)
        for (var i = 0; i <= 9; i++)
        {
            var x = i * 14;
            var bright = (i % 3 == 0);
            c.Line(vpX, vpY, x,       0,  bright ? gridMid : gridLine);
            c.Line(vpX, vpY, x,       63, bright ? gridMid : gridLine);
            c.Line(vpX, vpY, 127 - x, 0,  bright ? gridMid : gridLine);
            c.Line(vpX, vpY, 127 - x, 63, bright ? gridMid : gridLine);
        }

        // Horizontal rows — upper and lower, converging toward VP
        int[] hRows = [2, 9, 18, 29, 40, 52, 61];
        foreach (var row in hRows)
        {
            var isHorizon = row == 29;
            c.HLine(0, row, 128, isHorizon ? gridBright : gridLine);
        }

        // Animated horizontal sweep line
        var drift   = c.Tick / 180 % 4;
        var sweepY  = 30 + drift;
        c.HLine(0, sweepY, 128, Rgb(12, 110, 185));
        var dotPhase = c.Tick / 22 % 128;
        for (var x = 0; x < 128; x += 12)
        {
            var dotX = (x + dotPhase) % 128;
            c.Px(dotX, sweepY - 1, gridBright);
            c.Px(dotX, sweepY + 1, gridBright);
        }

        // Dark backdrop for time text
        var time  = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", System.Globalization.CultureInfo.InvariantCulture);
        var tx    = CenterX(time, 4, 2);
        var tw    = DotTextWidth(time, 4);
        c.FillRect(tx - 4, 8, tw + 8, 22, Rgb(0, 2, 12));
        c.Rect(tx - 5, 7, tw + 10, 24, gridLine);

        // Large cyan time
        DrawDotText(c, tx, 11, time, gridBright, 4, 2, glow: true);

        // Footer date with >> <<
        var footer = $">> {DayPt(c.Now.DayOfWeek)} {c.Now:dd/MM/yyyy} <<";
        c.Text((128 - TextWidth(footer)) / 2, 55, footer, Rgb(46, 188, 220));
    }
}
