using System;
using System.Globalization;
using MicaAudio.Core.Presets;

namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static readonly RgbaColor CyberYellow = Rgb(252, 205, 0);
    private static readonly RgbaColor SlateDark   = Rgb(12, 14, 18);
    private static readonly RgbaColor DeepCrimson = Rgb(50, 6, 12);
    private static readonly RgbaColor CyberRed    = Rgb(255, 24, 64);

    // ----------------------------------------------------
    // Highly Detailed Cyberpunk HUB75 Watchface
    // ----------------------------------------------------
    private static void DrawCyberpunk(in Ctx c)
    {
        // 0. Base Dark Background / Geometry
        c.Clear(Rgb(12, 14, 18));
        DrawTechGrid(c);

        // 1. Top Header Bar (Angled Kiroshi Style)
        // Invokes native CutCorner helpers for faceted UI
        c.FillRect(0, 0, 42, 12, CyberYellow);
        Geometry.CutCornerBR(c, 0, 0, 42, 12, 4, SlateDark);
        DrawDotText(c, 2, 4, "V-OS 2", Black, 1, 1, glow: false);
        
        // 2. Optics Link & Auth State
        c.FillRect(45, 0, 83, 12, DeepCrimson);
        Geometry.CutCornerBL(c, 45, 0, 83, 12, 4, SlateDark);
        DrawDotText(c, 51, 4, "OPTICS LINK", CyberRed, 1, 1, glow: false);
        c.FillRect(114, 4, 3, 3, Cyan); // Cyan status square in top right badge

        // 3. System Spline & Notches (Red Side Divider)
        c.FillRect(72, 14, 2, 36, CyberRed);
        c.FillRect(71, 24, 4, 8, CyberRed); 

        // 4. Time Display Matrix & Reticles
        DrawMainTime(c);

        // 5. RAM / BIO.PRT Logs (Animated)
        DrawAggregatedStats(c);

        // 6. Bottom Alert Banner & Hazard Stripes
        DrawAlertFooter(c);

        // 7. Random Interference Tremors / Distortion Matrix
        ApplyGlitchDistortion(c);
    }

    private static void DrawTechGrid(in Ctx c)
    {
        var gridColor = Rgb(24, 28, 36);
        for (var y = 14; y < 52; y += 4)
        {
            for (var x = 2; x < VirtualWidth; x += 4)
            {
                if (x >= 70 && x <= 74) continue; // Skip red divider area
                c.Px(x, y, gridColor);
            }
        }
    }

    private static void DrawMainTime(in Ctx c)
    {
        var hour = c.Now.ToString("HH", CultureInfo.InvariantCulture);
        var minute = c.Now.ToString("mm", CultureInfo.InvariantCulture);

        var d0 = hour[0].ToString();
        var d1 = hour[1].ToString();
        var d2 = minute[0].ToString();
        var d3 = minute[1].ToString();

        // Exact visual coordinates to prevent overlap with colon and red spline
        var x0 = 2;
        var x1 = 18;
        var x2 = 39;
        var x3 = 55;
        var ty = 20;

        DrawGlitchDigit(c, x0, ty, d0);
        DrawGlitchDigit(c, x1, ty, d1);
        DrawGlitchDigit(c, x2, ty, d2);
        DrawGlitchDigit(c, x3, ty, d3);

        // Draw the 3-line colon in the center
        var colY1 = ty + 6;
        var colY2 = ty + 9;
        var colY3 = ty + 12;
        var colX = 33;
        var colLen = 5;

        c.HLine(colX, colY1, colLen, CyberYellow);
        c.HLine(colX, colY2, colLen, CyberYellow);
        c.HLine(colX, colY3, colLen, CyberYellow);

        // Draw the corner brackets around the time
        // Top-left bracket in Cyan
        c.FillRect(2, 19, 8, 2, Cyan);
        c.FillRect(2, 19, 2, 8, Cyan);

        // Bottom-right bracket in Cyan
        c.FillRect(62, 42, 8, 2, Cyan);
        c.FillRect(68, 36, 2, 8, Cyan);
    }

    private static void DrawGlitchDigit(in Ctx c, int x, int y, string digit)
    {
        // Draw chromatic aberration/glitch effect for a single digit
        // Cyan shadow to the left
        DrawDotText(c, x - 1, y, digit, Cyan, 3, 2, glow: false);
        // Magenta/Red shadow to the right
        DrawDotText(c, x + 1, y, digit, HotPink, 3, 2, glow: false);
        // Yellow shadow slightly up
        DrawDotText(c, x, y - 1, digit, CyberYellow, 3, 2, glow: false);
        // Main white body
        DrawDotText(c, x, y, digit, White, 3, 2, glow: false);
    }

    private static void DrawAggregatedStats(in Ctx c)
    {
        // 1. SYS.MEM Header & Animated bar
        c.Text(77, 15, "SYS.MEM", CyberYellow);
        
        var activeBlocks = 5 + (c.Tick / 1000 % 4);
        for (var i = 0; i < 8; i++)
        {
            var bx = 77 + (i * 5); // 5-pixel spacing: 3 wide + 2 gap
            var blockColor = i < activeBlocks ? CyberYellow : Rgb(40, 32, 0);
            c.FillRect(bx, 24, 4, 2, blockColor);
        }

        // 2. BIO.PRT Header & Animated Ticks
        c.Text(77, 29, "BIO.PRT", Cyan);

        var bioLevel = 4 + (c.Tick / 700 % 5);
        for (var i = 0; i < 12; i++)
        {
            var bx = 77 + (i * 3);
            var tickColor = i < bioLevel ? Cyan : Rgb(0, 32, 36);
            c.VLine(bx, 38, 3, tickColor);
        }

        // 3. Address Log (Dynamic/Animated hex address)
        string[] logs = ["0XB7A2", "0XFF2C", "0X04E8", "0X8D91"];
        var logIndex = (c.Tick / 1500) % logs.Length;
        c.Text(77, 43, logs[logIndex], CyberRed);
    }

    private static void DrawAlertFooter(in Ctx c)
    {
        // Red dividing line
        c.HLine(0, 52, 128, CyberRed);
        c.VLine(28, 52, 12, CyberRed);

        // Dark red hazard stripes on the left
        c.FillRect(4, 55, 3, 7, Rgb(90, 0, 24));
        c.FillRect(10, 55, 3, 7, Rgb(90, 0, 24));
        c.FillRect(16, 55, 3, 7, Rgb(90, 0, 24));
        c.FillRect(22, 55, 3, 7, Rgb(90, 0, 24));

        // Date in cyan
        var dateStr = c.Now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        c.Text(34, 55, dateStr, Cyan);

        // Angled CyberYellow badge on the right
        c.FillRect(100, 52, 28, 12, CyberYellow);
        Geometry.CutCornerTL(c, 100, 52, 28, 12, 4, SlateDark);

        // Current seconds inside the yellow badge in black
        var secondsStr = c.Now.Second.ToString("D2", CultureInfo.InvariantCulture);
        DrawDotText(c, 110, 55, secondsStr, Black, 1, 1, glow: false);
    }

    private static void ApplyGlitchDistortion(in Ctx c)
    {
        // Glitch/tremor triggers based on time hash to keep it natural and organic
        var tickSec = c.Tick / 300;
        var hash = (tickSec * 17 + c.Now.Second * 23) % 100;
        
        // Trigger tremor/interference ~15% of the time, in 300ms bursts
        if (hash >= 15) return;

        var rand = new Random(c.Tick);
        
        // Create a copy of the entire frame buffer to allow global shaking/wobble displacement
        var tempFrame = new RgbaColor[VirtualWidth * VirtualHeight];
        Array.Copy(c.Frame, tempFrame, c.Frame.Length);

        // 1. Matrix Wave Wobble / Distortion
        // Apply horizontal wave shift to the active middle region (y=12..51)
        var waveOffset = rand.NextDouble() * 10.0;
        var freq = 0.15 + rand.NextDouble() * 0.15;
        var amp = 1.0 + rand.NextDouble() * 1.5;

        for (var y = 12; y < 52; y++)
        {
            var shift = (int)Math.Round(Math.Sin(y * freq + waveOffset) * amp);
            if (shift == 0) continue;

            for (var x = 0; x < VirtualWidth; x++)
            {
                var srcX = x - shift;
                if (srcX >= 0 && srcX < VirtualWidth)
                {
                    c.Frame[y * VirtualWidth + x] = tempFrame[y * VirtualWidth + srcX];
                }
                else
                {
                    c.Frame[y * VirtualWidth + x] = SlateDark;
                }
            }
        }

        // Recopy frame state after wave distortion to apply shake on top
        Array.Copy(c.Frame, tempFrame, c.Frame.Length);

        // 2. Global Screen Shake / Tremor (vertical and horizontal offset)
        var dx = rand.Next(-1, 2);
        var dy = rand.Next(-1, 2);
        if (dx != 0 || dy != 0)
        {
            for (var y = 12; y < 52; y++)
            {
                var targetY = y + dy;
                if (targetY < 12 || targetY >= 52) continue;

                for (var x = 0; x < VirtualWidth; x++)
                {
                    var targetX = x + dx;
                    if (targetX < 0 || targetX >= VirtualWidth) continue;

                    c.Frame[targetY * VirtualWidth + targetX] = tempFrame[y * VirtualWidth + x];
                }
            }
        }

        // 3. Scanline Jitter: shift 2-3 random horizontal slices violently by +/- 3 pixels
        var numSlices = rand.Next(1, 4);
        for (var s = 0; s < numSlices; s++)
        {
            var startY = rand.Next(14, 50);
            var height = rand.Next(1, 4);
            var shift = rand.Next(0, 2) == 0 ? -3 : 3;

            for (var y = startY; y < Math.Min(51, startY + height); y++)
            {
                var rowCopy = new RgbaColor[VirtualWidth];
                for (var x = 0; x < VirtualWidth; x++) rowCopy[x] = c.Get(x, y);

                for (var x = 0; x < VirtualWidth; x++)
                {
                    var srcX = (x - shift + VirtualWidth) % VirtualWidth;
                    c.Px(x, y, rowCopy[srcX]);
                }
            }
        }

        // 4. Random cyber-noise horizontal streaks (bright green, cyan, or yellow static)
        if (rand.Next(100) < 60)
        {
            var noiseY = rand.Next(14, 50);
            var noiseX = rand.Next(8, 75);
            var noiseLen = rand.Next(12, 36);
            var noiseColor = rand.Next(3) switch
            {
                0 => Cyan,
                1 => CyberYellow,
                _ => CyberRed
            };
            c.HLine(noiseX, noiseY, noiseLen, noiseColor);
        }
    }

    private static class Geometry
    {
        public static void CutCornerBR(in Ctx c, int x, int y, int w, int h, int s, RgbaColor color)
        {
            for (var i = 0; i < s; i++)
            {
                c.HLine(x + w - s + i, y + h - 1 - i, s - i, color);
            }
        }

        public static void CutCornerBL(in Ctx c, int x, int y, int w, int h, int s, RgbaColor color)
        {
            for (var i = 0; i < s; i++)
            {
                c.HLine(x, y + h - 1 - i, s - i, color);
            }
        }

        public static void CutCornerTL(in Ctx c, int x, int y, int w, int h, int s, RgbaColor color)
        {
            for (var i = 0; i < s; i++)
            {
                c.HLine(x, y + i, s - i, color);
            }
        }
    }
}
