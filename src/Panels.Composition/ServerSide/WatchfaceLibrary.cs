using System.Globalization;
using MicaAudio.Core.Presets;
using Panels.Composition.Drawing;

namespace Panels.Composition.ServerSide;

// DOCS: docs/wiki/modules/paineis.md#compositor-hub75
// Procedural 128x64 HUB75 watchfaces used by both server-side composition and WinUI preview.
// Each watchface lives in Watchfaces/Watchface<Name>.cs as a partial class fragment.
public static partial class WatchfaceLibrary
{
    private const int VirtualWidth = 128;
    private const int VirtualHeight = 64;

    public static IReadOnlyList<string> All { get; } =
    [
        "cyberterminal", "flipclock", "neotokyo", "relogiochuva", "aurora",
        "gridscifi", "retroambar", "cosmico", "monocromatico", "cyberpunk",
    ];

    public static bool IsKnown(string? mostrador) =>
        !string.IsNullOrWhiteSpace(mostrador)
        && All.Contains(mostrador.Trim().ToLowerInvariant());

    public static void Render(
        string mostrador,
        RgbaColor[] frame,
        int frameWidth,
        int frameHeight,
        int widgetX,
        int widgetY,
        int widgetWidth,
        int widgetHeight,
        DateTime brasiliaNow,
        bool use24Hour)
    {
        if (widgetWidth <= 0 || widgetHeight <= 0)
        {
            return;
        }

        ClearRegion(frame, frameWidth, frameHeight, widgetX, widgetY, widgetWidth, widgetHeight);

        var virtualFrame = new RgbaColor[VirtualWidth * VirtualHeight];
        Array.Fill(virtualFrame, Black);

        var ctx = new Ctx(virtualFrame, brasiliaNow, use24Hour);
        switch ((mostrador ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "flipclock":     DrawFlipClock(ctx);     break;
            case "neotokyo":      DrawNeoTokyo(ctx);      break;
            case "relogiochuva":  DrawRelogioChuva(ctx);  break;
            case "aurora":        DrawAurora(ctx);         break;
            case "gridscifi":     DrawGridSciFi(ctx);     break;
            case "retroambar":    DrawRetroAmbar(ctx);    break;
            case "cosmico":       DrawCosmico(ctx);        break;
            case "monocromatico": DrawMonocromatico(ctx); break;
            case "cyberpunk":     DrawCyberpunk(ctx);     break;
            default:              DrawCyberTerminal(ctx);  break;
        }

        BlitVirtualFrame(virtualFrame, frame, frameWidth, frameHeight, widgetX, widgetY, widgetWidth, widgetHeight);
    }

    // ──────────────────────────────────────────────
    // Ctx — drawing context passed to every watchface
    // ──────────────────────────────────────────────

    private readonly struct Ctx(RgbaColor[] frame, DateTime now, bool use24Hour)
    {
        public readonly RgbaColor[] Frame = frame;
        public readonly DateTime Now = now;
        public readonly bool Use24Hour = use24Hour;

        // Monotonically increasing sub-second counter for animations (0..59999 per minute)
        public readonly int Tick = (now.Second * 1000) + now.Millisecond;

        public void Px(int x, int y, RgbaColor color)
        {
            if ((uint)x >= VirtualWidth || (uint)y >= VirtualHeight) return;
            Frame[(y * VirtualWidth) + x] = color;
        }

        public RgbaColor Get(int x, int y)
        {
            if ((uint)x >= VirtualWidth || (uint)y >= VirtualHeight) return Black;
            return Frame[(y * VirtualWidth) + x];
        }

        public void MaxPx(int x, int y, RgbaColor color)
        {
            if ((uint)x >= VirtualWidth || (uint)y >= VirtualHeight) return;
            var i = (y * VirtualWidth) + x;
            var cur = Frame[i];
            Frame[i] = Rgb(Math.Max(cur.R, color.R), Math.Max(cur.G, color.G), Math.Max(cur.B, color.B));
        }

        public void Text(int x, int y, string text, RgbaColor color) =>
            PanelsMatrixDrawHelpers.DrawText5x7(Frame, VirtualWidth, VirtualHeight, x, y, text, color);

        public void HLine(int x, int y, int len, RgbaColor color)
        {
            for (var i = 0; i < len; i++) Px(x + i, y, color);
        }

        public void VLine(int x, int y, int len, RgbaColor color)
        {
            for (var i = 0; i < len; i++) Px(x, y + i, color);
        }

        public void Rect(int x, int y, int width, int height, RgbaColor color)
        {
            HLine(x, y, width, color);
            HLine(x, y + height - 1, width, color);
            VLine(x, y, height, color);
            VLine(x + width - 1, y, height, color);
        }

        public void FillRect(int x, int y, int width, int height, RgbaColor color)
        {
            if (width <= 0 || height <= 0) return;
            for (var row = 0; row < height; row++) HLine(x, y + row, width, color);
        }

        public void Clear(RgbaColor color) => FillRect(0, 0, VirtualWidth, VirtualHeight, color);

        public void Line(int x0, int y0, int x1, int y1, RgbaColor color)
        {
            var dx = Math.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Math.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var err = dx + dy;
            while (true)
            {
                Px(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                var e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Circle(int cx, int cy, int r, RgbaColor color)
        {
            var x = r; var y = 0; var err = 1 - r;
            while (x >= y)
            {
                Px(cx + x, cy + y, color); Px(cx - x, cy + y, color);
                Px(cx + x, cy - y, color); Px(cx - x, cy - y, color);
                Px(cx + y, cy + x, color); Px(cx - y, cy + x, color);
                Px(cx + y, cy - x, color); Px(cx - y, cy - x, color);
                y++;
                if (err < 0) err += (2 * y) + 3;
                else { err += (2 * (y - x)) + 5; x--; }
            }
        }

        public void Disc(int cx, int cy, int r, RgbaColor color)
        {
            for (var dy = -r; dy <= r; dy++)
            for (var dx = -r; dx <= r; dx++)
                if ((dx * dx) + (dy * dy) <= r * r) Px(cx + dx, cy + dy, color);
        }
    }

    // ──────────────────────────────────────────────
    // Shared background fills
    // ──────────────────────────────────────────────

    private static void FillGradient(in Ctx c, RgbaColor top, RgbaColor bottom)
    {
        for (var y = 0; y < VirtualHeight; y++)
            c.HLine(0, y, VirtualWidth, Lerp(top, bottom, y / 63f));
    }

    private static void FillGridNoise(in Ctx c, RgbaColor baseColor, RgbaColor altColor, int stride)
    {
        c.FillRect(0, 0, VirtualWidth, VirtualHeight, baseColor);
        for (var y = 0; y < VirtualHeight; y++)
        for (var x = 0; x < VirtualWidth; x++)
            if (((x * 3) + (y * 5)) % stride == 0) c.Px(x, y, altColor);
    }

    private static void ApplyMatrixTexture(in Ctx c, RgbaColor color, int modulo)
    {
        for (var y = 0; y < VirtualHeight; y += 2)
        for (var x = (y / 2) % 2; x < VirtualWidth; x += 2)
            if (((x + y) % modulo) == 0) c.MaxPx(x, y, color);

        for (var y = 1; y < VirtualHeight; y += 4)
        for (var x = 0; x < VirtualWidth; x++)
            c.Px(x, y, Scale(c.Get(x, y), 0.82f));
    }

    // ──────────────────────────────────────────────
    // Shared scenery helpers (used by 2+ watchfaces)
    // ──────────────────────────────────────────────

    private static void DrawStarfield(in Ctx c, int count, bool colorful)
    {
        for (var i = 0; i < count; i++)
        {
            var x = (i * 67 + 23) % VirtualWidth;
            var y = (i * 43 + 11) % 44;
            var blink = ((i + c.Tick / 150) % 9) == 0;
            var bright = ((i + c.Tick / 300) % 17) == 0;
            RgbaColor color;
            if (colorful)
            {
                color = blink  ? Rgb(255, 96, 200)  :
                        bright ? Rgb(220, 200, 255)  :
                        i % 3 == 0 ? Rgb(106, 92, 214) : Rgb(176, 160, 214);
            }
            else
            {
                color = blink ? Rgb(230, 230, 235) : bright ? Rgb(180, 182, 186) : Rgb(88, 90, 96);
            }
            c.Px(x, y, color);
            if (bright) c.MaxPx(x + 1, y, Scale(color, 0.4f));
        }
    }

    private static void DrawMountains(in Ctx c, int baseY, RgbaColor low, RgbaColor high)
    {
        int[] peaks = [10, 18, 7, 25, 14, 20, 9, 16, 28, 13, 21, 10, 24, 16, 19, 11];
        for (var i = 0; i < peaks.Length; i++)
        {
            var x0 = i * 8;
            var peak = baseY - peaks[i];
            for (var x = 0; x < 8; x++)
            {
                var edge = Math.Abs(x - 4);
                var top = peak + edge * 2;
                for (var y = top; y < VirtualHeight; y++)
                    c.Px(x0 + x, y, Lerp(high, low, Math.Clamp((y - top) / 22f, 0f, 1f)));
            }
        }
    }

    // ──────────────────────────────────────────────
    // Dot-matrix large text (LED-style display)
    // ──────────────────────────────────────────────

    private static int DrawDotText(in Ctx c, int x, int y, string text, RgbaColor color, int pitch, int dotSize, bool glow)
    {
        var normalized = MatrixFont5x7.Normalize(text);
        var cursor = x;
        foreach (var ch in normalized)
        {
            if (!MatrixFont5x7.TryGetGlyph(ch, out var glyph))
            {
                cursor += pitch * 3;
                continue;
            }

            var charWidth = ch == ':' ? 2 : 5;
            for (var row = 0; row < glyph.Length; row++)
            for (var col = 0; col < Math.Min(charWidth, glyph[row].Length); col++)
            {
                if (glyph[row][col] != '1') continue;
                var px = cursor + (col * pitch);
                var py = y + (row * pitch);
                if (glow)
                {
                    c.MaxPx(px - 1, py,            Scale(color, 0.30f));
                    c.MaxPx(px + dotSize, py,       Scale(color, 0.30f));
                    c.MaxPx(px, py - 1,             Scale(color, 0.24f));
                    c.MaxPx(px, py + dotSize,       Scale(color, 0.24f));
                    c.MaxPx(px - 1, py - 1,         Scale(color, 0.12f));
                    c.MaxPx(px + dotSize, py + dotSize, Scale(color, 0.12f));
                }

                var lit = ((row + col) % 2) == 0 ? color : Scale(color, 0.88f);
                c.FillRect(px, py, dotSize, dotSize, lit);
            }

            cursor += ch == ':' ? pitch * 2 : pitch * 6;
        }

        return cursor - x;
    }

    private static int DotTextWidth(string text, int pitch)
    {
        var normalized = MatrixFont5x7.Normalize(text);
        var width = 0;
        foreach (var ch in normalized)
            width += ch == ':' ? pitch * 2 : pitch * 6;
        return Math.Max(0, width - pitch);
    }

    private static int CenterX(string text, int pitch, int dotSize) =>
        Math.Max(0, (VirtualWidth - DotTextWidth(text, pitch) - dotSize) / 2);

    private static int TextWidth(string text) =>
        Math.Max(0, MatrixFont5x7.Normalize(text).Length * 6 - 1);

    // ──────────────────────────────────────────────
    // Locale helpers
    // ──────────────────────────────────────────────

    private static string DayPt(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => "SEG",
        DayOfWeek.Tuesday   => "TER",
        DayOfWeek.Wednesday => "QUA",
        DayOfWeek.Thursday  => "QUI",
        DayOfWeek.Friday    => "SEX",
        DayOfWeek.Saturday  => "SAB",
        _                   => "DOM",
    };

    private static string MonthEn(int month) => month switch
    {
        1  => "JAN", 2 => "FEB", 3  => "MAR", 4  => "APR",
        5  => "MAY", 6 => "JUN", 7  => "JUL", 8  => "AUG",
        9  => "SEP", 10 => "OCT", 11 => "NOV", _ => "DEC",
    };

    // ──────────────────────────────────────────────
    // Colour math
    // ──────────────────────────────────────────────

    private static readonly RgbaColor Black     = Rgb(0,   0,   0);
    private static readonly RgbaColor White     = Rgb(245, 248, 250);
    private static readonly RgbaColor Cyan      = Rgb(38,  224, 239);
    private static readonly RgbaColor CyanDim   = Rgb(8,   72,  86);
    private static readonly RgbaColor Amber     = Rgb(255, 176, 36);
    private static readonly RgbaColor AmberDim  = Rgb(82,  47,  8);
    private static readonly RgbaColor HotPink   = Rgb(255, 63,  158);
    private static readonly RgbaColor Violet    = Rgb(118, 56,  230);
    private static readonly RgbaColor Blue      = Rgb(34,  88,  220);
    private static readonly RgbaColor Green     = Rgb(38,  210, 126);

    private static RgbaColor Rgb(int r, int g, int b) =>
        new((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255), 255);

    private static RgbaColor Lerp(RgbaColor a, RgbaColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Rgb(
            (int)MathF.Round(a.R + (b.R - a.R) * t),
            (int)MathF.Round(a.G + (b.G - a.G) * t),
            (int)MathF.Round(a.B + (b.B - a.B) * t));
    }

    private static RgbaColor Scale(RgbaColor color, float amount) =>
        Rgb((int)(color.R * amount), (int)(color.G * amount), (int)(color.B * amount));

    // ──────────────────────────────────────────────
    // Frame infrastructure
    // ──────────────────────────────────────────────

    private static void ClearRegion(RgbaColor[] frame, int fw, int fh, int x, int y, int w, int h)
    {
        for (var dy = 0; dy < h; dy++)
        for (var dx = 0; dx < w; dx++)
            PanelsMatrixDrawHelpers.DrawPixel(frame, fw, fh, x + dx, y + dy, Black);
    }

    private static void BlitVirtualFrame(
        RgbaColor[] source, RgbaColor[] target,
        int tw, int th, int dx, int dy, int dw, int dh)
    {
        for (var y = 0; y < dh; y++)
        {
            var sy = Math.Clamp(y * VirtualHeight / dh, 0, VirtualHeight - 1);
            for (var x = 0; x < dw; x++)
            {
                var sx = Math.Clamp(x * VirtualWidth / dw, 0, VirtualWidth - 1);
                PanelsMatrixDrawHelpers.DrawPixel(target, tw, th, dx + x, dy + y, source[(sy * VirtualWidth) + sx]);
            }
        }
    }
}
