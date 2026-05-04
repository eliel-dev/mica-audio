namespace MicaAudio.Core.Presets;

public sealed class GradientPalette
{
    public IReadOnlyList<PaletteStop> Stops { get; init; } =
    [
        new PaletteStop { Offset = 0f, Color = new RgbaColor(6, 12, 40) },
        new PaletteStop { Offset = 0.25f, Color = new RgbaColor(22, 73, 128) },
        new PaletteStop { Offset = 0.55f, Color = new RgbaColor(35, 179, 127) },
        new PaletteStop { Offset = 0.8f, Color = new RgbaColor(248, 191, 38) },
        new PaletteStop { Offset = 1f, Color = new RgbaColor(244, 81, 30) },
    ];
}
