using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Services;

internal static class DefaultPresets
{
    public static IReadOnlyList<PresetDefinition> Create()
    {
        return
        [
            CreatePreset(
                id: "audiomotion-clone",
                name: "AudioMotion Clone",
                glow: false,
                barWidth: 0.08f,
                stops:
                [
                    new PaletteStop { Offset = 0.00f, Color = new RgbaColor(255, 30, 30) },
                    new PaletteStop { Offset = 0.16f, Color = new RgbaColor(255, 116, 0) },
                    new PaletteStop { Offset = 0.33f, Color = new RgbaColor(255, 236, 0) },
                    new PaletteStop { Offset = 0.50f, Color = new RgbaColor(0, 255, 80) },
                    new PaletteStop { Offset = 0.67f, Color = new RgbaColor(0, 220, 255) },
                    new PaletteStop { Offset = 0.83f, Color = new RgbaColor(0, 80, 255) },
                    new PaletteStop { Offset = 1.00f, Color = new RgbaColor(255, 0, 255) },
                ]),
        ];
    }

    private static PresetDefinition CreatePreset(
        string id,
        string name,
        bool glow,
        float barWidth,
        IReadOnlyList<PaletteStop> stops)
    {
        return new PresetDefinition
        {
            SchemaVersion = 5,
            PresetId = id,
            Name = name,
            RendererId = RendererIds.AudioMotionClone,
            DisplayBandCount = 38,
            ScaleMode = ScaleMode.Linear,
            FpsTarget = 60,
            Layout = LayoutMode.Normal,
            EnableGlow = glow,
            RendererParameters = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["barWidth"] = barWidth,
                ["barSpace"] = 0.10f,
                ["decay"] = 0.90f,
                ["peakHoldMs"] = 220f,
                ["lineThickness"] = 3f,
                ["lineMode"] = 1f,
                ["heightScale"] = 0.86f,
                ["minHalfHeight"] = 0f,
            },
            Palette = new GradientPalette
            {
                Name = name,
                Stops = stops,
            },
        };
    }
}
