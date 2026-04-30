using Device.Protocol.Stream;

namespace Output.Tests;

public class StreamFrameV2Tests
{
    [Fact]
    public void CreateBins128_ShouldGenerateExpectedLayout()
    {
        Span<byte> bins = stackalloc byte[StreamFrameV2.BinCount128];
        for (var i = 0; i < bins.Length; i++)
        {
            bins[i] = (byte)i;
        }

        var encodedFlags = Bins128VisualFlags.Create(
            Bins128VisualStyle.FlowLine,
            Bins128PaletteFamily.Arctic);

        var payload = StreamFrameV2.CreateBins128(
            sequence: 7,
            timestampQpc: 123456,
            level0To255: 200,
            bins128: bins,
            brightness0To255: 128,
            flags: encodedFlags);

        Assert.Equal(StreamFrameV2.PayloadSizeBins128, payload.Length);
        Assert.Equal(StreamFrameV2.Version, payload[0]);
        Assert.Equal(StreamFrameV2.MessageTypeBins128, payload[1]);
        Assert.Equal((byte)200, payload[14]);
        Assert.Equal((byte)0, payload[15]);
        Assert.Equal((byte)127, payload[142]);
        Assert.Equal((byte)128, payload[143]);
        Assert.Equal(encodedFlags, payload[144]);
    }

    [Fact]
    public void WriteBins128_ShouldMatchAllocatedPayloadLayout()
    {
        Span<byte> bins = stackalloc byte[StreamFrameV2.BinCount128];
        for (var i = 0; i < bins.Length; i++)
        {
            bins[i] = (byte)(255 - i);
        }

        var expected = StreamFrameV2.CreateBins128(
            sequence: 11,
            timestampQpc: 789,
            level0To255: 96,
            bins128: bins,
            brightness0To255: 77,
            flags: 3);
        Span<byte> destination = stackalloc byte[StreamFrameV2.PayloadSizeBins128];

        StreamFrameV2.WriteBins128(
            destination,
            sequence: 11,
            timestampQpc: 789,
            level0To255: 96,
            bins128: bins,
            brightness0To255: 77,
            flags: 3);

        Assert.Equal(expected, destination.ToArray());
    }

    [Fact]
    public void CreateFrame128x64Rgb565_ShouldGenerateExpectedLayout()
    {
        Span<ushort> pixels = stackalloc ushort[StreamFrameV2.PixelCount128x64];
        pixels[0] = 0xF800;
        pixels[1] = 0x07E0;
        pixels[2] = 0x001F;

        var payload = StreamFrameV2.CreateFrame128x64Rgb565(
            sequence: 9,
            timestampQpc: 654321,
            pixels128x64Rgb565: pixels,
            brightness0To255: 255,
            flags: 5);

        Assert.Equal(StreamFrameV2.PayloadSizeFrame128x64Rgb565, payload.Length);
        Assert.Equal(StreamFrameV2.Version, payload[0]);
        Assert.Equal(StreamFrameV2.MessageTypeFrame128x64Rgb565, payload[1]);
        Assert.Equal((byte)255, payload[14]);
        Assert.Equal((byte)0x00, payload[15]);
        Assert.Equal((byte)0xF8, payload[16]);
        Assert.Equal((byte)0xE0, payload[17]);
        Assert.Equal((byte)0x07, payload[18]);
        Assert.Equal((byte)0x1F, payload[19]);
        Assert.Equal((byte)0x00, payload[20]);
        Assert.Equal((byte)5, payload[^1]);
    }

    [Fact]
    public void WriteFrame128x64Rgb565_ShouldMatchAllocatedPayloadLayout()
    {
        Span<ushort> pixels = stackalloc ushort[StreamFrameV2.PixelCount128x64];
        pixels[0] = 0xF800;
        pixels[1] = 0x07E0;
        pixels[2] = 0x001F;
        pixels[^1] = 0xFFFF;

        var expected = StreamFrameV2.CreateFrame128x64Rgb565(
            sequence: 12,
            timestampQpc: 345,
            pixels128x64Rgb565: pixels,
            brightness0To255: 101,
            flags: 9);
        var destination = new byte[StreamFrameV2.PayloadSizeFrame128x64Rgb565];

        StreamFrameV2.WriteFrame128x64Rgb565(
            destination,
            sequence: 12,
            timestampQpc: 345,
            pixels128x64Rgb565: pixels,
            brightness0To255: 101,
            flags: 9);

        Assert.Equal(expected, destination);
    }
}
