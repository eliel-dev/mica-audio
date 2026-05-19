namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawMonocromatico(in Ctx c)
    {
        // Very dark near-black with subtle gray noise
        FillGridNoise(c, Rgb(0, 0, 0), Rgb(6, 7, 7), 10);
        ApplyMatrixTexture(c, Rgb(20, 20, 22), 7);

        // Stars (monochrome, sparse)
        DrawStarfield(c, 28, colorful: false);

        // Crescent moon — upper left
        DrawCrescent(c, cx: 15, cy: 13, r: 8);

        // Large dome / hemisphere in lower center
        DrawMonoDome(c);

        // Animated shimmer reflection below the dome
        DrawMonoReflection(c);

        // Horizon line
        c.HLine(0, 50, 128, Rgb(100, 104, 108));
        c.HLine(0, 51, 128, Rgb(36, 38, 42));

        // Large white time — upper-center area
        var time = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", System.Globalization.CultureInfo.InvariantCulture);
        DrawDotText(c, CenterX(time, 4, 2), 27, time, White, 4, 2, glow: false);
    }

    private static void DrawCrescent(in Ctx c, int cx, int cy, int r)
    {
        // Full disc then subtract offset disc to get crescent
        c.Disc(cx, cy, r, Rgb(155, 158, 162));
        c.Disc(cx + 4, cy - 3, r - 1, Black);
        c.Circle(cx, cy, r, Rgb(220, 222, 225));
        // Inner crescent glow
        c.Circle(cx - 1, cy, r - 1, Scale(Rgb(200, 202, 205), 0.35f));
    }

    private static void DrawMonoDome(in Ctx c)
    {
        const int cx = 68;
        const int cy = 70;   // center is below the visible area so only the top arc shows
        const int r  = 34;

        for (var dy = -r; dy <= 0; dy++)
        for (var dx = -r; dx <= r; dx++)
        {
            if ((dx * dx) + (dy * dy) > r * r) continue;
            var dist  = MathF.Sqrt(dx * dx + dy * dy) / r;
            var shine = Math.Clamp((float)(-dy) / r, 0f, 1f);  // brighter toward top
            var t     = dist * (1f - shine * 0.4f);
            c.MaxPx(cx + dx, cy + dy, Lerp(Rgb(20, 20, 22), Rgb(114, 118, 124), t));
        }

        // Rim outline
        for (var angle = 180; angle <= 360; angle += 2)
        {
            var rad = angle * Math.PI / 180.0;
            c.Px(cx + (int)Math.Round(Math.Cos(rad) * r),
                 cy + (int)Math.Round(Math.Sin(rad) * r),
                 Rgb(220, 224, 228));
        }
    }

    private static void DrawMonoReflection(in Ctx c)
    {
        var shimmer = c.Tick / 100 % 5;
        for (var y = 52; y < 64; y++)
        {
            var half = Math.Max(1, 22 - (y - 52) * 2);
            var x0   = 68 - half / 2 + ((y + shimmer) % 3) - 1;
            var grayVal = (byte)Math.Clamp(80 - (y - 52) * 5, 20, 80);
            c.HLine(x0, y, half, Rgb(grayVal, grayVal + 2, grayVal + 4));
        }
    }
}
