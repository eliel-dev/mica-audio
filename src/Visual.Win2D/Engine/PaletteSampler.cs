using MicaAudio.Core.Presets;
using Windows.UI;

namespace Visual.Win2D.Engine;

public sealed class PaletteSampler
{
    private readonly PaletteStop[] stops;

    public PaletteSampler(GradientPalette palette)
    {
        stops = palette.Stops.OrderBy(s => s.Offset).ToArray();
        if (stops.Length == 0)
        {
            stops =
            [
                new PaletteStop { Offset = 0f, Color = new RgbaColor(20, 20, 20) },
                new PaletteStop { Offset = 1f, Color = new RgbaColor(255, 255, 255) },
            ];
        }
    }

    public Color ColorAt(float t, float alphaScale = 1f)
    {
        t = Math.Clamp(t, 0f, 1f);

        PaletteStop left = stops[0];
        PaletteStop right = stops[^1];

        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].Offset)
            {
                left = stops[i - 1];
                right = stops[i];
                break;
            }
        }

        var range = Math.Max(0.0001f, right.Offset - left.Offset);
        var localT = Math.Clamp((t - left.Offset) / range, 0f, 1f);

        byte Lerp(byte a, byte b) => (byte)Math.Clamp(a + ((b - a) * localT), 0f, 255f);

        var a = (byte)Math.Clamp(Lerp(left.Color.A, right.Color.A) * alphaScale, 0f, 255f);
        return Color.FromArgb(
            a,
            Lerp(left.Color.R, right.Color.R),
            Lerp(left.Color.G, right.Color.G),
            Lerp(left.Color.B, right.Color.B));
    }
}
