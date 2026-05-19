using MicaAudio.Core.Presets;

namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawAurora(in Ctx c)
    {
        // Deep indigo night sky
        FillGradient(c, Rgb(4, 10, 28), Rgb(5, 16, 32));
        ApplyMatrixTexture(c, Rgb(8, 28, 44), 5);

        // Stars behind the aurora
        DrawStarfield(c, 35, colorful: false);

        // Aurora bands (bottom-most drawn first so upper ones overlay)
        DrawAuroraBand(c, baseY: 8,  height: 16, color: Rgb(22,  190, 120), phase: 0.00, amp: 8);
        DrawAuroraBand(c, baseY: 12, height: 14, color: Rgb(30,  175, 165), phase: 1.35, amp: 7);
        DrawAuroraBand(c, baseY: 16, height: 12, color: Rgb(90,  58,  215), phase: 2.15, amp: 6);
        DrawAuroraBand(c, baseY: 10, height: 10, color: Rgb(145, 48,  178), phase: 3.10, amp: 5);
        DrawAuroraBand(c, baseY: 4,  height:  8, color: Rgb(42,  210, 90),  phase: 0.70, amp: 4);

        // Mountain silhouettes
        DrawMountains(c, 46, Rgb(4, 12, 20), Rgb(9, 22, 30));

        // Pine tree forest in front of mountains
        DrawAuroraForest(c, 47);

        // Time (smaller pitch to leave room for the scenic elements)
        var time = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", System.Globalization.CultureInfo.InvariantCulture);
        DrawDotText(c, CenterX(time, 3, 2), 22, time, White, 3, 2, glow: false);

        // Date below time
        var date = $"{DayPt(c.Now.DayOfWeek)} {c.Now:dd/MM/yyyy}";
        c.Text((128 - TextWidth(date)) / 2, 40, date, Rgb(210, 235, 228));

        // Animated shimmer line at base of aurora
        var shim = c.Tick / 100 % 12;
        c.HLine(44 + shim, 52, 30 - shim, Rgb(24, 88, 84));
    }

    private static void DrawAuroraBand(in Ctx c, int baseY, int height, RgbaColor color, double phase, int amp)
    {
        var motion = c.Tick / 1200.0;
        for (var x = 0; x < VirtualWidth; x++)
        {
            var y0 = baseY + (int)Math.Round(Math.Sin(x * 0.08 + phase + motion) * amp);
            for (var dy = 0; dy < height; dy++)
            {
                var fade   = 1f - dy / (float)height;
                var stripe = ((x + dy + c.Tick / 90) % 7) == 0 ? 0.98f : 0.64f;
                c.MaxPx(x, y0 + dy, Scale(color, fade * stripe));
            }
        }
    }

    private static void DrawAuroraForest(in Ctx c, int groundY)
    {
        for (var x = -2; x < 130; x += 6)
        {
            var treeH = 9 + (x * 7 & 7);
            for (var row = 0; row < treeH; row++)
            {
                var half = Math.Max(1, row / 3);
                c.HLine(x + 3 - half, groundY - treeH + row, half * 2 + 1, Rgb(2, 10, 12));
            }
        }
    }
}
