using MicaAudio.Core.Presets;

namespace Panels.Composition.Drawing;

// Cross-platform mirror of App.WinUI.Services.Panels.PanelsMatrixDrawHelpers
// used by autonomous server-side widgets.
public static class PanelsMatrixDrawHelpers
{
    public static readonly RgbaColor Black = new(0, 0, 0, 255);

    public static void Clear(RgbaColor[] frame)
    {
        Array.Fill(frame, Black);
    }

    public static void DrawPixel(RgbaColor[] frame, int frameWidth, int frameHeight, int x, int y, RgbaColor color)
    {
        if (x < 0 || x >= frameWidth || y < 0 || y >= frameHeight)
        {
            return;
        }

        frame[(y * frameWidth) + x] = color;
    }

    public static void DrawText5x7(RgbaColor[] frame, int frameWidth, int frameHeight, int x, int y, string text, RgbaColor color)
    {
        var normalized = MatrixFont5x7.Normalize(text);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        var cursorX = x;
        foreach (var ch in normalized)
        {
            if (!MatrixFont5x7.TryGetGlyph(ch, out var glyph))
            {
                cursorX += 6;
                continue;
            }

            for (var row = 0; row < glyph.Length; row++)
            {
                var rowBits = glyph[row];
                for (var col = 0; col < rowBits.Length; col++)
                {
                    if (rowBits[col] == '1')
                    {
                        DrawPixel(frame, frameWidth, frameHeight, cursorX + col, y + row, color);
                    }
                }
            }

            cursorX += 6;
        }
    }
}
