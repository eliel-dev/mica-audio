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
              "loopLoadPercent": 47,
              "freeHeapBytes": 195584,
              "largestHeapBlockBytes": 120320,
              "psramAvailable": true,
              "freePsramBytes": 6209536,
              "largestPsramBlockBytes": 4718592,
              "wifiConnected": true,
              "firmwareVersion": "v1.2.3",
              "ipAddress": "192.168.1.23"
            }
            """;

        var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json, JsonOptions);

        Assert.NotNull(telemetry);
        Assert.Equal("mp-01", telemetry!.DeviceId);
        Assert.Equal(-56, telemetry.Rssi);
        Assert.Equal(7200, telemetry.UptimeSeconds);
        Assert.Equal(47, telemetry.LoopLoadPercent);
        Assert.Equal(195584L, telemetry.FreeHeapBytes);
        Assert.Equal(120320L, telemetry.LargestHeapBlockBytes);
        Assert.True(telemetry.PsramAvailable);
        Assert.Equal(6209536L, telemetry.FreePsramBytes);
        Assert.Equal(4718592L, telemetry.LargestPsramBlockBytes);
        Assert.True(telemetry.WifiConnected);
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
        Assert.Null(telemetry.LoopLoadPercent);
        Assert.Null(telemetry.FreeHeapBytes);
        Assert.Null(telemetry.LargestHeapBlockBytes);
        Assert.Null(telemetry.PsramAvailable);
        Assert.Null(telemetry.FreePsramBytes);
        Assert.Null(telemetry.LargestPsramBlockBytes);
        Assert.Null(telemetry.WifiConnected);
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
