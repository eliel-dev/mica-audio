using System.Buffers.Binary;

namespace Device.Protocol.Stream;

// DOCS: docs/wiki/reference/ws-protocol-v2.md#estrutura-streamframev3
// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md
public static class StreamFrameV3
{
    public const byte Version = 3;
    public const byte MessageTypeBins128 = StreamFrameV2.MessageTypeBins128;
    public const byte MessageTypeFrame128x64Rgb565 = StreamFrameV2.MessageTypeFrame128x64Rgb565;
    public const int BinCount128 = StreamFrameV2.BinCount128;
    public const int PixelCount128x64 = StreamFrameV2.PixelCount128x64;
    public const int PayloadSizeBins128 = 149;
    public const int PayloadSizeFrame128x64Rgb565 = 16404;

    public static byte[] CreateBins128(
        uint sequence,
        uint ownerEpoch,
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
        WriteBins128(payload, sequence, ownerEpoch, timestampQpc, level0To255, bins128, brightness0To255, flags);
        return payload;
    }

    public static void WriteBins128(
        Span<byte> destination,
        uint sequence,
        uint ownerEpoch,
        long timestampQpc,
        byte level0To255,
        ReadOnlySpan<byte> bins128,
        byte brightness0To255,
        byte flags = 0)
    {
        if (destination.Length < PayloadSizeBins128)
        {
            throw new ArgumentException("destination must have room for a V3 Bins128 payload.", nameof(destination));
        }

        if (bins128.Length != BinCount128)
        {
            throw new ArgumentException("bins128 must have exactly 128 values.", nameof(bins128));
        }

        destination[0] = Version;
        destination[1] = MessageTypeBins128;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(6, 4), ownerEpoch);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(10, 8), unchecked((ulong)timestampQpc));
        destination[18] = level0To255;
        bins128.CopyTo(destination.Slice(19, BinCount128));
        destination[147] = brightness0To255;
        destination[148] = flags;
    }

    public static byte[] CreateFrame128x64Rgb565(
        uint sequence,
        uint ownerEpoch,
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
        WriteFrame128x64Rgb565(payload, sequence, ownerEpoch, timestampQpc, pixels128x64Rgb565, brightness0To255, flags);
        return payload;
    }

    public static void WriteFrame128x64Rgb565(
        Span<byte> destination,
        uint sequence,
        uint ownerEpoch,
        long timestampQpc,
        ReadOnlySpan<ushort> pixels128x64Rgb565,
        byte brightness0To255,
        byte flags = 0)
    {
        if (destination.Length < PayloadSizeFrame128x64Rgb565)
        {
            throw new ArgumentException("destination must have room for a V3 128x64 RGB565 payload.", nameof(destination));
        }

        if (pixels128x64Rgb565.Length != PixelCount128x64)
        {
            throw new ArgumentException("pixels128x64Rgb565 must have exactly 8192 values.", nameof(pixels128x64Rgb565));
        }

        destination[0] = Version;
        destination[1] = MessageTypeFrame128x64Rgb565;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(6, 4), ownerEpoch);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(10, 8), unchecked((ulong)timestampQpc));
        destination[18] = brightness0To255;

        var offset = 19;
        for (var i = 0; i < pixels128x64Rgb565.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), pixels128x64Rgb565[i]);
            offset += 2;
        }

        destination[PayloadSizeFrame128x64Rgb565 - 1] = flags;
    }
}
