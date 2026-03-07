using Windows.UI;

namespace App.WinUI.Views.Controls;

internal static class AppPreviewDrawHelpers
{
    public static Color AccentToColor(string? accent, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(accent))
        {
            return fallback;
        }

        try
        {
            if (accent.StartsWith('#') && accent.Length is 7 or 9)
            {
                var value = Convert.ToUInt32(accent.TrimStart('#'), 16);
                if (accent.Length == 7)
                {
                    var r = (byte)((value >> 16) & 0xFF);
                    var g = (byte)((value >> 8) & 0xFF);
                    var b = (byte)(value & 0xFF);
                    return Color.FromArgb(255, r, g, b);
                }

                var a = (byte)((value >> 24) & 0xFF);
                var r2 = (byte)((value >> 16) & 0xFF);
                var g2 = (byte)((value >> 8) & 0xFF);
                var b2 = (byte)(value & 0xFF);
                return Color.FromArgb(a, r2, g2, b2);
            }
        }
        catch
        {
            return fallback;
        }

        return fallback;
    }

    public static Color RainbowByFraction(float fraction)
    {
        var hue = Math.Clamp(fraction, 0f, 1f) * 360f;
        return HsvToRgb(hue, 0.85f, 0.98f);
    }

    public static Color HsvToRgb(float h, float s, float v)
    {
        h = (h % 360f + 360f) % 360f;
        s = Math.Clamp(s, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);

        var c = v * s;
        var x = c * (1f - MathF.Abs(((h / 60f) % 2f) - 1f));
        var m = v - c;

        var (r1, g1, b1) = h switch
        {
            < 60f => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _ => (c, 0f, x),
        };

        return Color.FromArgb(
            255,
            (byte)Math.Clamp((r1 + m) * 255f, 0f, 255f),
            (byte)Math.Clamp((g1 + m) * 255f, 0f, 255f),
            (byte)Math.Clamp((b1 + m) * 255f, 0f, 255f));
    }
}
