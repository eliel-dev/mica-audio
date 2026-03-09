using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using App.WinUI.Services.Monitoring;

namespace Output.Tests;

public sealed class HwinfoMonitoringTests
{
    [Fact]
    public void Parse_ShouldReadHeaderSensorsAndReadings()
    {
        var pollTime = DateTimeOffset.Parse("2026-03-09T12:00:00Z", CultureInfo.InvariantCulture);
        var buffer = BuildSharedMemory(
            signature: HwinfoSharedMemoryBinaryParser.ActiveSignature,
            pollTime,
            sensors:
            [
                new SensorSpec(1, 0, "CPU [#0]", "CPU Principal"),
                new SensorSpec(2, 0, "GPU [#0]", string.Empty),
            ],
            readings:
            [
                new ReadingSpec(HwinfoReadingKind.Temp, 0, 10, "CPU Package", string.Empty, "C", 71.5, 35.2, 82.0, 63.8),
                new ReadingSpec(HwinfoReadingKind.Usage, 1, 11, "GPU Core Load", string.Empty, "%", 85.4, 5.0, 98.0, 61.5),
            ]);

        var payload = HwinfoSharedMemoryBinaryParser.Parse(buffer);

        Assert.Equal(HwinfoSharedMemoryBinaryParser.ActiveSignature, payload.Header.Signature);
        Assert.Equal(pollTime, payload.Header.PollTimeUtc);
        Assert.Equal("CPU Principal", payload.Sensors[0].DisplayName);
        Assert.Equal("GPU [#0]", payload.Sensors[1].DisplayName);
        Assert.Equal("CPU Package", payload.Readings[0].DisplayLabel);
        Assert.Equal("%", payload.Readings[1].Unit);
        Assert.Equal(85.4d, payload.Readings[1].CurrentValue);
    }

    [Fact]
    public void Parse_ShouldPreserveLocalizedStringsFromLegacyNarrowFields()
    {
        var pollTime = DateTimeOffset.Parse("2026-03-09T12:00:00Z", CultureInfo.InvariantCulture);
        var buffer = BuildSharedMemory(
            signature: HwinfoSharedMemoryBinaryParser.ActiveSignature,
            pollTime,
            sensors: [new SensorSpec(1, 0, "CPU [#0]: AMD Ryzen 9 9950X: C-State Ocupação", string.Empty)],
            readings: [new ReadingSpec(HwinfoReadingKind.Other, 0, 10, "Memória física disponível", string.Empty, "MB", 3626, 2337, 7745, 3975)],
            textEncoding: Encoding.Default);

        var payload = HwinfoSharedMemoryBinaryParser.Parse(buffer);

        Assert.Equal("CPU [#0]: AMD Ryzen 9 9950X: C-State Ocupação", payload.Sensors[0].DisplayName);
        Assert.Equal("Memória física disponível", payload.Readings[0].DisplayLabel);
    }

    [Fact]
    public void Project_ShouldReturnStaleSnapshotWhenPollTimeIsOld()
    {
        var pollTime = DateTimeOffset.Parse("2026-03-09T12:00:00Z", CultureInfo.InvariantCulture);
        var buffer = BuildSharedMemory(
            signature: HwinfoSharedMemoryBinaryParser.ActiveSignature,
            pollTime,
            sensors: [new SensorSpec(1, 0, "CPU [#0]", string.Empty)],
            readings: [new ReadingSpec(HwinfoReadingKind.Temp, 0, 10, "CPU Package", string.Empty, "C", 55.0, 40.0, 70.0, 52.0)]);

        var payload = HwinfoSharedMemoryBinaryParser.Parse(buffer);
        var snapshot = MonitoringSnapshotProjector.Project(payload, pollTime.AddSeconds(11));

        Assert.Equal(MonitoringSourceState.Stale, snapshot.State);
        Assert.Equal("Snapshot do HWiNFO64 esta desatualizado", snapshot.StateText);
        Assert.Single(snapshot.Readings);
    }

