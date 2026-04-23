using Device.Protocol.Stream;

namespace Output.Tests;

public sealed class VisualUdpFrameV1Tests
{
    [Fact]
    public void TryWriteAndValidateDatagram_ShouldAcceptAuthenticatedBins128Payload()
    {
        var payload = CreateBinsPayload(sequence: 42);
        var datagram = new byte[VisualUdpFrameV1.GetDatagramSize(payload.Length)];

        Assert.True(VisualUdpFrameV1.TryWriteDatagram(datagram, payload, "device-token", out var written));
        Assert.Equal(datagram.Length, written);

        Assert.True(VisualUdpFrameV1.TryValidateDatagram(
            datagram,
            "device-token",
            lastAcceptedSequence: 41,
            out var sequence,
            out var payloadOffset,
            out var payloadLength));
        Assert.Equal(42u, sequence);
        Assert.Equal(payload.Length, payloadLength);
        Assert.Equal(payload, datagram.AsSpan(payloadOffset, payloadLength).ToArray());
    }

    [Fact]
    public void TryValidateDatagram_ShouldRejectInvalidHmacAndOldSequence()
    {
        var payload = CreateBinsPayload(sequence: 7);
        var datagram = new byte[VisualUdpFrameV1.GetDatagramSize(payload.Length)];
        Assert.True(VisualUdpFrameV1.TryWriteDatagram(datagram, payload, "device-token", out _));

        datagram[^1] ^= 0x01;
        Assert.False(VisualUdpFrameV1.TryValidateDatagram(
            datagram,
            "device-token",
            lastAcceptedSequence: null,
            out _,
            out _,
            out _));

        datagram[^1] ^= 0x01;
        Assert.False(VisualUdpFrameV1.TryValidateDatagram(
            datagram,
            "device-token",
            lastAcceptedSequence: 7,
            out _,
            out _,
            out _));
    }

    [Fact]
    public void TryWriteDatagram_ShouldRejectRawRgb565Payload()
    {
        var payload = StreamFrameV2.CreateFrame128x64Rgb565(
            sequence: 1,
            timestampQpc: 100,
            pixels128x64Rgb565: new ushort[StreamFrameV2.PixelCount128x64],
            brightness0To255: 255);
        var datagram = new byte[VisualUdpFrameV1.GetDatagramSize(payload.Length)];

        Assert.False(VisualUdpFrameV1.TryWriteDatagram(datagram, payload, "device-token", out _));
    }

    private static byte[] CreateBinsPayload(uint sequence)
    {
        Span<byte> bins = stackalloc byte[StreamFrameV2.BinCount128];
        for (var index = 0; index < bins.Length; index++)
        {
            bins[index] = (byte)index;
        }

        return StreamFrameV2.CreateBins128(
            sequence,
            timestampQpc: 123,
            level0To255: 200,
            bins,
            brightness0To255: 128);
    }
}
