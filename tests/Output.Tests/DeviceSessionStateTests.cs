using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class DeviceSessionStateTests
{
    [Fact]
    public void MarkAuthenticated_ShouldStampFirstSeenAndLastAuth()
    {
        var now = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new DeviceSessionState(CreateRecord(now), TimeSpan.FromMilliseconds(500));

        var later = now.AddSeconds(5);
        state.MarkAuthenticated(later);

        Assert.True(state.Record.IsRegistered);
        Assert.Equal(later, state.Record.FirstSeenUtc);
        Assert.Equal(later, state.Record.LastAuthUtc);
        Assert.Equal(DeviceConfigState.KnownGood, state.Record.ConfigState);
    }

    [Fact]
    public void MarkTelemetry_ShouldStampTelemetryAndPreserveAuth()
    {
        var now = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new DeviceSessionState(CreateRecord(now), TimeSpan.FromMilliseconds(500));

        state.MarkAuthenticated(now.AddSeconds(5));
        var authUtc = state.Record.LastAuthUtc;

        var telemetryUtc = now.AddSeconds(7);
        state.MarkTelemetry(
            telemetryUtc,
            ip: "192.168.0.10",
            rssi: -52,
            firmwareVersion: "1.0.0",
            activeAppId: "visualizer",
            activeAppName: "Visualizer",
            boardModel: "esp32s3_devkitc1",
            panelType: "hub75",
            uptimeSeconds: 321,
            loopHealthyPercent: 94,
            loopLoadPercent: 40,
            chipTemperatureCelsius: 46.5d,
            freeHeapBytes: 4096,
            largestHeapBlockBytes: 2048,
            psramAvailable: true,
            freePsramBytes: 8192,
            largestPsramBlockBytes: 4096,
            wifiConnected: true,
            wifiState: "connected",
            resetReason: "RTC_SW_SYS_RESET",
            controlQueueDepth: 1,
            controlWorkerState: "idle",
            panelsWorkerState: "pending_batch",
            lastSlowCommand: "queue_panels_batch",
            lastSlowCommandDurationMs: 640);

        Assert.Equal(telemetryUtc, state.Record.LastTelemetryUtc);
        Assert.Equal(authUtc, state.Record.LastAuthUtc);
        Assert.Equal("192.168.0.10", state.Record.LastKnownIp);
        Assert.Equal(321, state.Record.UptimeSeconds);
        Assert.Equal(94, state.Record.LoopHealthyPercent);
        Assert.Equal(46.5d, state.Record.ChipTemperatureCelsius);
        Assert.Equal("Visualizer", state.Record.ActiveAppName);
        Assert.Equal("hub75", state.Record.PanelType);
        Assert.Equal("RTC_SW_SYS_RESET", state.Record.ResetReason);
        Assert.Equal(1u, state.Record.ControlQueueDepth);
        Assert.Equal("pending_batch", state.Record.PanelsWorkerState);
        Assert.Equal(640L, state.Record.LastSlowCommandDurationMs);
    }

    [Fact]
    public void MarkStats_ShouldPersistObservabilityFieldsWithoutResettingTelemetry()
    {
        var now = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new DeviceSessionState(CreateRecord(now), TimeSpan.FromMilliseconds(500));

        state.MarkTelemetry(now, ip: "192.168.0.20", rssi: -48, firmwareVersion: "1.0.0", uptimeSeconds: 90, loopLoadPercent: 18);

        state.MarkStats(
            now.AddSeconds(1),
            ip: "192.168.0.20",
            chipModel: "ESP32-S3",
            chipRevision: 2,
            chipCores: 2,
            cpuFreqMHz: 240,
            sdkVersion: "5.5.0",
            heapTotalBytes: 327680,
            psramTotalBytes: 8388608,
            flashTotalBytes: 16777216,
            sketchSizeBytes: 901120,
            freeSketchBytes: 9437184);

        Assert.Equal(90, state.Record.UptimeSeconds);
        Assert.Equal("ESP32-S3", state.Record.ChipModel);
        Assert.Equal(2, state.Record.ChipRevision);
        Assert.Equal(240, state.Record.CpuFreqMHz);
        Assert.Equal(327680L, state.Record.HeapTotalBytes);
        Assert.Equal(9437184L, state.Record.FreeSketchBytes);
    }

    [Fact]
    public void MarkControlPlaneDisconnected_ShouldRespectGracePeriodWithControlledTime()
    {
        var now = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new DeviceSessionState(CreateRecord(now), TimeSpan.FromMilliseconds(500));

        state.MarkControlPlaneConnected(now, "192.168.0.11");
        Assert.Equal(DeviceStatus.Online, state.ToSnapshot(now, TimeSpan.FromSeconds(15)).Status);

        state.MarkControlPlaneDisconnected(now.AddMilliseconds(100));
        Assert.Equal(DeviceStatus.Online, state.ToSnapshot(now.AddMilliseconds(100), TimeSpan.FromSeconds(15)).Status);

        Assert.Equal(DeviceStatus.Offline, state.ToSnapshot(now.AddMilliseconds(700), TimeSpan.FromSeconds(15)).Status);
    }

    [Fact]
    public void LegacyControlPlaneTraffic_ShouldExposeLegacyOnlyUntilFreshnessExpires()
    {
        var now = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new DeviceSessionState(CreateRecord(now), TimeSpan.FromMilliseconds(500));

        Assert.True(state.MarkLegacyControlPlaneTraffic(now));

        var legacySnapshot = state.ToSnapshot(now, TimeSpan.FromSeconds(15));
        Assert.Equal(DeviceStatus.Offline, legacySnapshot.Status);
        Assert.Equal(DeviceControlPlaneState.LegacyOnly, legacySnapshot.ControlPlaneState);

        var expiredSnapshot = state.ToSnapshot(now.AddSeconds(16), TimeSpan.FromSeconds(15));
        Assert.Equal(DeviceControlPlaneState.Offline, expiredSnapshot.ControlPlaneState);
    }

    [Fact]
    public void MqttReconnect_ShouldClearLegacyControlPlaneState()
    {
        var now = new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new DeviceSessionState(CreateRecord(now), TimeSpan.FromMilliseconds(500));

        state.MarkLegacyControlPlaneTraffic(now);
        Assert.Equal(DeviceControlPlaneState.LegacyOnly, state.ToSnapshot(now, TimeSpan.FromSeconds(15)).ControlPlaneState);

        state.MarkControlPlaneConnected(now.AddSeconds(1), "192.168.0.12");
        Assert.Equal(DeviceControlPlaneState.MqttOnline, state.ToSnapshot(now.AddSeconds(1), TimeSpan.FromSeconds(15)).ControlPlaneState);
    }

    private static DeviceRecord CreateRecord(DateTimeOffset now)
        => new()
        {
            DeviceId = "device-1",
            Token = "token-1",
            Name = "Device 1",
            CreatedAtUtc = now,
            LastSeenUtc = now,
            IsRegistered = false,
        };
}
