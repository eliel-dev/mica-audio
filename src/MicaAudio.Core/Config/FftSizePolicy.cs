namespace MicaAudio.Core.Config;

public static class FftSizePolicy
{
    public const int CanonicalSize = VisualizerRuntimeDefaults.DefaultFftSize;

    public static readonly int[] UiSupportedSizes =
    [
        CanonicalSize,
    ];

    public static int CoerceUiSize(int value)
        => CanonicalSize;
}
