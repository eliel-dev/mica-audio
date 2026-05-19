namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawRetroAmbar(in Ctx c)
    {
        // Dark warm background
        FillGridNoise(c, Rgb(8, 4, 0), Rgb(15, 7, 0), 8);
        ApplyMatrixTexture(c, Rgb(38, 18, 0), 6);

        // Double outer border
        c.Rect(0, 0, 128, 64, Rgb(140, 78, 12));
        c.Rect(2, 2, 124, 60, AmberDim);
        c.Rect(3, 3, 122, 58, Scale(AmberDim, 0.55f));

        // Dashed top decoration line
        for (var x = 7; x < 121; x += 3)
        {
            c.Px(x, 7, Amber);
            c.Px(x, 56, Rgb(100, 56, 8));
        }

        // Corner cross marks
        c.Px(6, 6, Amber); c.Px(121, 6,  Amber);
        c.Px(6, 57, Rgb(100, 56, 8)); c.Px(121, 57, Rgb(100, 56, 8));

        // Vertical side bars (left and right indicators)
        for (var y = 18; y < 44; y += 4)
        {
            c.HLine(4,  y, 3, Amber);
            c.HLine(4,  y + 1, 3, Scale(Amber, 0.5f));
            c.HLine(121, y, 3, Amber);
            c.HLine(121, y + 1, 3, Scale(Amber, 0.5f));
        }

        // Large pulsing amber time — alternates between two brightness levels
        var bright = (c.Tick / 180 % 2) == 0;
        var timeColor = bright ? Amber : Rgb(230, 144, 20);
        var time = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", System.Globalization.CultureInfo.InvariantCulture);
        DrawDotText(c, CenterX(time, 5, 3), 16, time, timeColor, 5, 3, glow: true);

        // Date centered
        var date = $"{DayPt(c.Now.DayOfWeek)} {c.Now:dd/MM/yyyy}";
        c.Text((128 - TextWidth(date)) / 2, 47, date, Amber);

        // Pulsing indicator row at bottom
        var phase = c.Tick / 100 % 6;
        for (var i = 0; i < 6; i++)
            c.FillRect(45 + i * 7, 57, 3, 3, i == phase ? Amber : AmberDim);
    }
}
