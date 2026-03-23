using System.Globalization;
using System.Text;
using Microsoft.Graphics.Canvas;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
// DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
internal static class Hub75PreviewHelper
{
    public const int PanelWidth = 128;
    public const int PanelHeight = 64;

    private static readonly Dictionary<char, string[]> Font5x7 = new()
    {
        ['0'] = ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        ['6'] = ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
        ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['B'] = ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        ['C'] = ["01110", "10001", "10000", "10000", "10000", "10001", "01110"],
        ['D'] = ["11100", "10010", "10001", "10001", "10001", "10010", "11100"],
        ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        ['F'] = ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
        ['G'] = ["01110", "10001", "10000", "10111", "10001", "10001", "01110"],
        ['H'] = ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['I'] = ["01110", "00100", "00100", "00100", "00100", "00100", "01110"],
        ['J'] = ["00111", "00010", "00010", "00010", "00010", "10010", "01100"],
        ['K'] = ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
        ['L'] = ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
        ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        ['N'] = ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
        ['O'] = ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        ['Q'] = ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
        ['R'] = ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['U'] = ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['V'] = ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
        ['W'] = ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
        ['X'] = ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
        ['Y'] = ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
        ['Z'] = ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
        [':'] = ["00000", "00100", "00100", "00000", "00100", "00100", "00000"],
        ['-'] = ["00000", "00000", "00000", "01110", "00000", "00000", "00000"],
        ['.'] = ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
        ['/'] = ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
        [' '] = ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
    };

    public static void DrawPanel(in AppPreviewRenderContext context, out float ox, out float oy, out float pitch, out float ledSize)
    {
        var padding = MathF.Min(6f, MathF.Max(2f, MathF.Min(context.Width, context.Height) * 0.08f));
        var availableWidth = MathF.Max(1f, context.Width - (padding * 2f));
        var availableHeight = MathF.Max(1f, context.Height - (padding * 2f));

        pitch = MathF.Min(availableWidth / PanelWidth, availableHeight / PanelHeight);
        pitch = MathF.Max(pitch, 0.1f);
        ledSize = pitch < 1f
            ? MathF.Min(pitch, MathF.Max(0.25f, pitch * 0.92f))
            : MathF.Max(1f, pitch * 0.78f);

        var drawWidth = PanelWidth * pitch;
        var drawHeight = PanelHeight * pitch;
        ox = (context.Width - drawWidth) * 0.5f;
        oy = (context.Height - drawHeight) * 0.5f;

        var ds = context.DrawingSession;
        ds.FillRoundedRectangle(ox - 2f, oy - 2f, drawWidth + 4f, drawHeight + 4f, 6f, 6f, Color.FromArgb(255, 2, 3, 5));
        ds.DrawRoundedRectangle(ox - 2f, oy - 2f, drawWidth + 4f, drawHeight + 4f, 6f, 6f, Color.FromArgb(255, 38, 50, 64), 1f);
        ds.FillRectangle(ox, oy, drawWidth, drawHeight, Color.FromArgb(255, 0, 0, 0));
    }

    public static string NormalizeForMatrix(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var upper = char.ToUpperInvariant(ch);
            if (Font5x7.ContainsKey(upper))
            {
                builder.Append(upper);
                continue;
            }

            builder.Append(char.IsWhiteSpace(ch) ? ' ' : '-');
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static void DrawPixel(CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y, Color color, bool glow = true)
    {
        if (x < 0 || x >= PanelWidth || y < 0 || y >= PanelHeight)
        {
            return;
        }

        var left = ox + (x * pitch) + ((pitch - ledSize) * 0.5f);
        var top = oy + (y * pitch) + ((pitch - ledSize) * 0.5f);

        if (glow)
        {
            var glowColor = Color.FromArgb((byte)Math.Min(120, (int)color.A), color.R, color.G, color.B);
            ds.FillRectangle(left - 0.35f, top - 0.35f, ledSize + 0.7f, ledSize + 0.7f, glowColor);
        }

        ds.FillRectangle(left, top, ledSize, ledSize, color);
    }

    public static void DrawFrame(
        CanvasDrawingSession drawingSession,
        float width,
        float height,
        IReadOnlyList<RgbaColor> pixels,
        int matrixWidth = PanelWidth,
        int matrixHeight = PanelHeight)
    {
        var matrixAspect = (float)matrixWidth / matrixHeight;
        var canvasAspect = width <= 0f || height <= 0f ? matrixAspect : width / height;
        var drawWidth = width;
        var drawHeight = height;

        if (canvasAspect > matrixAspect)
        {
            drawWidth = height * matrixAspect;
            drawHeight = height;
        }
        else
        {
            drawWidth = width;
            drawHeight = width / matrixAspect;
        }

        var offsetX = (width - drawWidth) * 0.5f;
        var offsetY = (height - drawHeight) * 0.5f;
        var cellW = drawWidth / matrixWidth;
        var cellH = drawHeight / matrixHeight;
        var drawCellW = Math.Max(1f, cellW);
        var drawCellH = Math.Max(1f, cellH);

        drawingSession.Clear(Color.FromArgb(255, 8, 10, 14));

        var requiredPixels = matrixWidth * matrixHeight;
        if (pixels.Count < requiredPixels)
        {
            return;
        }

        for (var y = 0; y < matrixHeight; y++)
        {
            var rowStart = y * matrixWidth;
            var drawY = offsetY + (y * cellH);

            var x = 0;
            while (x < matrixWidth)
            {
                var pixel = pixels[rowStart + x];
                if (pixel.A == 0)
                {
                    x++;
                    continue;
                }

                var runStart = x;
                x++;

                while (x < matrixWidth)
                {
                    var candidate = pixels[rowStart + x];
                    if (candidate.A != pixel.A
                        || candidate.R != pixel.R
                        || candidate.G != pixel.G
                        || candidate.B != pixel.B)
                    {
                        break;
                    }

                    x++;
                }

                var runLength = x - runStart;
                var color = Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B);
                drawingSession.FillRectangle(
                    offsetX + (runStart * cellW),
                    drawY,
                    drawCellW * runLength,
                    drawCellH,
                    color);
            }
        }
    }

    public static void DrawText5x7(CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y, string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var normalized = NormalizeForMatrix(text);
        var cursor = x;
        foreach (var ch in normalized)
        {
            if (!Font5x7.TryGetValue(ch, out var glyph))
            {
                cursor += 6;
                continue;
            }

            for (var row = 0; row < glyph.Length; row++)
            {
                var rowBits = glyph[row];
                for (var col = 0; col < rowBits.Length; col++)
                {
                    if (rowBits[col] == '1')
                    {
                        DrawPixel(ds, ox, oy, pitch, ledSize, cursor + col, y + row, color);
                    }
                }
            }

            cursor += 6;
        }
    }
}

