using Device.Protocol.Stream;

namespace Output.Tests;

public class StreamFrameV1Tests
{
    [Fact]
    public void Create_ShouldGenerateExpectedLayout()
    {
        Span<byte> bins = stackalloc byte[64];
        for (var i = 0; i < bins.Length; i++)
        {
            bins[i] = (byte)i;
        }

        var payload = StreamFrameV1.Create(
            sequence: 7,
            timestampQpc: 123456789,
            level0To255: 200,
            bins64: bins,
            brightness0To255: 150,
            flags: 3);

        Assert.Equal(StreamFrameV1.PayloadSize, payload.Length);
        Assert.Equal(StreamFrameV1.Version, payload[0]);
        Assert.Equal(StreamFrameV1.MessageTypeBins64, payload[1]);
        Assert.Equal((byte)200, payload[14]);
        Assert.Equal((byte)0, payload[15]);
        Assert.Equal((byte)63, payload[78]);
        Assert.Equal((byte)150, payload[79]);
        Assert.Equal((byte)3, payload[80]);
    }

    [Fact]
    public void Create_WithInvalidBinCount_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            StreamFrameV1.Create(
                sequence: 1,
                timestampQpc: 1,
                level0To255: 1,
                bins64: new byte[63],
                brightness0To255: 1));

        Assert.Contains("bins64", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateFrame64x32Rgb565_ShouldGenerateExpectedLayout()
    {
        Span<ushort> pixels = stackalloc ushort[StreamFrameV1.PixelCount64x32];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (ushort)i;
        }

        var payload = StreamFrameV1.CreateFrame64x32Rgb565(
            sequence: 11,
            timestampQpc: 987654321,
            pixels64x32Rgb565: pixels,
            brightness0To255: 144,
            flags: 7);

        Assert.Equal(StreamFrameV1.PayloadSizeFrame64x32Rgb565, payload.Length);
        Assert.Equal(StreamFrameV1.Version, payload[0]);
        Assert.Equal(StreamFrameV1.MessageTypeFrame64x32Rgb565, payload[1]);
        Assert.Equal((byte)144, payload[14]);
        Assert.Equal((byte)0x00, payload[15]);
        Assert.Equal((byte)0x00, payload[16]);
        Assert.Equal((byte)0x01, payload[17]);
        Assert.Equal((byte)0x00, payload[18]);
        Assert.Equal((byte)0xFF, payload[4109]);
        Assert.Equal((byte)0x07, payload[4111]);
    }

    [Fact]
    public void CreateFrame64x32Rgb565_WithInvalidPixelCount_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            StreamFrameV1.CreateFrame64x32Rgb565(
                sequence: 1,
                timestampQpc: 1,
                pixels64x32Rgb565: new ushort[2047],
                brightness0To255: 1));

        Assert.Contains("pixels64x32Rgb565", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
