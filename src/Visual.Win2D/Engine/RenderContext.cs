using Microsoft.Graphics.Canvas;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;

namespace Visual.Win2D.Engine;

public sealed class RenderContext
{
    public required CanvasDrawingSession DrawingSession { get; init; }

    public required SpectrumFrame Frame { get; init; }

    public required PresetDefinition Preset { get; init; }

    public required PaletteSampler Palette { get; init; }

    public required float[] Peaks { get; init; }

    public required float Width { get; init; }

    public required float Height { get; init; }

    public required float DeltaSeconds { get; init; }

    public float Param(string name, float fallback)
    {
        if (Preset.RendererParameters.TryGetValue(name, out var value))
        {
            return value;
        }

        return fallback;
    }
}
