using MicaAudio.Core.Audio;

namespace MicaAudio.Core.Presets;

public sealed class PresetDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public string PresetId { get; init; } = "audiomotion-clone";

    public string Name { get; init; } = "AudioMotion Clone";

    public string RendererId { get; init; } = "audiomotion-clone";

    public int DisplayBandCount { get; init; } = 38;

    public ScaleMode ScaleMode { get; init; } = ScaleMode.Linear;

    public int FpsTarget { get; init; } = 60;

    public LayoutMode Layout { get; init; } = LayoutMode.Normal;

    public bool EnableGlow { get; init; }

    public Dictionary<string, float> RendererParameters { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["barWidth"] = 0.06f,
        ["gap"] = 0.88f,
        ["decay"] = 0.92f,
        ["peakHoldMs"] = 250f,
        ["lineThickness"] = 3f,
        ["lineMode"] = 1f,
        ["heightScale"] = 0.78f,
        ["minHalfHeight"] = 0f,
    };

    public GradientPalette Palette { get; init; } = new();
}
