using Device.Protocol.Stream;

namespace Output.Tests;

public sealed class StreamFrameV3Tests
{
    [Fact]
    public void CreateBins128_ShouldGenerateExpectedLayout()
    {
        Span<byte> bins = stackalloc byte[StreamFrameV3.BinCount128];
        for (var i = 0; i < bins.Length; i++)
        {
            bins[i] = (byte)i;
        }

        var payload = StreamFrameV3.CreateBins128(
            sequence: 7,
            ownerEpoch: 3,
            timestampQpc: 123456,
            level0To255: 200,
            bins128: bins,
            brightness0To255: 128,
            flags: 9);

        Assert.Equal(StreamFrameV3.PayloadSizeBins128, payload.Length);
        Assert.Equal(StreamFrameV3.Version, payload[0]);
        Assert.Equal(StreamFrameV3.MessageTypeBins128, payload[1]);
        Assert.Equal((byte)3, payload[6]);
        Assert.Equal((byte)200, payload[18]);
        Assert.Equal((byte)0, payload[19]);
        Assert.Equal((byte)127, payload[146]);
        Assert.Equal((byte)128, payload[147]);
        Assert.Equal((byte)9, payload[148]);
    }

    [Fact]
    public void CreateFrame128x64Rgb565_ShouldGenerateExpectedLayout()
    {
        Span<ushort> pixels = stackalloc ushort[StreamFrameV3.PixelCount128x64];
        pixels[0] = 0xF800;
        pixels[1] = 0x07E0;
        pixels[2] = 0x001F;

        var payload = StreamFrameV3.CreateFrame128x64Rgb565(
            sequence: 9,
            ownerEpoch: 4,
            timestampQpc: 654321,
            pixels128x64Rgb565: pixels,
            brightness0To255: 255,
            flags: 5);

        Assert.Equal(StreamFrameV3.PayloadSizeFrame128x64Rgb565, payload.Length);
        Assert.Equal(StreamFrameV3.Version, payload[0]);
        Assert.Equal(StreamFrameV3.MessageTypeFrame128x64Rgb565, payload[1]);
        Assert.Equal((byte)4, payload[6]);
        Assert.Equal((byte)255, payload[18]);
        Assert.Equal((byte)0x00, payload[19]);
        Assert.Equal((byte)0xF8, payload[20]);
        Assert.Equal((byte)0xE0, payload[21]);
        Assert.Equal((byte)0x07, payload[22]);
        Assert.Equal((byte)0x1F, payload[23]);
        Assert.Equal((byte)0x00, payload[24]);
        Assert.Equal((byte)5, payload[^1]);
    }
}
