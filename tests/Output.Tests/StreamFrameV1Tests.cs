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
}
