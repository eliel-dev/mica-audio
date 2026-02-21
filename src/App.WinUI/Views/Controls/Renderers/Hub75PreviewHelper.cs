using Microsoft.Graphics.Canvas;
using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal static class Hub75PreviewHelper
{
    public const int PanelWidth = 64;
    public const int PanelHeight = 32;

    private static readonly IReadOnlyDictionary<char, string[]> Font5x7 = new Dictionary<char, string[]>
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
        [':'] = ["00000", "00100", "00100", "00000", "00100", "00100", "00000"],
        ['C'] = ["01110", "10001", "10000", "10000", "10000", "10001", "01110"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        [' '] = ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
    };

    public static void DrawPanel(in AppPreviewRenderContext context, out float ox, out float oy, out float pitch, out float ledSize)
    {
        pitch = MathF.Min((context.Width - 10f) / PanelWidth, (context.Height - 10f) / PanelHeight);
        pitch = MathF.Max(pitch, 1.2f);
        ledSize = MathF.Max(1f, pitch * 0.76f);

        var drawWidth = PanelWidth * pitch;
        var drawHeight = PanelHeight * pitch;
        ox = (context.Width - drawWidth) * 0.5f;
        oy = (context.Height - drawHeight) * 0.5f;

        var ds = context.DrawingSession;
        ds.FillRoundedRectangle(ox - 2f, oy - 2f, drawWidth + 4f, drawHeight + 4f, 3f, 3f, Color.FromArgb(255, 3, 5, 8));
        ds.DrawRoundedRectangle(ox - 1f, oy - 1f, drawWidth + 2f, drawHeight + 2f, 2f, 2f, Color.FromArgb(255, 24, 34, 44), 1f);
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

    public static void DrawText5x7(CanvasDrawingSession ds, float ox, float oy, float pitch, float ledSize, int x, int y, string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var cursor = x;
        foreach (var ch in text)
        {
            if (!Font5x7.TryGetValue(char.ToUpperInvariant(ch), out var glyph))
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

