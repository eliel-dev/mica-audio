using MicaAudio.Core.Presets;
using Output.Led;

namespace Output.Tests;

public class LedFrameDeduplicatorTests
{
    [Fact]
    public void EncodeToRgb565_ShouldProduceExpectedPixels()
    {
        var frame = new[]
        {
            new RgbaColor(255, 0, 0, 255),
            new RgbaColor(0, 255, 0, 255),
            new RgbaColor(0, 0, 255, 255),
        };
        Span<ushort> encoded = stackalloc ushort[3];

        LedFrameDeduplicator.EncodeToRgb565(frame, encoded);

        Assert.Equal((ushort)0xF800, encoded[0]);
        Assert.Equal((ushort)0x07E0, encoded[1]);
        Assert.Equal((ushort)0x001F, encoded[2]);
    }

    [Fact]
    public void ShouldBroadcast_ShouldReturnFalseForSamePixelsAndBrightness()
    {
        ushort[] previous = [0xF800, 0x07E0];
        ushort[] current = [0xF800, 0x07E0];

        Assert.False(LedFrameDeduplicator.ShouldBroadcast(current, true, previous, 128, 128));
    }

    [Fact]
    public void ShouldBroadcast_ShouldReturnTrueWhenBrightnessChanges()
    {
        ushort[] previous = [0xF800, 0x07E0];
        ushort[] current = [0xF800, 0x07E0];

        Assert.True(LedFrameDeduplicator.ShouldBroadcast(current, true, previous, 64, 128));
    }
}
