using MicaAudio.Core.Presets;

namespace MicaAudio.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-compartilhado
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

    public static void DrawHorizontalLine(RgbaColor[] frame, int frameWidth, int frameHeight, int x, int y, int width, RgbaColor color)
    {
        for (var offset = 0; offset < width; offset++)
        {
            DrawPixel(frame, frameWidth, frameHeight, x + offset, y, color);
        }
    }

    public static void Blit(
        RgbaColor[] source,
        int sourceWidth,
        int sourceHeight,
        RgbaColor[] destination,
        int destinationWidth,
        int destinationHeight,
        int destinationX,
        int destinationY)
    {
        for (var y = 0; y < sourceHeight; y++)
        {
            var targetY = destinationY + y;
            if (targetY < 0 || targetY >= destinationHeight)
            {
                continue;
            }

            for (var x = 0; x < sourceWidth; x++)
            {
                var targetX = destinationX + x;
                if (targetX < 0 || targetX >= destinationWidth)
                {
                    continue;
                }

                var color = source[(y * sourceWidth) + x];
                if (color.A == 0)
                {
                    continue;
                }

                destination[(targetY * destinationWidth) + targetX] = color;
            }
        }
    }
}
