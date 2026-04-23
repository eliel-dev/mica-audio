using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Device.Protocol.Stream;

// DOCS: docs/wiki/reference/ws-protocol-v2.md#udp-visual-v1
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public static class VisualUdpFrameV1
{
    public const int HeaderSize = 12;
    public const int TagSize = 16;
    public const byte Version = 1;
    public const byte Magic0 = (byte)'M';
    public const byte Magic1 = (byte)'I';
    public const byte Magic2 = (byte)'C';
    public const byte Magic3 = (byte)'A';

    public static int GetDatagramSize(int payloadLength)
        => HeaderSize + Math.Max(0, payloadLength) + TagSize;

    public static bool TryWriteDatagram(
        Span<byte> destination,
        ReadOnlySpan<byte> payload,
        string token,
        out int written)
    {
        written = 0;
        if (!IsSupportedPayload(payload) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var totalLength = GetDatagramSize(payload.Length);
        if (destination.Length < totalLength)
        {
            return false;
        }

        var sequence = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(2, 4));
        destination[0] = Magic0;
        destination[1] = Magic1;
        destination[2] = Magic2;
        destination[3] = Magic3;
        destination[4] = Version;
        destination[5] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(6, 4), sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10, 2), checked((ushort)payload.Length));
        payload.CopyTo(destination.Slice(HeaderSize, payload.Length));
        WriteTag(token, destination.Slice(0, HeaderSize + payload.Length), destination.Slice(HeaderSize + payload.Length, TagSize));
        written = totalLength;
        return true;
    }

    public static bool TryValidateDatagram(
        ReadOnlySpan<byte> datagram,
        string token,
        uint? lastAcceptedSequence,
        out uint sequence,
        out int payloadOffset,
        out int payloadLength)
    {
        sequence = 0;
        payloadOffset = 0;
        payloadLength = 0;
        if (string.IsNullOrWhiteSpace(token)
            || datagram.Length < HeaderSize + TagSize
            || datagram[0] != Magic0
            || datagram[1] != Magic1
            || datagram[2] != Magic2
            || datagram[3] != Magic3
            || datagram[4] != Version)
        {
            return false;
        }

        sequence = BinaryPrimitives.ReadUInt32LittleEndian(datagram.Slice(6, 4));
        if (lastAcceptedSequence.HasValue && sequence <= lastAcceptedSequence.Value)
        {
            return false;
        }

        payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(datagram.Slice(10, 2));
        payloadOffset = HeaderSize;
        var payloadEnd = payloadOffset + payloadLength;
        var tagEnd = payloadEnd + TagSize;
        if (payloadLength <= 0 || tagEnd != datagram.Length)
        {
            return false;
        }

        var payload = datagram.Slice(payloadOffset, payloadLength);
        if (!IsSupportedPayload(payload))
        {
            return false;
        }

        Span<byte> expectedTag = stackalloc byte[TagSize];
        WriteTag(token, datagram.Slice(0, payloadEnd), expectedTag);
        return CryptographicOperations.FixedTimeEquals(expectedTag, datagram.Slice(payloadEnd, TagSize));
    }

    public static bool IsSupportedPayload(ReadOnlySpan<byte> payload)
        => payload.Length == StreamFrameV2.PayloadSizeBins128
           && payload[0] == StreamFrameV2.Version
           && payload[1] == StreamFrameV2.MessageTypeBins128;

    private static void WriteTag(string token, ReadOnlySpan<byte> data, Span<byte> destination)
    {
        Span<byte> hash = stackalloc byte[32];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), data, hash);
        hash[..TagSize].CopyTo(destination);
    }
}
