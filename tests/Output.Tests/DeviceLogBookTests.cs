using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceLogBookTests
{
    [Fact]
    public void AppendGlobal_ShouldTrimToConfiguredLimit()
    {
        var book = new DeviceLogBook(maxLogEntries: 2, maxDeviceLogEntries: 2);

        book.AppendGlobal("linha-1");
        book.AppendGlobal("linha-2");
        book.AppendGlobal("linha-3");

        var logs = book.GetGlobalLogs();
        Assert.Equal(2, logs.Count);
        Assert.DoesNotContain(logs, line => line.Contains("linha-1", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("linha-3", StringComparison.Ordinal));
    }

    [Fact]
    public void AppendDevice_ShouldTrimPerDeviceEntries()
    {
        var book = new DeviceLogBook(maxLogEntries: 10, maxDeviceLogEntries: 2);

        book.AppendDevice("device-1", "a");
        book.AppendDevice("device-1", "b");
        book.AppendDevice("device-1", "c");

        var logs = book.GetDeviceLogs("device-1");
        Assert.Equal(2, logs.Count);
        Assert.DoesNotContain(logs, line => line.Contains('a'));
        Assert.Contains(logs, line => line.Contains('c'));
    }

    [Fact]
    public void RecordLifecycleEvents_ShouldTrackOnlineReturnAndFreshTelemetry()
    {
        var now = DateTimeOffset.UtcNow;
        var book = new DeviceLogBook(maxLogEntries: 10, maxDeviceLogEntries: 10);

        book.RecordLifecycleEvents(
            previous:
            [
                CreateSnapshot("device-1", DeviceStatus.Offline, now.AddMinutes(-2), null),
            ],
            next:
            [
                CreateSnapshot("device-1", DeviceStatus.Online, now, now),
            ],
            now);

        var logs = book.GetDeviceLogs("device-1");
        Assert.Contains(logs, line => line.Contains("Dispositivo autenticado e online.", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("Dispositivo voltou a aparecer apos ficar offline.", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("Primeira telemetria recebida apos reconexao.", StringComparison.Ordinal));
    }

    private static DeviceSnapshot CreateSnapshot(
        string deviceId,
        DeviceStatus status,
        DateTimeOffset lastSeenUtc,
        DateTimeOffset? lastTelemetryUtc)
    {
        return new DeviceSnapshot
        {
            DeviceId = deviceId,
            Name = deviceId,
            Profile = "dma_exp",
            Status = status,
            IsRegistered = true,
            FirstSeenUtc = lastSeenUtc.AddMinutes(-5),
            LastSeenUtc = lastSeenUtc,
            LastTelemetryUtc = lastTelemetryUtc,
        };
    }
}
