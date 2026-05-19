namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawRelogioChuva(in Ctx c)
    {
        // Deep dark-blue night sky
        FillGradient(c, Rgb(3, 8, 22), Rgb(4, 11, 30));
        ApplyMatrixTexture(c, Rgb(8, 20, 40), 6);

        var horizon = 47;

        // City buildings (dark silhouettes in background)
        DrawRainCity(c, horizon);

        // Water surface below horizon
        for (var y = horizon; y < VirtualHeight; y++)
        {
            var t = (y - horizon) / (float)(VirtualHeight - horizon);
            c.HLine(0, y, VirtualWidth, Lerp(Rgb(5, 12, 32), Rgb(2, 5, 14), t));
        }

        // Street lamp with cone of light
        DrawStreetLamp(c, 14, 22);

        // Rain (drawn before time so it appears behind the text glow)
        DrawRain(c, horizon);

        // Large white time
        var time = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", System.Globalization.CultureInfo.InvariantCulture);
        DrawDotText(c, CenterX(time, 4, 2), 13, time, White, 4, 2, glow: false);

        // Date bottom-left
        var date = $"{DayPt(c.Now.DayOfWeek)} {c.Now:dd/MM/yyyy}";
        c.Text(4, 55, date, Rgb(190, 210, 230));

        // Umbrella bottom-right
        DrawUmbrella(c, 107, 53);
    }

    private static void DrawRainCity(in Ctx c, int horizon)
    {
        int[] heights = [8, 13, 10, 16, 9, 18, 11, 14, 7, 16, 12, 9, 14, 10, 18, 8, 12, 15];
        var bw = 7;
        for (var i = 0; i < heights.Length; i++)
        {
            var x = i * bw;
            var bh = heights[i];
            var y = horizon - bh;
            // Building body
            c.FillRect(x, y, bw - 1, bh + 18, Rgb(2, 5, 14));
            // Top accent
            c.HLine(x, y, bw - 1, Rgb(6, 16, 36));
            // Occasional dim window
            for (var wy = y + 2; wy < horizon - 2; wy += 4)
            for (var wx = x + 1; wx < x + bw - 2; wx += 3)
                if (((wx * 5 + wy * 7 + i * 3) % 7) < 2)
                    c.Px(wx, wy, Rgb(8, 30, 68));
        }
    }

    private static void DrawStreetLamp(in Ctx c, int x, int topY)
    {
        var groundY = topY + 32;
        // Pole
        c.VLine(x, topY, groundY - topY, Rgb(68, 72, 76));
        // Arm
        c.HLine(x, topY, 12, Rgb(68, 72, 76));
        // Lamp head (bright yellow)
        c.Disc(x + 12, topY, 2, Rgb(255, 240, 140));
        c.MaxPx(x + 12, topY, Rgb(255, 255, 200));

        // Light cone (triangle of dimming light)
        for (var dy = 1; dy <= 22; dy++)
        {
            var half = (dy * 3) / 4;
            var brightness = Math.Max(0, 90 - dy * 3);
            if (brightness <= 0) break;
            var lc = Rgb(brightness / 2, brightness / 2 + 10, brightness / 6);
            c.HLine(x + 12 - half, topY + dy, half * 2 + 1, lc);
        }
    }

    private static void DrawRain(in Ctx c, int horizon)
    {
        for (var i = 0; i < 60; i++)
        {
            // Rain moves diagonally down-right
            var bx = (i * 23 + c.Tick / 14) % 134 - 3;
            var by = (i * 13 + c.Tick / 9)  % horizon;
            c.Px(bx,     by,     Rgb(110, 152, 210));
            c.Px(bx - 1, by + 1, Rgb(70,  110, 170));
            c.Px(bx - 2, by + 2, Rgb(40,  72,  130));
        }
    }

    private static void DrawUmbrella(in Ctx c, int x, int y)
    {
        var col = Rgb(180, 210, 230);
        c.HLine(x,     y,     13, col);
        c.HLine(x + 1, y - 1, 11, col);
        c.HLine(x + 3, y - 2,  7, col);
        c.HLine(x + 4, y - 3,  5, col);
        c.VLine(x + 6, y,       7, col);
        c.Px(x + 5, y + 7, col);
        c.Px(x + 4, y + 7, col);
        c.Px(x + 3, y + 6, col);
    }
}
