namespace MicaAudio.Core.Led;

public sealed class LedOutputConfig
{
    public int Width { get; init; } = LedDefaults.MatrixWidth;

    public int Height { get; init; } = LedDefaults.MatrixHeight;

    public float Brightness { get; init; } = LedDefaults.Brightness;
}
