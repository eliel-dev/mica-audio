using MicaAudio.Core.Presets;

namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawCosmico(in Ctx c)
    {
        // Deep space gradient
        FillGradient(c, Rgb(2, 1, 20), Rgb(10, 3, 40));
        ApplyMatrixTexture(c, Rgb(14, 8, 65), 6);

        // Dense colorful starfield
        DrawStarfield(c, 90, colorful: true);

        // Nebula cloud patches (subtle)
        DrawNebula(c, 30, 10, 22, Rgb(60, 20, 100), 0.18f);
        DrawNebula(c, 85, 35, 18, Rgb(20, 40, 110), 0.12f);

        // Large ringed planet — upper right
        DrawPlanet(c, cx: 108, cy: 14, r: 19,
            dark: Rgb(44, 22, 130), light: Rgb(175, 58, 200), ring: true);

        // Small moon — upper left
        DrawPlanet(c, cx: 20, cy: 28, r: 5,
            dark: Rgb(18, 10, 65), light: Rgb(110, 36, 165), ring: false);

        // Orbiting dot around main planet (animated)
        DrawOrbitingDot(c, cx: 108, cy: 14, radius: 24, color: Rgb(245, 90, 195));

        // Purple mountain horizon
        DrawMountains(c, 52, Rgb(10, 3, 30), Rgb(44, 10, 75));

        // Large white time centered in mid-panel
        var time = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", System.Globalization.CultureInfo.InvariantCulture);
        DrawDotText(c, CenterX(time, 4, 2), 28, time, White, 4, 2, glow: false);
    }

    private static void DrawPlanet(in Ctx c, int cx, int cy, int r, RgbaColor dark, RgbaColor light, bool ring)
    {
        // Planet body with diagonal shade bands
        for (var dy = -r; dy <= r; dy++)
        for (var dx = -r; dx <= r; dx++)
        {
            if ((dx * dx) + (dy * dy) > r * r) continue;
            var shade  = Math.Clamp((dx + dy + r) / (float)(r * 2), 0f, 1f);
            var band   = ((dy + c.Tick / 200) % 6) == 0 ? 0.25f : 0f;
            c.Px(cx + dx, cy + dy, Lerp(light, dark, Math.Clamp(shade + band, 0f, 1f)));
        }

        // Rim highlight
        c.Circle(cx, cy, r, Scale(light, 0.75f));

        if (!ring) return;

        // Planetary ring — sinusoidal arc, coloured
        for (var rx = -(r + 7); rx <= r + 7; rx++)
        {
            var ry = (int)Math.Round(Math.Sin(rx * 0.18) * 4);
            var inPlanet = (rx * rx + ry * ry) <= r * r;
            if (inPlanet) continue;

            var ringColor = rx % 2 == 0 ? Rgb(210, 165, 70) : Rgb(100, 38, 155);
            c.MaxPx(cx + rx, cy + ry,     ringColor);
            c.MaxPx(cx + rx, cy + ry + 1, Scale(ringColor, 0.5f));
        }
    }

    private static void DrawOrbitingDot(in Ctx c, int cx, int cy, int radius, RgbaColor color)
    {
        var angle = (c.Tick / 1100.0) * Math.PI * 2;
        var ox = cx + (int)Math.Round(Math.Cos(angle) * radius);
        var oy = cy + (int)Math.Round(Math.Sin(angle) * (radius / 4));
        c.Disc(ox, oy, 1, color);
        // Short trail
        for (var t = 1; t <= 3; t++)
        {
            var ta = angle - t * 0.22;
            var tx = cx + (int)Math.Round(Math.Cos(ta) * radius);
            var ty = cy + (int)Math.Round(Math.Sin(ta) * (radius / 4));
            c.MaxPx(tx, ty, Scale(color, 0.35f / t));
        }
    }

    private static void DrawNebula(in Ctx c, int cx, int cy, int r, RgbaColor color, float intensity)
    {
        for (var dy = -r; dy <= r; dy++)
        for (var dx = -r; dx <= r; dx++)
        {
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > r) continue;
            var fade = (1f - dist / r) * intensity;
            if (((cx + dx) * 3 + (cy + dy) * 5) % 4 != 0) continue;
            c.MaxPx(cx + dx, cy + dy, Scale(color, fade));
        }
    }
}
