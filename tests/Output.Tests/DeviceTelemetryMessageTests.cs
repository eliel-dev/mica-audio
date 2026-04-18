using System.Text.Json;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceTelemetryMessageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Deserialize_ShouldReadExtendedTelemetryFields()
    {
        const string json = """
            {
              "deviceId": "mp-01",
              "rssi": -56,
              "uptimeSeconds": 7200,
              "loopHealthyPercent": 92,
              "loopLoadPercent": 47,
              "chipTemperatureCelsius": 48.5,
              "freeHeapBytes": 195584,
              "largestHeapBlockBytes": 120320,
              "psramAvailable": true,
              "freePsramBytes": 6209536,
              "largestPsramBlockBytes": 4718592,
              "wifiConnected": true,
              "wifiState": "connected",
              "provisioningPortalActive": false,
              "auxLedAvailable": true,
              "testLedAvailable": true,
              "lastWifiEvent": "ws_connected",
              "hub75PresentFrames": 480,
              "resetReason": "RTC_SW_SYS_RESET",
              "controlQueueDepth": 2,
              "controlWorkerState": "awaiting_ota_result",
              "panelsWorkerState": "idle",
              "lastSlowCommand": "queue_panels_batch",
              "lastSlowCommandDurationMs": 812,
              "telemetrySequence": 42,
              "brightnessCap": 120,
              "brightnessRequested": 180,
              "brightnessApplied": 120,
              "testLedEnabled": true,
              "testLedDuty": 120,
              "animatedWebpBatchSupported": true,
              "firmwareVersion": "v1.2.3",
              "ipAddress": "192.168.1.23"
            }
            """;

        var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json, JsonOptions);

        Assert.NotNull(telemetry);
        Assert.Equal("mp-01", telemetry!.DeviceId);
        Assert.Equal(-56, telemetry.Rssi);
        Assert.Equal(7200, telemetry.UptimeSeconds);
        Assert.Equal(92, telemetry.LoopHealthyPercent);
        Assert.Equal(47, telemetry.LoopLoadPercent);
        Assert.Equal(48.5d, telemetry.ChipTemperatureCelsius);
        Assert.Equal(195584L, telemetry.FreeHeapBytes);
        Assert.Equal(120320L, telemetry.LargestHeapBlockBytes);
        Assert.True(telemetry.PsramAvailable);
        Assert.Equal(6209536L, telemetry.FreePsramBytes);
        Assert.Equal(4718592L, telemetry.LargestPsramBlockBytes);
        Assert.True(telemetry.WifiConnected);
        Assert.Equal("connected", telemetry.WifiState);
        Assert.False(telemetry.ProvisioningPortalActive);
        Assert.True(telemetry.AuxLedAvailable);
        Assert.True(telemetry.TestLedAvailable);
        Assert.Equal("ws_connected", telemetry.LastWifiEvent);
        Assert.Equal(480u, telemetry.Hub75PresentFrames);
        Assert.Equal("RTC_SW_SYS_RESET", telemetry.ResetReason);
        Assert.Equal(2u, telemetry.ControlQueueDepth);
        Assert.Equal("awaiting_ota_result", telemetry.ControlWorkerState);
        Assert.Equal("idle", telemetry.PanelsWorkerState);
        Assert.Equal("queue_panels_batch", telemetry.LastSlowCommand);
        Assert.Equal(812L, telemetry.LastSlowCommandDurationMs);
        Assert.Equal(42u, telemetry.TelemetrySequence);
        Assert.Equal(120, telemetry.BrightnessCap);
        Assert.Equal(180, telemetry.BrightnessRequested);
        Assert.Equal(120, telemetry.BrightnessApplied);
        Assert.True(telemetry.TestLedEnabled);
        Assert.Equal(120, telemetry.TestLedDuty);
        Assert.True(telemetry.AnimatedWebpBatchSupported);
        Assert.Equal("v1.2.3", telemetry.FirmwareVersion);
        Assert.Equal("192.168.1.23", telemetry.IpAddress);
    }

    [Fact]
    public void Deserialize_ShouldKeepExtendedFieldsNull_WhenPayloadIsLegacy()
    {
        const string json = """
            {
              "deviceId": "mp-legacy",
              "rssi": -62,
              "firmwareVersion": "v0.9.0",
              "ipAddress": "192.168.1.80"
            }
            """;

        var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json, JsonOptions);

        Assert.NotNull(telemetry);
        Assert.Equal("mp-legacy", telemetry!.DeviceId);
        Assert.Equal(-62, telemetry.Rssi);
        Assert.Null(telemetry.UptimeSeconds);
        Assert.Null(telemetry.LoopHealthyPercent);
        Assert.Null(telemetry.LoopLoadPercent);
        Assert.Null(telemetry.ChipTemperatureCelsius);
        Assert.Null(telemetry.FreeHeapBytes);
        Assert.Null(telemetry.LargestHeapBlockBytes);
        Assert.Null(telemetry.PsramAvailable);
        Assert.Null(telemetry.FreePsramBytes);
        Assert.Null(telemetry.LargestPsramBlockBytes);
        Assert.Null(telemetry.WifiConnected);
        Assert.Null(telemetry.WifiState);
        Assert.Null(telemetry.ProvisioningPortalActive);
        Assert.Null(telemetry.AuxLedAvailable);
        Assert.Null(telemetry.TestLedAvailable);
        Assert.Null(telemetry.LastWifiEvent);
        Assert.Null(telemetry.ResetReason);
        Assert.Null(telemetry.ControlQueueDepth);
        Assert.Null(telemetry.ControlWorkerState);
        Assert.Null(telemetry.PanelsWorkerState);
        Assert.Null(telemetry.LastSlowCommand);
        Assert.Null(telemetry.LastSlowCommandDurationMs);
        Assert.Null(telemetry.Hub75PresentFrames);
        Assert.Null(telemetry.TelemetrySequence);
        Assert.Null(telemetry.BrightnessCap);
        Assert.Null(telemetry.BrightnessRequested);
        Assert.Null(telemetry.BrightnessApplied);
        Assert.Null(telemetry.TestLedEnabled);
        Assert.Null(telemetry.TestLedDuty);
        Assert.Null(telemetry.AnimatedWebpBatchSupported);
    }

    [Fact]
    public void Deserialize_ShouldAllowPsramUnavailableWithoutPsramSizes()
    {
        const string json = """
            {
              "deviceId": "mp-nopsram",
              "psramAvailable": false,
              "freeHeapBytes": 180224
            }
            """;

        var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json, JsonOptions);

        Assert.NotNull(telemetry);
        Assert.Equal("mp-nopsram", telemetry!.DeviceId);
        Assert.False(telemetry.PsramAvailable);
        Assert.Null(telemetry.FreePsramBytes);
        Assert.Null(telemetry.LargestPsramBlockBytes);
        Assert.Equal(180224L, telemetry.FreeHeapBytes);
    }
}
