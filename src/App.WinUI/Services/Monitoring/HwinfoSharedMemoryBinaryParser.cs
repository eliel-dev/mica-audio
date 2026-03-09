using System.Buffers.Binary;
using System.Text;

namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal static class HwinfoSharedMemoryBinaryParser
{
    internal const string ActiveSignature = "HWiS";
    internal const string DeadSignature = "DEAD";
    internal const int HeaderSize = 44;
    internal const int SensorElementMinimumSize = 264;
    internal const int ReadingElementMinimumSize = 316;

    private const int FixedStringLength = 128;
    private const int UnitStringLength = 16;
    private const int MaximumSensorCount = 4096;
    private const int MaximumReadingCount = 32768;

    public static HwinfoSharedMemoryPayload Parse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderSize)
        {
            throw new InvalidDataException("O header do HWiNFO64 esta incompleto.");
        }

        var header = ParseHeaderOnly(buffer[..HeaderSize]);
        ValidateHeader(header, buffer.Length);

        return new HwinfoSharedMemoryPayload(
            header,
            ParseSensors(buffer, header),
            ParseReadings(buffer, header));
    }

    public static HwinfoSharedMemoryHeader ParseHeaderOnly(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderSize)
        {
            throw new InvalidDataException("O header do HWiNFO64 esta incompleto.");
        }

        return ParseHeader(header[..HeaderSize]);
    }

    private static HwinfoSharedMemoryHeader ParseHeader(ReadOnlySpan<byte> header)
    {
        var signature = ReadAscii(header[..4]);
        var pollTimeSeconds = ReadInt64(header, 12);
        DateTimeOffset? pollTimeUtc = pollTimeSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(pollTimeSeconds)
            : null;

        return new HwinfoSharedMemoryHeader(
            signature,
            version: ReadUInt32(header, 4),
            revision: ReadUInt32(header, 8),
            pollTimeUtc,
            sensorOffset: ReadUInt32(header, 20),
            sensorElementSize: ReadUInt32(header, 24),
            sensorCount: ReadUInt32(header, 28),
            readingOffset: ReadUInt32(header, 32),
            readingElementSize: ReadUInt32(header, 36),
            readingCount: ReadUInt32(header, 40));
    }

    private static void ValidateHeader(HwinfoSharedMemoryHeader header, int totalLength)
    {
        if (header.SensorCount > MaximumSensorCount || header.ReadingCount > MaximumReadingCount)
        {
            throw new InvalidDataException("O header do HWiNFO64 informou uma quantidade invalida de elementos.");
        }

        if (header.SensorElementSize < SensorElementMinimumSize)
        {
            throw new InvalidDataException("O header do HWiNFO64 informou um tamanho invalido de sensor.");
        }

        if (header.ReadingElementSize < ReadingElementMinimumSize)
        {
            throw new InvalidDataException("O header do HWiNFO64 informou um tamanho invalido de leitura.");
        }

        EnsureSectionWithinBounds(totalLength, header.SensorOffset, header.SensorElementSize, header.SensorCount, "sensor");
        EnsureSectionWithinBounds(totalLength, header.ReadingOffset, header.ReadingElementSize, header.ReadingCount, "reading");
    }

    private static void EnsureSectionWithinBounds(int totalLength, uint offset, uint elementSize, uint count, string sectionName)
    {
        var requiredLength = (long)offset + ((long)elementSize * count);
        if (requiredLength > totalLength)
        {
            throw new InvalidDataException($"A secao {sectionName} do HWiNFO64 excede o buffer informado.");
        }
    }

    private static List<HwinfoSensorEntry> ParseSensors(ReadOnlySpan<byte> buffer, HwinfoSharedMemoryHeader header)
    {
        if (header.SensorCount == 0)
        {
            return [];
        }

        var sensors = new List<HwinfoSensorEntry>((int)header.SensorCount);
        for (var index = 0; index < header.SensorCount; index++)
        {
            var offset = checked((int)(header.SensorOffset + (index * header.SensorElementSize)));
            var element = buffer.Slice(offset, (int)header.SensorElementSize);
            sensors.Add(new HwinfoSensorEntry(
                sensorId: ReadUInt32(element, 0),
                sensorInstance: ReadUInt32(element, 4),
                originalName: ReadAscii(element.Slice(8, FixedStringLength)),
                userName: ReadAscii(element.Slice(8 + FixedStringLength, FixedStringLength))));
        }

        return sensors;
    }

    private static List<HwinfoReadingEntry> ParseReadings(ReadOnlySpan<byte> buffer, HwinfoSharedMemoryHeader header)
    {
        if (header.ReadingCount == 0)
        {
            return [];
        }

        var readings = new List<HwinfoReadingEntry>((int)header.ReadingCount);
        for (var index = 0; index < header.ReadingCount; index++)
        {
            var offset = checked((int)(header.ReadingOffset + (index * header.ReadingElementSize)));
            var element = buffer.Slice(offset, (int)header.ReadingElementSize);
            readings.Add(new HwinfoReadingEntry(
                kind: (HwinfoReadingKind)ReadUInt32(element, 0),
                sensorIndex: ReadUInt32(element, 4),
                readingId: ReadUInt32(element, 8),
                originalLabel: ReadAscii(element.Slice(12, FixedStringLength)),
                userLabel: ReadAscii(element.Slice(12 + FixedStringLength, FixedStringLength)),
                unit: ReadAscii(element.Slice(12 + (FixedStringLength * 2), UnitStringLength)),
                currentValue: ReadNullableDouble(element, 12 + (FixedStringLength * 2) + UnitStringLength),
                minValue: ReadNullableDouble(element, 12 + (FixedStringLength * 2) + UnitStringLength + 8),
                maxValue: ReadNullableDouble(element, 12 + (FixedStringLength * 2) + UnitStringLength + 16),
                avgValue: ReadNullableDouble(element, 12 + (FixedStringLength * 2) + UnitStringLength + 24)));
        }

        return readings;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, sizeof(uint)));

    private static long ReadInt64(ReadOnlySpan<byte> buffer, int offset)
        => BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(offset, sizeof(long)));

    private static double? ReadNullableDouble(ReadOnlySpan<byte> buffer, int offset)
    {
        var raw = BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(offset, sizeof(long)));
        var value = BitConverter.Int64BitsToDouble(raw);
        return double.IsFinite(value) ? value : null;
    }

    private static string ReadAscii(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.Default.GetString(bytes);
        var terminatorIndex = text.IndexOf('\0');
        return (terminatorIndex >= 0 ? text[..terminatorIndex] : text).Trim();
    }
}
