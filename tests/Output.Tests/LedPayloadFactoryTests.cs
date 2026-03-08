using MicaAudio.Core.Audio;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class LedPayloadFactoryTests
{
    [Fact]
    public void CreateSpectrumPayload_ShouldResampleDisplayBandsToMatrixWidth()
    {
        var spectrum = new SpectrumFrame([0f, 0.5f, 1f], [0f, 0.5f, 1f], 0.75f, 123);

        var payload = LedPayloadFactory.CreateSpectrumPayload(spectrum, "preset");

        Assert.NotNull(payload.Bins128);
        Assert.Equal(LedDefaults.MatrixWidth, payload.Bins128!.Length);
        Assert.Equal(0f, payload.Bins128[0]);
        Assert.Equal(1f, payload.Bins128[^1]);
        Assert.Equal(0.75f, payload.Level);
        Assert.Equal("preset", payload.PresetId);
    }

    [Fact]
    public void CreateFramePayload_ShouldKeepFrameAndMetadata()
    {
        var frame = Enumerable.Repeat(new RgbaColor(1, 2, 3, 4), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();

        var payload = LedPayloadFactory.CreateFramePayload(frame, "gif", 0.5f);

        Assert.Same(frame, payload.Frame128x64);
        Assert.Equal(0.5f, payload.Level);
        Assert.Equal("gif", payload.PresetId);
    }
}
