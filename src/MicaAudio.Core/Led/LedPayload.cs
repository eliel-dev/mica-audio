using MicaAudio.Core.Presets;

namespace MicaAudio.Core.Led;

public sealed class LedPayload
{
    public float[]? Bins64 { get; init; }

    public float Level { get; init; }

    public string? PresetId { get; init; }

    public RgbaColor[]? Frame64x32 { get; init; }
}
