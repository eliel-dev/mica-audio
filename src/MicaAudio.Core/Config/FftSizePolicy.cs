namespace MicaAudio.Core.Config;

public static class FftSizePolicy
{
    public static readonly int[] UiSupportedSizes =
    [
        1024, 2048, 4096, 8192,
    ];

    public static int CoerceUiSize(int value)
    {
        if (value <= 0)
        {
            return 2048;
        }

        if (value >= UiSupportedSizes[^1])
        {
            return UiSupportedSizes[^1];
        }

        if (Array.IndexOf(UiSupportedSizes, value) >= 0)
        {
            return value;
        }

        var best = UiSupportedSizes[0];
        var bestDistance = global::System.Math.Abs(best - value);

        for (var i = 1; i < UiSupportedSizes.Length; i++)
        {
            var current = UiSupportedSizes[i];
            var distance = global::System.Math.Abs(current - value);
            if (distance < bestDistance)
            {
                best = current;
                bestDistance = distance;
            }
        }

        return best;
    }
}
