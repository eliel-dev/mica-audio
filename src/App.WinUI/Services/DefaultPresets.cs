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
            CreatePreset(
                id: "audiomotion-sunset",
                name: "AudioMotion Sunset",
                glow: false,
                barWidth: 0.08f,
                stops:
                [
                    new PaletteStop { Offset = 0.00f, Color = new RgbaColor(255, 72, 40) },
                    new PaletteStop { Offset = 0.20f, Color = new RgbaColor(255, 128, 32) },
                    new PaletteStop { Offset = 0.40f, Color = new RgbaColor(255, 186, 52) },
                    new PaletteStop { Offset = 0.60f, Color = new RgbaColor(255, 108, 84) },
                    new PaletteStop { Offset = 0.80f, Color = new RgbaColor(196, 66, 255) },
                    new PaletteStop { Offset = 1.00f, Color = new RgbaColor(88, 54, 255) },
                ]),
            CreatePreset(
                id: "audiomotion-arctic",
                name: "AudioMotion Arctic",
                glow: false,
                barWidth: 0.08f,
                stops:
                [
                    new PaletteStop { Offset = 0.00f, Color = new RgbaColor(0, 214, 255) },
                    new PaletteStop { Offset = 0.20f, Color = new RgbaColor(0, 168, 255) },
                    new PaletteStop { Offset = 0.40f, Color = new RgbaColor(74, 224, 255) },
                    new PaletteStop { Offset = 0.60f, Color = new RgbaColor(144, 255, 219) },
                    new PaletteStop { Offset = 0.80f, Color = new RgbaColor(68, 128, 255) },
                    new PaletteStop { Offset = 1.00f, Color = new RgbaColor(144, 72, 255) },
                ]),
            CreatePreset(
                id: "audiomotion-neon",
                name: "AudioMotion Neon",
                glow: false,
                barWidth: 0.08f,
                stops:
                [
                    new PaletteStop { Offset = 0.00f, Color = new RgbaColor(57, 255, 20) },
                    new PaletteStop { Offset = 0.18f, Color = new RgbaColor(0, 255, 180) },
                    new PaletteStop { Offset = 0.36f, Color = new RgbaColor(0, 220, 255) },
                    new PaletteStop { Offset = 0.54f, Color = new RgbaColor(45, 120, 255) },
                    new PaletteStop { Offset = 0.72f, Color = new RgbaColor(196, 80, 255) },
                    new PaletteStop { Offset = 0.88f, Color = new RgbaColor(255, 46, 178) },
                    new PaletteStop { Offset = 1.00f, Color = new RgbaColor(255, 120, 64) },
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
