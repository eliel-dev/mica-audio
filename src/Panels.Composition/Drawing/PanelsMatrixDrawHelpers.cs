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

    /// <summary>
    /// Copies pre-scaled source pixels into <paramref name="target"/> at the widget
    /// position (destX, destY). Source pixels with alpha == 0 are skipped so that
    /// letterbox padding from <see cref="MediaScaleMode.Fit"/> remains transparent.
    /// The caller is responsible for ensuring source pixels are already sized to
    /// match the widget rectangle (destWidth × destHeight).
    /// </summary>
    public static void BlitDirect(
        ReadOnlySpan<RgbaColor> source,
        int srcWidth,
        int srcHeight,
        RgbaColor[] target,
        int targetWidth,
        int targetHeight,
        int destX,
        int destY)
    {
        for (var row = 0; row < srcHeight; row++)
        {
            var ty = destY + row;
            if (ty < 0 || ty >= targetHeight)
            {
                continue;
            }

            for (var col = 0; col < srcWidth; col++)
            {
                var tx = destX + col;
                if (tx < 0 || tx >= targetWidth)
                {
                    continue;
                }

                var px = source[(row * srcWidth) + col];

                // Respect alpha: fully transparent pixels (black padding from Fit mode)
                // do not overwrite whatever is below this widget layer.
                if (px.A == 0)
                {
                    continue;
                }

                target[(ty * targetWidth) + tx] = px;
            }
        }
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
