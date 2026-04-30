namespace MicaAudio.Core.Led;

// DOCS: docs/wiki/modules/output-led.md#modulo-output-led
public sealed class LedOutputConfig
{
    public int Width { get; init; } = LedDefaults.MatrixWidth;

    public int Height { get; init; } = LedDefaults.MatrixHeight;

    public float Brightness { get; init; } = LedDefaults.Brightness;

    public string? TargetDeviceId { get; init; }
}
