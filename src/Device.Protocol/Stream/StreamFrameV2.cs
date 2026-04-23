using System.Buffers.Binary;

namespace Device.Protocol.Stream;

// DOCS: docs/wiki/reference/ws-protocol-v2.md#estrutura-streamframev2
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public static class StreamFrameV2
{
    public const byte Version = 2;
    public const byte MessageTypeBins128 = 1;
    public const byte MessageTypeFrame128x64Rgb565 = 2;
    public const int BinCount128 = 128;
    public const int PixelCount128x64 = 128 * 64;
    public const int PayloadSizeBins128 = 145;
    public const int PayloadSizeFrame128x64Rgb565 = 16400;

    public static byte[] CreateBins128(
        uint sequence,
        long timestampQpc,
        byte level0To255,
        ReadOnlySpan<byte> bins128,
        byte brightness0To255,
        byte flags = 0)
    {
        if (bins128.Length != BinCount128)
        {
            throw new ArgumentException("bins128 must have exactly 128 values.", nameof(bins128));
        }

        var payload = new byte[PayloadSizeBins128];
        WriteBins128(payload, sequence, timestampQpc, level0To255, bins128, brightness0To255, flags);
        return payload;
    }

    public static void WriteBins128(
        Span<byte> destination,
        uint sequence,
        long timestampQpc,
        byte level0To255,
        ReadOnlySpan<byte> bins128,
        byte brightness0To255,
        byte flags = 0)
    {
        if (destination.Length < PayloadSizeBins128)
        {
            throw new ArgumentException("destination must have room for a Bins128 payload.", nameof(destination));
        }

        if (bins128.Length != BinCount128)
        {
            throw new ArgumentException("bins128 must have exactly 128 values.", nameof(bins128));
        }

        destination[0] = Version;
        destination[1] = MessageTypeBins128;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(6, 8), unchecked((ulong)timestampQpc));
        destination[14] = level0To255;
        bins128.CopyTo(destination.Slice(15, BinCount128));
        destination[143] = brightness0To255;
        destination[144] = flags;
    }

    public static byte[] CreateFrame128x64Rgb565(
        uint sequence,
        long timestampQpc,
        ReadOnlySpan<ushort> pixels128x64Rgb565,
        byte brightness0To255,
        byte flags = 0)
    {
        if (pixels128x64Rgb565.Length != PixelCount128x64)
        {
            throw new ArgumentException("pixels128x64Rgb565 must have exactly 8192 values.", nameof(pixels128x64Rgb565));
        }

        var payload = new byte[PayloadSizeFrame128x64Rgb565];
        WriteFrame128x64Rgb565(payload, sequence, timestampQpc, pixels128x64Rgb565, brightness0To255, flags);
        return payload;
    }

    public static void WriteFrame128x64Rgb565(
        Span<byte> destination,
        uint sequence,
        long timestampQpc,
        ReadOnlySpan<ushort> pixels128x64Rgb565,
        byte brightness0To255,
        byte flags = 0)
    {
        if (destination.Length < PayloadSizeFrame128x64Rgb565)
        {
            throw new ArgumentException("destination must have room for a 128x64 RGB565 payload.", nameof(destination));
        }

        if (pixels128x64Rgb565.Length != PixelCount128x64)
        {
            throw new ArgumentException("pixels128x64Rgb565 must have exactly 8192 values.", nameof(pixels128x64Rgb565));
        }

        destination[0] = Version;
        destination[1] = MessageTypeFrame128x64Rgb565;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(6, 8), unchecked((ulong)timestampQpc));
        destination[14] = brightness0To255;

        var offset = 15;
        for (var i = 0; i < pixels128x64Rgb565.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), pixels128x64Rgb565[i]);
            offset += 2;
        }

        destination[PayloadSizeFrame128x64Rgb565 - 1] = flags;
    }
}
