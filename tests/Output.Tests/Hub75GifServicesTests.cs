using App.WinUI.Services.Gif;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using System.Runtime.Versioning;

namespace Output.Tests;

[SupportedOSPlatform("windows")]
public sealed class Hub75GifServicesTests
{
    private const string SinglePixelGifBase64 = "R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==";
    private const string AnimatedDisposalGifBase64 = "R0lGODlhBAAEAPAAAAAAAP8AACH/C05FVFNDQVBFMi4wAwEAAAAh+QQJCgAAACwAAAAABAAEAAACBUwAhstQACH5BAkKAAAALAAAAAAEAAQAAAIFBBKGxlwAOw==";

    [Fact]
    public void Hub75FrameFormatter_Fit_ShouldPreserveAspectWithBorders()
    {
        var formatter = new Hub75FrameFormatter();
        var source = new RgbaColor[4 * 4];
        Array.Fill(source, new RgbaColor(255, 0, 0, 255));
        var frame = new DecodedGifFrame(4, 4, source);

        var output = formatter.Format(frame, GifScaleMode.Fit);

        Assert.Equal(128 * 64, output.Length);
        Assert.Equal(new RgbaColor(0, 0, 0, 255), output[(32 * 128) + 0]);
        Assert.Equal(new RgbaColor(255, 0, 0, 255), output[(32 * 128) + 32]);
        Assert.Equal(new RgbaColor(255, 0, 0, 255), output[(32 * 128) + 95]);
        Assert.Equal(new RgbaColor(0, 0, 0, 255), output[(32 * 128) + 127]);
    }

    [Fact]
    public void Hub75FrameFormatter_Fill_ShouldCropCenter()
    {
        var formatter = new Hub75FrameFormatter();
        var source = new[]
        {
            new RgbaColor(255, 0, 0, 255), new RgbaColor(255, 0, 0, 255), new RgbaColor(255, 0, 0, 255), new RgbaColor(255, 0, 0, 255),
            new RgbaColor(0, 255, 0, 255), new RgbaColor(0, 255, 0, 255), new RgbaColor(0, 255, 0, 255), new RgbaColor(0, 255, 0, 255),
            new RgbaColor(0, 0, 255, 255), new RgbaColor(0, 0, 255, 255), new RgbaColor(0, 0, 255, 255), new RgbaColor(0, 0, 255, 255),
            new RgbaColor(255, 255, 0, 255), new RgbaColor(255, 255, 0, 255), new RgbaColor(255, 255, 0, 255), new RgbaColor(255, 255, 0, 255),
        };
        var frame = new DecodedGifFrame(4, 4, source);

        var output = formatter.Format(frame, GifScaleMode.Fill);

        Assert.Equal(128 * 64, output.Length);
        Assert.Equal(new RgbaColor(0, 255, 0, 255), output[0]);
        Assert.Equal(new RgbaColor(0, 0, 255, 255), output[(63 * 128) + 127]);
    }

    [Fact]
    public void Hub75FrameFormatter_Stretch_ShouldOccupyWholeArea()
    {
        var formatter = new Hub75FrameFormatter();
        var source = new[]
        {
            new RgbaColor(255, 0, 0, 255),
            new RgbaColor(0, 0, 255, 255),
        };
        var frame = new DecodedGifFrame(2, 1, source);

        var output = formatter.Format(frame, GifScaleMode.Stretch);

        Assert.Equal(new RgbaColor(255, 0, 0, 255), output[(20 * 128) + 20]);
        Assert.Equal(new RgbaColor(0, 0, 255, 255), output[(20 * 128) + 100]);
    }

    [Fact]
    public void Hub75GifDecoder_InvalidSignature_ShouldThrow()
    {
        var decoder = new Hub75GifDecoder();
        var invalidBytes = System.Text.Encoding.ASCII.GetBytes("not-a-gif-content");

        Assert.Throws<InvalidDataException>(() => decoder.Decode(invalidBytes));
    }

    [Fact]
    public void Hub75GifDecoder_StaticGifWithoutDelay_ShouldUseDefaultDuration()
    {
        var decoder = new Hub75GifDecoder();
        var gifBytes = Convert.FromBase64String(SinglePixelGifBase64);

        var frames = decoder.Decode(gifBytes);

        var frame = Assert.Single(frames);
        Assert.Equal(Hub75GifDecoder.DefaultFrameDurationMs, frame.DurationMs);
    }

    [Fact]
    public void Hub75GifDecoder_AnimatedGifWithBackgroundDisposal_ShouldNotKeepPixelsFromPreviousFrame()
    {
        var decoder = new Hub75GifDecoder();
        var gifBytes = Convert.FromBase64String(AnimatedDisposalGifBase64);

        var frames = decoder.Decode(gifBytes);

        Assert.Equal(2, frames.Count);

        var first = frames[0];
        var second = frames[1];

        Assert.Equal(new RgbaColor(255, 0, 0, 255), first.Pixels[0]);
        Assert.Equal(new RgbaColor(0, 0, 0, 0), second.Pixels[0]);
        Assert.Equal(new RgbaColor(255, 0, 0, 255), second.Pixels[2]);
    }

    [Theory]
    [InlineData(-1, Hub75GifDecoder.DefaultFrameDurationMs)]
    [InlineData(0, Hub75GifDecoder.DefaultFrameDurationMs)]
    [InlineData(1, 10)]
    [InlineData(7, 70)]
    public void Hub75GifDecoder_NormalizeFrameDurationMs_ShouldConvertHundredthsToMilliseconds(int rawDelayUnits, int expectedDurationMs)
    {
        var durationMs = Hub75GifDecoder.NormalizeFrameDurationMs(rawDelayUnits);

        Assert.Equal(expectedDurationMs, durationMs);
    }
}
