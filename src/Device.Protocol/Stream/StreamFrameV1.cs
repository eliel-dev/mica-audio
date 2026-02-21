using System.Buffers.Binary;

namespace Device.Protocol.Stream;

// DOCS: docs/wiki/reference/ws-protocol-v1.md#estrutura-streamframev1
public static class StreamFrameV1
{
    public const byte Version = 1;
    public const byte MessageTypeBins64 = 1;
    public const int PayloadSize = 81;

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
}
