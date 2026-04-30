using MicaAudio.Core.Presets;

namespace MicaAudio.Core.Led;

public sealed class LedPayload
{
    private float[]? bins128;
    private RgbaColor[]? frame128x64;

    public float[]? Bins128
    {
        get => bins128;
        init => bins128 = value;
    }

    public float Level { get; init; }

    public string? PresetId { get; init; }

    public byte BinsFlags { get; init; }

    public RgbaColor[]? Frame128x64
    {
        get => frame128x64;
        init => frame128x64 = value;
    }

    [Obsolete("Use Bins128.")]
    public float[]? Bins64
    {
        get => bins128;
        init => bins128 = value;
    }

    [Obsolete("Use Frame128x64.")]
    public RgbaColor[]? Frame64x32
    {
        get => frame128x64;
        init => frame128x64 = value;
    }
}
