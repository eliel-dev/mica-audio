namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawNeoTokyo(in Ctx c)
    {
        // Deep purple-blue night sky
        FillGradient(c, Rgb(6, 2, 28), Rgb(4, 0, 16));
        ApplyMatrixTexture(c, Rgb(14, 8, 50), 6);

        // Stars in upper sky
        DrawStarfield(c, 50, colorful: true);

        var horizon = 42;

        // City skyline
        DrawTokyoBuildings(c, horizon);

        // Water surface: dark gradient below horizon
        for (var y = horizon; y < VirtualHeight; y++)
        {
            var fade = (y - horizon) / (float)(VirtualHeight - horizon);
            c.HLine(0, y, VirtualWidth, Lerp(Rgb(8, 4, 40), Rgb(2, 1, 12), fade));
        }

        // Horizon neon glow line
        c.HLine(0, horizon - 1, VirtualWidth, Rgb(110, 36, 200));
        c.HLine(0, horizon,     VirtualWidth, Rgb(82, 24, 160));

        // Water reflections with ripple animation
        DrawTokyoReflections(c, horizon);

        // Left neon sign: tall (未来ホテル stacked)
        DrawNeonSignLeft(c, 3, 10);

        // Right neon sign: shorter (ホテル)
        DrawNeonSignRight(c, 116, 15);

        // Large hot-pink time — pulse on even seconds
        var pulse = (c.Tick / 500 % 2) == 0 ? HotPink : Rgb(220, 48, 136);
        var time = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", System.Globalization.CultureInfo.InvariantCulture);
        DrawDotText(c, CenterX(time, 4, 2), 8, time, pulse, 4, 2, glow: true);
    }

    private static void DrawTokyoBuildings(in Ctx c, int horizon)
    {
        // Varied building heights and widths for a dense skyline
        int[] heights = [18, 26, 12, 32, 20, 28, 15, 30, 22, 25, 14, 22, 17, 29, 13, 20, 24, 16, 19];
        int[] widths  = [  5,  4,  6,  5,  4,  5,  6,  4,  5,  4,  5,  6,  4,  5,  6,  4,  5,  6,  4];

        // Start after the left sign gap (x≈12) to leave room for the sign
        var cursor = 13;
        for (var i = 0; i < heights.Length && cursor < 113; i++)
        {
            var w = widths[i % widths.Length];
            var bh = heights[i];
            var x = cursor;
            var y = horizon - bh;

            // Building body
            c.FillRect(x, y, w, bh + 1, Rgb(4, 3, 18));
            // Left edge highlight (purple)
            c.VLine(x, y, bh, Rgb(40, 16, 80));
            // Top bar
            c.HLine(x, y, w, Rgb(60, 22, 110));

            // Windows — random-ish pattern using position hash
            for (var wy = y + 2; wy < horizon - 1; wy += 3)
            for (var wx = x + 1; wx < x + w - 1; wx += 2)
            {
                var seed = (wx * 7 + wy * 13 + i * 5);
                if (seed % 5 < 2)
                {
                    // Window flickers occasionally
                    var flicker = ((wx + wy + i + c.Tick / 200) % 11) == 0;
                    var winColor = flicker ? HotPink
                        : seed % 3 == 0 ? Rgb(52, 80, 230)
                        : Rgb(38, 58, 200);
                    c.Px(wx, wy, winColor);
                }
            }

            // Antenna on tall buildings
            if (bh > 24)
            {
                var blinkOn = (c.Tick / 600 % 2) == 0;
                c.Px(x + w / 2, y - 1, blinkOn ? Rgb(255, 60, 60) : Rgb(100, 20, 20));
                c.Px(x + w / 2, y - 2, Rgb(60, 60, 60));
            }

            cursor += w + 1;
        }
    }

    private static void DrawTokyoReflections(in Ctx c, int horizon)
    {
        var ripple = c.Tick / 120 % 4;
        for (var dy = 1; dy <= 16; dy++)
        {
            var srcY = horizon - dy;
            var dstY = horizon + dy - 1;
            var fade = 1f - dy / 18f;
            for (var x = 0; x < VirtualWidth; x++)
            {
                // Ripple shifts source pixel horizontally
                var srcX = Math.Clamp(x + (dy + ripple) % 3 - 1, 0, VirtualWidth - 1);
                var src = c.Get(srcX, srcY);
                if ((src.R | src.G | src.B) == 0) continue;
                c.MaxPx(x, dstY, Scale(src, fade * 0.40f));
            }
        }

        // Horizontal shimmer lines on water
        var shimmer = c.Tick / 60 % 6;
        c.HLine(0, horizon + shimmer,     VirtualWidth, Rgb(80, 24, 160));
        c.HLine(0, horizon + shimmer + 1, VirtualWidth, Rgb(40, 10, 80));
    }

    // ── Japanese-style pixel art neon signs ──────────────────────────────

    // Each character is a 5×7 bitmap (row strings, MSB left)
    private static readonly string[][] NeonChars =
    [
        // 未 (mi) — future
        ["11111", "10001", "11111", "10101", "11111", "10001", "10001"],
        // 来 (rai) — come
        ["01010", "11111", "01010", "11111", "01010", "10101", "01010"],
        // ホ (ho)
        ["10101", "11111", "00100", "11111", "10101", "00100", "00100"],
        // テ (te)
        ["11111", "00100", "11111", "00100", "00100", "01100", "00100"],
        // ル (ru)
        ["01001", "01001", "01011", "01101", "10101", "10001", "01110"],
    ];

    private static void DrawNeonSignLeft(in Ctx c, int x, int y)
    {
        // Tall sign: 未来ホテル (4 chars in column)
        var flashOn = (c.Tick / 200 % 5) != 0;
        var color   = flashOn ? Rgb(255, 100, 210) : Rgb(200, 60, 170);
        var dim     = Scale(color, 0.25f);

        // Sign border (9 wide, 30 tall)
        c.Rect(x, y, 9, 32, color);
        c.FillRect(x + 1, y + 1, 7, 30, Rgb(6, 0, 8));

        // Draw 4 characters (未来ホテル = indices 0,1,2,3)
        for (var ci = 0; ci < 4; ci++)
        {
            var glyph = NeonChars[ci];
            var cy = y + 2 + ci * 7;
            for (var row = 0; row < 7; row++)
            for (var col = 0; col < 5; col++)
                if (glyph[row][col] == '1')
                    c.Px(x + 2 + col, cy + row, color);
                else
                    c.Px(x + 2 + col, cy + row, dim);
        }
    }

    private static void DrawNeonSignRight(in Ctx c, int x, int y)
    {
        // Shorter sign: ホテル (3 chars)
        var flashOn = (c.Tick / 200 % 5) != 2;
        var color   = flashOn ? Rgb(255, 100, 210) : Rgb(200, 60, 170);
        var dim     = Scale(color, 0.25f);

        // Sign border (9 wide, 23 tall)
        c.Rect(x, y, 9, 25, color);
        c.FillRect(x + 1, y + 1, 7, 23, Rgb(6, 0, 8));

        // Draw 3 characters (ホテル = indices 2,3,4)
        for (var ci = 0; ci < 3; ci++)
        {
            var glyph = NeonChars[ci + 2];
            var cy = y + 2 + ci * 7;
            for (var row = 0; row < 7; row++)
            for (var col = 0; col < 5; col++)
                if (glyph[row][col] == '1')
                    c.Px(x + 2 + col, cy + row, color);
                else
                    c.Px(x + 2 + col, cy + row, dim);
        }
    }
}
