using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceTelemetryHistoryBookTests
{
    [Fact]
    public void RecordSnapshots_ShouldDeduplicateByTelemetrySequence()
    {
        var book = new DeviceTelemetryHistoryBook(maxSamples: 4);

        book.RecordSnapshots(
        [
            CreateSnapshot("device-1", telemetrySequence: 10, rssi: -50, loopLoad: 20, freeHeap: 1000, freePsram: 2000),
            CreateSnapshot("device-1", telemetrySequence: 10, rssi: -49, loopLoad: 22, freeHeap: 900, freePsram: 1900),
        ]);

        var history = book.GetHistory("device-1");
        Assert.Single(history.RssiSamples);
        Assert.Equal(-50, history.RssiSamples[0]);
        Assert.Single(history.LoopLoadSamples);
        Assert.Equal(10u, history.LastTelemetrySequence);
    }

    [Fact]
    public void RecordSnapshots_ShouldTrimToConfiguredLimit()
    {
        var book = new DeviceTelemetryHistoryBook(maxSamples: 2);

        book.RecordSnapshots(
        [
            CreateSnapshot("device-1", telemetrySequence: 1, rssi: -60, loopLoad: 10, freeHeap: 100, freePsram: 1000),
            CreateSnapshot("device-1", telemetrySequence: 2, rssi: -59, loopLoad: 20, freeHeap: 200, freePsram: 900),
            CreateSnapshot("device-1", telemetrySequence: 3, rssi: -58, loopLoad: 30, freeHeap: 300, freePsram: 800),
        ]);

        var history = book.GetHistory("device-1");
        Assert.Equal([-59, -58], history.RssiSamples);
        Assert.Equal([20, 30], history.LoopLoadSamples);
        Assert.Equal([200L, 300L], history.FreeHeapSamples);
        Assert.Equal([900L, 800L], history.FreePsramSamples);
    }

    [Fact]
    public void RecordSnapshots_ShouldResetSeriesWhenSequenceWraps()
    {
        var book = new DeviceTelemetryHistoryBook(maxSamples: 4);

        book.RecordSnapshots(
        [
            CreateSnapshot("device-1", telemetrySequence: 100, rssi: -70, loopLoad: 40, freeHeap: 100, freePsram: 500),
            CreateSnapshot("device-1", telemetrySequence: 1, rssi: -55, loopLoad: 15, freeHeap: 200, freePsram: 400),
        ]);

        var history = book.GetHistory("device-1");
        Assert.Single(history.RssiSamples);
        Assert.Equal(-55, history.RssiSamples[0]);
        Assert.Equal(1u, history.LastTelemetrySequence);
    }

    private static DeviceSnapshot CreateSnapshot(
        string deviceId,
        uint telemetrySequence,
        int? rssi,
        int? loopLoad,
        long? freeHeap,
        long? freePsram)
    {
        return new DeviceSnapshot
        {
            DeviceId = deviceId,
            Name = deviceId,
            Status = DeviceStatus.Online,
            TelemetrySequence = telemetrySequence,
            LastKnownRssi = rssi,
            LoopLoadPercent = loopLoad,
            FreeHeapBytes = freeHeap,
            FreePsramBytes = freePsram,
        };
    }
}
