using System.Globalization;
using MicaAudio.Core.Presets;

namespace Panels.Composition.ServerSide;

public static partial class WatchfaceLibrary
{
    private static void DrawCyberTerminal(in Ctx c)
    {
        // Background: dark teal grid noise with matrix texture
        FillGridNoise(c, Rgb(0, 9, 12), Rgb(0, 16, 22), 5);
        ApplyMatrixTexture(c, Rgb(0, 22, 28), 7);

        // Double border — outer bright, inner dim
        c.Rect(0, 0, 128, 64, Cyan);
        c.Rect(2, 2, 124, 60, CyanDim);
        c.Rect(3, 3, 122, 58, Scale(CyanDim, 0.6f));

        // Corner accent brackets (L-shapes)
        DrawCornerBracket(c,   1,  1, 10, Cyan, false, false);
        DrawCornerBracket(c, 117,  1, 10, Cyan, true,  false);
        DrawCornerBracket(c,   1, 53, 10, Cyan, false, true);
        DrawCornerBracket(c, 117, 53, 10, Cyan, true,  true);

        // Header row
        c.Text(6, 6, "> SYS ONLINE", Cyan);
        var upDays = (c.Now.DayOfYear - 1).ToString(CultureInfo.InvariantCulture);
        c.Text(76, 6, $"UP {upDays}D {c.Now:HH:mm}", Cyan);

        // Separator lines
        c.HLine(4, 15, 120, CyanDim);
        c.HLine(4, 16, 120, Scale(CyanDim, 0.5f));
        c.HLine(4, 46, 120, CyanDim);
        c.HLine(4, 47, 120, Scale(CyanDim, 0.5f));

        // Animated vertical scan line inside the text area
        var scanY = 17 + (c.Tick / 26 % 28);
        c.HLine(5, scanY,     118, Rgb(14, 92, 108));
        c.HLine(5, scanY + 1, 118, Rgb(6, 48, 58));

        // Large time
        var time = c.Now.ToString(c.Use24Hour ? "HH:mm" : "hh:mm", CultureInfo.InvariantCulture);
        DrawDotText(c, CenterX(time, 4, 2), 20, time, Cyan, 4, 2, glow: true);

        // Date row
        var date = $"{DayPt(c.Now.DayOfWeek)} {c.Now:dd/MM/yyyy}";
        var dw = TextWidth(date);
        c.Text((128 - dw) / 2, 50, date, Cyan);

        // Animated cursor dots bottom-right
        var phase = c.Tick / 80 % 5;
        for (var i = 0; i < 5; i++)
            c.FillRect(98 + i * 5, 57, 2, 2, i == phase ? Cyan : CyanDim);

        // Blinking bracket decoration bottom-left
        if ((c.Tick / 500 % 2) == 0)
        {
            c.Text(5, 57, "[", Scale(Cyan, 0.6f));
            c.Text(10, 57, "_", Scale(Cyan, 0.4f));
        }
    }

    private static void DrawCornerBracket(in Ctx c, int x, int y, int len, RgbaColor color, bool flipX, bool flipY)
    {
        var hx = flipX ? x - len + 1 : x;
        var vy = flipY ? y + len - 1  : y;
        var vx = flipX ? x + len - 1  : x;
        var hy = flipY ? y             : y;

        // Horizontal arm
        c.HLine(hx, hy, len, color);
        // Vertical arm
        c.VLine(vx, vy - (flipY ? len - 1 : 0), len, color);
    }
}
