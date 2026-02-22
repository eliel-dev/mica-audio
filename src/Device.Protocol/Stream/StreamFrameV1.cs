using System.Buffers.Binary;

namespace Device.Protocol.Stream;

// DOCS: docs/wiki/reference/ws-protocol-v1.md#estrutura-streamframev1
public static class StreamFrameV1
{
    public const byte Version = 1;
    public const byte MessageTypeBins64 = 1;
    public const byte MessageTypeFrame64x32Rgb565 = 2;
    public const int PayloadSize = 81;
    public const int PixelCount64x32 = 64 * 32;
    public const int PayloadSizeFrame64x32Rgb565 = 4112;

    // DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
    public static byte[] Create(
        uint sequence,
        long timestampQpc,
        byte level0To255,
        ReadOnlySpan<byte> bins64,
        byte brightness0To255,
        byte flags = 0)
    {
        if (bins64.Length != 64)
        {
            throw new ArgumentException("bins64 must have exactly 64 values.", nameof(bins64));
        }

        var payload = new byte[PayloadSize];
        payload[0] = Version;
        payload[1] = MessageTypeBins64;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(2, 4), sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(6, 8), unchecked((ulong)timestampQpc));

        payload[14] = level0To255;
        bins64.CopyTo(payload.AsSpan(15, 64));
        payload[79] = brightness0To255;
        payload[80] = flags;
        return payload;
    }

    // DOCS: docs/wiki/reference/ws-protocol-v1.md#estrutura-streamframev1-rgb565
    public static byte[] CreateFrame64x32Rgb565(
        uint sequence,
        long timestampQpc,
        ReadOnlySpan<ushort> pixels64x32Rgb565,
        byte brightness0To255,
        byte flags = 0)
    {
        if (pixels64x32Rgb565.Length != PixelCount64x32)
        {
            throw new ArgumentException("pixels64x32Rgb565 must have exactly 2048 values.", nameof(pixels64x32Rgb565));
        }

        var payload = new byte[PayloadSizeFrame64x32Rgb565];
        payload[0] = Version;
        payload[1] = MessageTypeFrame64x32Rgb565;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(2, 4), sequence);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(6, 8), unchecked((ulong)timestampQpc));

        payload[14] = brightness0To255;

        var offset = 15;
        for (var i = 0; i < pixels64x32Rgb565.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset, 2), pixels64x32Rgb565[i]);
            offset += 2;
        }

        payload[^1] = flags;
        return payload;
    }
}
