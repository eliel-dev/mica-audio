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
        Assert.DoesNotContain(logs, entry => entry.Message.Contains("linha-1", StringComparison.Ordinal));
        Assert.Contains(logs, entry => entry.Message.Contains("linha-3", StringComparison.Ordinal));
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
        Assert.DoesNotContain(logs, entry => entry.Message.Contains('a'));
        Assert.Contains(logs, entry => entry.Message.Contains('c'));
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
        Assert.Contains(logs, entry => entry.Message.Contains("Dispositivo autenticado e online.", StringComparison.Ordinal));
        Assert.Contains(logs, entry => entry.Message.Contains("Dispositivo voltou a aparecer apos ficar offline.", StringComparison.Ordinal));
        Assert.Contains(logs, entry => entry.Message.Contains("Primeira telemetria recebida apos reconexao.", StringComparison.Ordinal));
    }

    [Fact]
    public void RecordLifecycleEvents_ShouldIgnoreWebSocketConnectivityEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var book = new DeviceLogBook(maxLogEntries: 10, maxDeviceLogEntries: 10);

        book.RecordLifecycleEvents(
            previous:
            [
                CreateSnapshot("device-1", DeviceStatus.Online, now.AddSeconds(-2), now.AddSeconds(-2), lastWifiEvent: "wifi_connected"),
            ],
            next:
            [
                CreateSnapshot("device-1", DeviceStatus.Online, now, now, lastWifiEvent: "ws_disconnected"),
            ],
            now);

        var logs = book.GetDeviceLogs("device-1");
        Assert.DoesNotContain(logs, entry => entry.Message.Contains("ws_disconnected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppendDeviceFirmware_ShouldMergeStructuredEntries_AndIgnoreDuplicateSequence()
    {
        var book = new DeviceLogBook(maxLogEntries: 10, maxDeviceLogEntries: 10);

        var first = new DeviceLogMessage
        {
            DeviceId = "device-1",
            Sequence = 7,
            Level = "warning",
            Category = "mqtt",
            EventCode = "mqtt_retry",
            Message = "Broker indisponivel.",
        };

        var duplicate = new DeviceLogMessage
        {
            DeviceId = "device-1",
            Sequence = 7,
            Level = "warning",
            Category = "mqtt",
            EventCode = "mqtt_retry",
            Message = "Broker indisponivel.",
        };

        Assert.True(book.AppendDeviceFirmware(first));
        Assert.False(book.AppendDeviceFirmware(duplicate));

        var logs = book.GetDeviceLogs("device-1");
        var entry = Assert.Single(logs);
        Assert.Equal(DeviceLogSource.Device, entry.Source);
        Assert.Equal(DeviceLogSeverity.Warning, entry.Severity);
        Assert.Equal("mqtt", entry.Category);
        Assert.Equal("mqtt_retry", entry.Code);
        Assert.Equal("Broker indisponivel.", entry.Message);
    }

    private static DeviceSnapshot CreateSnapshot(
        string deviceId,
        DeviceStatus status,
        DateTimeOffset lastSeenUtc,
        DateTimeOffset? lastTelemetryUtc,
        string? lastWifiEvent = null)
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
            LastWifiEvent = lastWifiEvent,
        };
    }
}
