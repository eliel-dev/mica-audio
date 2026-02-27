using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Services;

internal static class DefaultPresets
{
    private const int CurrentSchemaVersion = 7;

    public static IReadOnlyList<PresetDefinition> Create()
    {
        return
        [
            CreateAudioMotionClonePreset("audiomotion-clone", "AudioMotion Clone", CreateRainbowStops()),
            CreateAudioMotionClonePreset("audiomotion-sunset", "AudioMotion Sunset", CreateSunsetStops()),
            CreateAudioMotionClonePreset("audiomotion-arctic", "AudioMotion Arctic", CreateArcticStops()),
            CreateAudioMotionClonePreset("audiomotion-neon", "AudioMotion Neon", CreateNeonStops()),

            CreateRendererPreset(
                id: "spectrum-vizzy-blob-neon",
                name: "Blob Neon",
                rendererId: RendererIds.VizzyBlobNeon,
                paletteStops: CreateVizzyBlobStops(),
                glow: true,
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["blobBaseRadius"] = 0.28f,
                    ["blobAudioDepth"] = 0.11f,
                    ["blobLfoDepth"] = 0.03f,
                    ["blobLfoSpeed"] = 1.20f,
                    ["blobPointCount"] = 96f,
                    ["blobGlowPasses"] = 3f,
                    ["blobStrokeWidth"] = 2.0f,
                    ["blobCoreAlpha"] = 0.92f,
                    ["blobGlowAlpha"] = 0.24f,
                    ["blobRotationSpeed"] = 0.18f,
                }),
            CreateRendererPreset(
                id: "spectrum-vizzy-orbit-rings",
                name: "Orbit Rings",
                rendererId: RendererIds.VizzyOrbitRings,
                paletteStops: CreateVizzyOrbitStops(),
                glow: true,
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["orbitRingCount"] = 4f,
                    ["orbitBaseRadius"] = 0.24f,
                    ["orbitRingSpacing"] = 0.03f,
                    ["orbitAudioDepth"] = 0.07f,
                    ["orbitLfoDepth"] = 0.02f,
                    ["orbitRotationSpeed"] = 0.35f,
                    ["orbitPointCount"] = 96f,
                    ["orbitGlowPasses"] = 2f,
                    ["orbitLineThickness"] = 2.2f,
                    ["orbitCoreCircleAlpha"] = 0.90f,
                }),
            CreateRendererPreset(
                id: "spectrum-vizzy-hyper-tunnel",
                name: "Hyper Tunnel",
                rendererId: RendererIds.VizzyHyperTunnel,
                paletteStops: CreateVizzyHyperTunnelStops(),
                glow: true,
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tunnelBaseRadius"] = 0.22f,
                    ["tunnelDepth"] = 1.00f,
                    ["tunnelSpeed"] = 1.10f,
                    ["tunnelWarp"] = 0.14f,
                    ["tunnelTwist"] = 0.35f,
                    ["tunnelGlowPasses"] = 3f,
                    ["tunnelLineThickness"] = 2.0f,
                    ["tunnelFogAmount"] = 0.18f,
                    ["tunnelSliceCount"] = 72f,
                    ["tunnelSegmentCount"] = 84f,
                }),

            CreateRendererPreset(
                id: "spectrum-bars",
                name: "Bars Arco-iris",
                rendererId: RendererIds.Bars,
                paletteStops: CreateRainbowStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["barWidth"] = 0.72f,
                    ["decay"] = 0.92f,
                    ["peakHoldMs"] = 240f,
                }),
            CreateRendererPreset(
                id: "spectrum-centered-lines",
                name: "Centered Lines",
                rendererId: RendererIds.CenterBars,
                paletteStops: CreateRainbowStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["lineMode"] = 1f,
                    ["lineThickness"] = 3f,
                    ["barWidth"] = 0.12f,
                    ["gap"] = 0.70f,
                    ["heightScale"] = 0.88f,
                    ["minHalfHeight"] = 0f,
                }),
            CreateRendererPreset(
                id: "spectrum-centered-blocks",
                name: "Centered Blocks",
                rendererId: RendererIds.CenterBars,
                paletteStops: CreateSunsetStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["lineMode"] = 0f,
                    ["barWidth"] = 0.80f,
                    ["gap"] = 0.16f,
                    ["heightScale"] = 0.86f,
                }),
            CreateRendererPreset(
                id: "spectrum-led",
                name: "LED Segmentado",
                rendererId: RendererIds.LedBars,
                paletteStops: CreateRainbowStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["decay"] = 0.88f,
                    ["peakHoldMs"] = 260f,
                }),
            CreateRendererPreset(
                id: "spectrum-line",
                name: "Linha Fluida",
                rendererId: RendererIds.Line,
                paletteStops: CreateArcticStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["lineThickness"] = 2.4f,
                    ["decay"] = 0.90f,
                }),
            CreateRendererPreset(
                id: "spectrum-area",
                name: "Area Fluida",
                rendererId: RendererIds.Area,
                paletteStops: CreateSunsetStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["decay"] = 0.90f,
                }),
            CreateRendererPreset(
                id: "spectrum-mirror",
                name: "Mirror",
                rendererId: RendererIds.Mirror,
                paletteStops: CreateRainbowStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["barWidth"] = 0.76f,
                    ["decay"] = 0.89f,
                }),
            CreateRendererPreset(
                id: "spectrum-reflection",
                name: "Reflection",
                rendererId: RendererIds.Reflection,
                paletteStops: CreateArcticStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["barWidth"] = 0.74f,
                }),
            CreateRendererPreset(
                id: "spectrum-peaks",
                name: "Peak Dots",
                rendererId: RendererIds.Peaks,
                paletteStops: CreateNeonStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["peakHoldMs"] = 280f,
                    ["decay"] = 0.86f,
                }),
            CreateRendererPreset(
                id: "spectrum-radial",
                name: "Radial Halo",
                rendererId: RendererIds.Radial,
                paletteStops: CreateRainbowStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["lineThickness"] = 2f,
                    ["decay"] = 0.90f,
                }),
            CreateRendererPreset(
                id: "spectrum-spectrogram",
                name: "Spectrogram",
                rendererId: RendererIds.Spectrogram,
                paletteStops: CreateRainbowStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["decay"] = 0.90f,
                }),
            CreateRendererPreset(
                id: "spectrum-waterfall",
                name: "Waterfall",
                rendererId: RendererIds.Waterfall,
                paletteStops: CreateRainbowStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["decay"] = 0.90f,
                }),
            CreateRendererPreset(
                id: "spectrum-pulse",
                name: "Pulse Aura",
                rendererId: RendererIds.Pulse,
                paletteStops: CreateNeonStops(),
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["lineThickness"] = 2.2f,
                    ["decay"] = 0.90f,
                }),
            CreateRendererPreset(
                id: "spectrum-neon-glow",
                name: "Neon Glow",
                rendererId: RendererIds.NeonGlow,
                paletteStops: CreateNeonStops(),
                glow: true,
                paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    ["barWidth"] = 0.66f,
                    ["decay"] = 0.88f,
                }),
        ];
    }

    private static PresetDefinition CreateAudioMotionClonePreset(string id, string name, IReadOnlyList<PaletteStop> paletteStops)
    {
        return CreateRendererPreset(
            id: id,
            name: name,
            rendererId: RendererIds.AudioMotionClone,
            paletteStops: paletteStops,
            paramOverrides: new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["lineThickness"] = 3f,
                ["lineMode"] = 1f,
                ["heightScale"] = 0.86f,
                ["minHalfHeight"] = 0f,
            });
    }

    private static PresetDefinition CreateRendererPreset(
        string id,
        string name,
        string rendererId,
        IReadOnlyList<PaletteStop> paletteStops,
        bool glow = false,
        int displayBandCount = 38,
        IReadOnlyDictionary<string, float>? paramOverrides = null)
    {
        var parameters = CreateBaseRendererParameters();
        if (paramOverrides is not null)
        {
            foreach (var pair in paramOverrides)
            {
                parameters[pair.Key] = pair.Value;
            }
        }

        return new PresetDefinition
        {
            SchemaVersion = CurrentSchemaVersion,
            PresetId = id,
            Name = name,
            RendererId = rendererId,
            DisplayBandCount = displayBandCount,
            ScaleMode = ScaleMode.Linear,
            FpsTarget = 60,
            Layout = LayoutMode.Normal,
            EnableGlow = glow,
            RendererParameters = parameters,
            Palette = new GradientPalette
            {
                Name = name,
                Stops = paletteStops,
            },
        };
    }

    private static Dictionary<string, float> CreateBaseRendererParameters()
    {
        return new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["barWidth"] = 0.08f,
            ["barSpace"] = 0.10f,
            ["gap"] = 0.16f,
            ["decay"] = 0.90f,
            ["peakHoldMs"] = 220f,
            ["lineThickness"] = 3f,
            ["lineMode"] = 1f,
            ["heightScale"] = 0.86f,
            ["minHalfHeight"] = 0f,
        };
    }

    private static IReadOnlyList<PaletteStop> CreateRainbowStops()
    {
        return
        [
            new PaletteStop { Offset = 0.00f, Color = new RgbaColor(255, 30, 30) },
            new PaletteStop { Offset = 0.16f, Color = new RgbaColor(255, 116, 0) },
            new PaletteStop { Offset = 0.33f, Color = new RgbaColor(255, 236, 0) },
            new PaletteStop { Offset = 0.50f, Color = new RgbaColor(0, 255, 80) },
            new PaletteStop { Offset = 0.67f, Color = new RgbaColor(0, 220, 255) },
            new PaletteStop { Offset = 0.83f, Color = new RgbaColor(0, 80, 255) },
            new PaletteStop { Offset = 1.00f, Color = new RgbaColor(255, 0, 255) },
        ];
    }

    private static IReadOnlyList<PaletteStop> CreateSunsetStops()
    {
        return
        [
            new PaletteStop { Offset = 0.00f, Color = new RgbaColor(255, 72, 40) },
            new PaletteStop { Offset = 0.20f, Color = new RgbaColor(255, 128, 32) },
            new PaletteStop { Offset = 0.40f, Color = new RgbaColor(255, 186, 52) },
            new PaletteStop { Offset = 0.60f, Color = new RgbaColor(255, 108, 84) },
            new PaletteStop { Offset = 0.80f, Color = new RgbaColor(196, 66, 255) },
            new PaletteStop { Offset = 1.00f, Color = new RgbaColor(88, 54, 255) },
        ];
    }

    private static IReadOnlyList<PaletteStop> CreateArcticStops()
    {
        return
        [
            new PaletteStop { Offset = 0.00f, Color = new RgbaColor(0, 214, 255) },
            new PaletteStop { Offset = 0.20f, Color = new RgbaColor(0, 168, 255) },
            new PaletteStop { Offset = 0.40f, Color = new RgbaColor(74, 224, 255) },
            new PaletteStop { Offset = 0.60f, Color = new RgbaColor(144, 255, 219) },
            new PaletteStop { Offset = 0.80f, Color = new RgbaColor(68, 128, 255) },
            new PaletteStop { Offset = 1.00f, Color = new RgbaColor(144, 72, 255) },
        ];
    }

    private static IReadOnlyList<PaletteStop> CreateNeonStops()
    {
        return
        [
            new PaletteStop { Offset = 0.00f, Color = new RgbaColor(57, 255, 20) },
            new PaletteStop { Offset = 0.18f, Color = new RgbaColor(0, 255, 180) },
            new PaletteStop { Offset = 0.36f, Color = new RgbaColor(0, 220, 255) },
            new PaletteStop { Offset = 0.54f, Color = new RgbaColor(45, 120, 255) },
            new PaletteStop { Offset = 0.72f, Color = new RgbaColor(196, 80, 255) },
            new PaletteStop { Offset = 0.88f, Color = new RgbaColor(255, 46, 178) },
            new PaletteStop { Offset = 1.00f, Color = new RgbaColor(255, 120, 64) },
        ];
    }

    private static IReadOnlyList<PaletteStop> CreateVizzyBlobStops()
    {
        return
        [
            new PaletteStop { Offset = 0.00f, Color = new RgbaColor(220, 232, 255) },
            new PaletteStop { Offset = 0.50f, Color = new RgbaColor(242, 246, 255) },
            new PaletteStop { Offset = 1.00f, Color = new RgbaColor(255, 255, 255) },
        ];
    }

    private static IReadOnlyList<PaletteStop> CreateVizzyOrbitStops()
    {
        return
        [
            new PaletteStop { Offset = 0.00f, Color = new RgbaColor(255, 255, 255) },
            new PaletteStop { Offset = 0.45f, Color = new RgbaColor(255, 188, 188) },
            new PaletteStop { Offset = 1.00f, Color = new RgbaColor(255, 110, 110) },
        ];
    }

    private static IReadOnlyList<PaletteStop> CreateVizzyHyperTunnelStops()
    {
        return
        [
            new PaletteStop { Offset = 0.00f, Color = new RgbaColor(255, 120, 38) },
            new PaletteStop { Offset = 0.30f, Color = new RgbaColor(255, 158, 72) },
            new PaletteStop { Offset = 0.62f, Color = new RgbaColor(70, 216, 232) },
            new PaletteStop { Offset = 1.00f, Color = new RgbaColor(0, 176, 220) },
        ];
    }
}