    [Fact]
    public void Select_ShouldReturnSixCardsWithLocalizedMemoryAndResolvedHardwareNames()
    {
        MonitoringReading[] readings =
        {
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X: C-State Ocupação", 1, "CPU Package", "C", HwinfoReadingKind.Temp, 74, 42, 82, 63),
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X: C-State Ocupação", 2, "Total CPU Usage", "%", HwinfoReadingKind.Usage, 36, 2, 92, 41),
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X: Enhanced", 3, "CPU Package Power", "W", HwinfoReadingKind.Power, 89.5, 22.1, 120.0, 73.4),
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X: Enhanced", 4, "Core Effective Clock (Average)", "MHz", HwinfoReadingKind.Clock, 5125, 3600, 5580, 4990),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070: DELL GeForce RTX 5070", 5, "Temperatura GPU", "C", HwinfoReadingKind.Temp, 67, 35, 76, 58),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070: DELL GeForce RTX 5070", 6, "Carga GPU", "%", HwinfoReadingKind.Usage, 81, 10, 99, 62),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070: DELL GeForce RTX 5070", 7, "Potência GPU", "W", HwinfoReadingKind.Power, 214.3, 60.0, 260.1, 171.7),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070: DELL GeForce RTX 5070", 8, "Relógio GPU", "MHz", HwinfoReadingKind.Clock, 2760, 210, 2790, 2480),
            CreateReading(3, 0, "Sistema: ASUS", 9, "Memória física utilizada", "MB", HwinfoReadingKind.Other, 12668, 8550, 13958, 12320),
            CreateReading(3, 0, "Sistema: ASUS", 10, "Memória física disponível", "MB", HwinfoReadingKind.Other, 3626, 2337, 7745, 3975),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070: DELL GeForce RTX 5070", 11, "Memória GPU alocada", "MB", HwinfoReadingKind.Other, 1530, 1190, 2040, 1643),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070: DELL GeForce RTX 5070", 12, "Memória GPU disponível", "MB", HwinfoReadingKind.Other, 10697, 10187, 11037, 10584),
        };

        var kpis = MonitoringKpiSelector.Select(readings, MonitoringMemoryFallbackSnapshot.Empty);

        Assert.Collection(
            kpis,
            usage =>
            {
                Assert.Equal("Uso total", usage.Title);
                Assert.Equal("AMD Ryzen 9 9950X + NVIDIA GeForce RTX 5070", usage.ContextText);
                Assert.Equal("AMD Ryzen 9 9950X", usage.Metrics[0].HardwareName);
                Assert.Equal("36%", usage.Metrics[0].ValueText);
                Assert.Equal("NVIDIA GeForce RTX 5070", usage.Metrics[1].HardwareName);
                Assert.Equal("81%", usage.Metrics[1].ValueText);
            },
            temperature =>
            {
                Assert.Equal("Temperatura geral", temperature.Title);
                Assert.Equal("74 C", temperature.Metrics[0].ValueText);
                Assert.Equal("67 C", temperature.Metrics[1].ValueText);
            },
            memory =>
            {
                Assert.Equal("Memoria RAM", memory.Title);
                Assert.Equal("Memoria do sistema", memory.ContextText);
                Assert.Equal("12.37 GB", memory.Metrics[0].ValueText);
                Assert.Equal("3.54 GB", memory.Metrics[1].ValueText);
                Assert.NotNull(memory.Capacity);
                Assert.Equal("12.37 GB", memory.Capacity!.UsedText);
                Assert.Equal("3.54 GB", memory.Capacity.AvailableText);
                Assert.Equal("15.91 GB", memory.Capacity.TotalText);
                Assert.Equal("77.7% (12.37 GB)", memory.Capacity.BarText);
                Assert.Equal(MonitoringDataOrigin.Hwinfo, memory.Metrics[0].DataOrigin);
            },
            vram =>
            {
                Assert.Equal("VRAM GPU", vram.Title);
                Assert.Equal("NVIDIA GeForce RTX 5070", vram.ContextText);
                Assert.Equal("1.49 GB", vram.Metrics[0].ValueText);
                Assert.Equal("10.45 GB", vram.Metrics[1].ValueText);
                Assert.NotNull(vram.Capacity);
                Assert.Equal("11.94 GB", vram.Capacity!.TotalText);
                Assert.Equal("12.5% (1.49 GB)", vram.Capacity.BarText);
                Assert.Equal(MonitoringDataOrigin.Hwinfo, vram.Metrics[1].DataOrigin);
            },
            power =>
            {
                Assert.Equal("Consumo", power.Title);
                Assert.Equal("89.5 W", power.Metrics[0].ValueText);
                Assert.Equal("214.3 W", power.Metrics[1].ValueText);
            },
            frequency =>
            {
                Assert.Equal("Frequencia", frequency.Title);
                Assert.Equal("5125 MHz", frequency.Metrics[0].ValueText);
                Assert.Equal("2760 MHz", frequency.Metrics[1].ValueText);
            });
    }

    [Fact]
    public void Select_ShouldPreferGeneralTemperatureOverHotspotOrPerCoreReadings()
    {
        MonitoringReading[] readings =
        {
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X", 1, "CPU Package", "C", HwinfoReadingKind.Temp, 73, 42, 81, 61),
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X", 2, "Core 0", "C", HwinfoReadingKind.Temp, 81, 38, 86, 68),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070", 3, "Temperatura GPU", "C", HwinfoReadingKind.Temp, 65, 35, 74, 57),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070", 4, "Temperatura do ponto quente da memória GPU", "C", HwinfoReadingKind.Temp, 83, 40, 94, 73),
        };

        var kpis = MonitoringKpiSelector.Select(readings, MonitoringMemoryFallbackSnapshot.Empty);

        Assert.Equal("73 C", kpis[1].Metrics[0].ValueText);
        Assert.Equal("65 C", kpis[1].Metrics[1].ValueText);
    }

    [Fact]
    public void Select_ShouldKeepCardAvailableWhenOnlyOneMetricExists()
    {
        MonitoringReading[] readings =
        {
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X", 1, "Total CPU Usage", "%", HwinfoReadingKind.Usage, 42, 2, 92, 37),
        };

        var kpis = MonitoringKpiSelector.Select(readings, MonitoringMemoryFallbackSnapshot.Empty);

        Assert.True(kpis[0].IsAvailable);
        Assert.True(kpis[0].Metrics[0].IsAvailable);
        Assert.False(kpis[0].Metrics[1].IsAvailable);
        Assert.Equal("Indisponivel", kpis[0].Metrics[1].ValueText);
    }

    [Fact]
    public void Select_ShouldUseWindowsFallbackOnlyWhenHwinfoMemoryCannotBeResolved()
    {
        MonitoringReading[] readings =
        {
            CreateReading(1, 0, "CPU [#0]: AMD Ryzen 9 9950X", 1, "CPU Package", "C", HwinfoReadingKind.Temp, 73, 42, 81, 61),
            CreateReading(2, 0, "GPU [#0]: NVIDIA GeForce RTX 5070", 2, "Temperatura GPU", "C", HwinfoReadingKind.Temp, 65, 35, 74, 57),
        };
        var fallbackSnapshot = new MonitoringMemoryFallbackSnapshot(
            new MonitoringSystemMemoryFallback(usedBytes: 12UL * 1024UL * 1024UL * 1024UL, availableBytes: 20UL * 1024UL * 1024UL * 1024UL, totalBytes: 32UL * 1024UL * 1024UL * 1024UL),
            new MonitoringGpuMemoryFallback("NVIDIA GeForce RTX 5070", usedBytes: 2UL * 1024UL * 1024UL * 1024UL, totalBytes: 12UL * 1024UL * 1024UL * 1024UL));

        var kpis = MonitoringKpiSelector.Select(readings, fallbackSnapshot);

        Assert.Equal(MonitoringDataOrigin.WindowsFallback, kpis[2].Metrics[0].DataOrigin);
        Assert.Equal(MonitoringDataOrigin.WindowsFallback, kpis[2].Metrics[1].DataOrigin);
        Assert.Equal(MonitoringDataOrigin.WindowsFallback, kpis[3].Metrics[0].DataOrigin);
        Assert.Equal(MonitoringDataOrigin.WindowsFallback, kpis[3].Metrics[1].DataOrigin);
        Assert.Equal("12 GB", kpis[2].Metrics[0].ValueText);
        Assert.Equal("20 GB", kpis[2].Metrics[1].ValueText);
        Assert.Equal("2 GB", kpis[3].Metrics[0].ValueText);
        Assert.Equal("10 GB", kpis[3].Metrics[1].ValueText);
        Assert.Equal("37.5% (12 GB)", kpis[2].Capacity!.BarText);
        Assert.Equal("16.7% (2 GB)", kpis[3].Capacity!.BarText);
    }

    [Fact]
    public void Select_ShouldPreferHwinfoMemoryOverWindowsFallback()
    {
        MonitoringReading[] readings =
        {
            CreateReading(3, 0, "Sistema: ASUS", 1, "Memória física utilizada", "MB", HwinfoReadingKind.Other, 12668, 8550, 13958, 12320),
            CreateReading(3, 0, "Sistema: ASUS", 2, "Memória física disponível", "MB", HwinfoReadingKind.Other, 3626, 2337, 7745, 3975),
        };
        var fallbackSnapshot = new MonitoringMemoryFallbackSnapshot(
            new MonitoringSystemMemoryFallback(usedBytes: 12UL * 1024UL * 1024UL * 1024UL, availableBytes: 20UL * 1024UL * 1024UL * 1024UL, totalBytes: 32UL * 1024UL * 1024UL * 1024UL),
            gpuMemory: null);

        var kpis = MonitoringKpiSelector.Select(readings, fallbackSnapshot);

        Assert.Equal(MonitoringDataOrigin.Hwinfo, kpis[2].Metrics[0].DataOrigin);
        Assert.Equal(MonitoringDataOrigin.Hwinfo, kpis[2].Metrics[1].DataOrigin);
        Assert.Equal("12.37 GB", kpis[2].Metrics[0].ValueText);
        Assert.Equal("77.7% (12.37 GB)", kpis[2].Capacity!.BarText);
    }

    private static byte[] BuildSharedMemory(
        string signature,
        DateTimeOffset pollTime,
        IReadOnlyList<SensorSpec> sensors,
        IReadOnlyList<ReadingSpec> readings,
        Encoding? textEncoding = null)
    {
        textEncoding ??= Encoding.ASCII;
        var sensorOffset = HwinfoSharedMemoryBinaryParser.HeaderSize;
        var readingOffset = sensorOffset + (sensors.Count * HwinfoSharedMemoryBinaryParser.SensorElementMinimumSize);
        var totalLength = readingOffset + (readings.Count * HwinfoSharedMemoryBinaryParser.ReadingElementMinimumSize);
        var buffer = new byte[totalLength];

        WriteEncoded(buffer.AsSpan(0, 4), signature, Encoding.ASCII);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(12, 8), pollTime.ToUnixTimeSeconds());
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20, 4), (uint)sensorOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24, 4), HwinfoSharedMemoryBinaryParser.SensorElementMinimumSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(28, 4), (uint)sensors.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(32, 4), (uint)readingOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(36, 4), HwinfoSharedMemoryBinaryParser.ReadingElementMinimumSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(40, 4), (uint)readings.Count);

        for (var index = 0; index < sensors.Count; index++)
        {
            var offset = sensorOffset + (index * HwinfoSharedMemoryBinaryParser.SensorElementMinimumSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, 4), sensors[index].SensorId);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 4, 4), sensors[index].SensorInstance);
            WriteEncoded(buffer.AsSpan(offset + 8, 128), sensors[index].OriginalName, textEncoding);
            WriteEncoded(buffer.AsSpan(offset + 136, 128), sensors[index].UserName, textEncoding);
        }

        for (var index = 0; index < readings.Count; index++)
        {
            var offset = readingOffset + (index * HwinfoSharedMemoryBinaryParser.ReadingElementMinimumSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, 4), (uint)readings[index].Kind);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 4, 4), readings[index].SensorIndex);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 8, 4), readings[index].ReadingId);
            WriteEncoded(buffer.AsSpan(offset + 12, 128), readings[index].OriginalLabel, textEncoding);
            WriteEncoded(buffer.AsSpan(offset + 140, 128), readings[index].UserLabel, textEncoding);
            WriteEncoded(buffer.AsSpan(offset + 268, 16), readings[index].Unit, Encoding.ASCII);
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset + 284, 8), BitConverter.DoubleToInt64Bits(readings[index].Current));
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset + 292, 8), BitConverter.DoubleToInt64Bits(readings[index].Min));
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset + 300, 8), BitConverter.DoubleToInt64Bits(readings[index].Max));
            BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset + 308, 8), BitConverter.DoubleToInt64Bits(readings[index].Avg));
        }

        return buffer;
    }

    private static MonitoringReading CreateReading(
        uint sensorId,
        uint sensorInstance,
        string sensorName,
        uint readingId,
        string label,
        string unit,
        HwinfoReadingKind kind,
        double current,
        double min,
        double max,
        double avg,
        string? displaySensorName = null,
        string? displayLabel = null)
        => new(
            sensorId,
            sensorInstance,
            sensorName,
            displaySensorName ?? sensorName,
            readingId,
            label,
            displayLabel ?? label,
            unit,
            kind,
            current,
            min,
            max,
            avg);

    private static void WriteEncoded(Span<byte> destination, string value, Encoding encoding)
    {
        destination.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var bytes = encoding.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    private sealed record SensorSpec(uint SensorId, uint SensorInstance, string OriginalName, string UserName);

    private sealed record ReadingSpec(
        HwinfoReadingKind Kind,
        uint SensorIndex,
        uint ReadingId,
        string OriginalLabel,
        string UserLabel,
        string Unit,
        double Current,
        double Min,
        double Max,
        double Avg);
}
