using MicaAudio.Core.Presets;

namespace Output.Led;

// DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
internal static class LedFrameDeduplicator
{
    public static void EncodeToRgb565(ReadOnlySpan<RgbaColor> frame, Span<ushort> destination)
    {
        if (destination.Length < frame.Length)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));
        }

        for (var i = 0; i < frame.Length; i++)
        {
            var color = frame[i];
            var r = (ushort)((color.R >> 3) & 0x1F);
            var g = (ushort)((color.G >> 2) & 0x3F);
            var b = (ushort)((color.B >> 3) & 0x1F);
            destination[i] = (ushort)((r << 11) | (g << 5) | b);
        }
    }

    public static bool ShouldBroadcast(
        ReadOnlySpan<ushort> currentFrame,
        bool hasPreviousFrame,
        ReadOnlySpan<ushort> previousFrame,
        byte brightnessByte,
        byte previousBrightness)
    {
        if (!hasPreviousFrame || brightnessByte != previousBrightness)
        {
            return true;
        }

        return !currentFrame.SequenceEqual(previousFrame);
    }
}
