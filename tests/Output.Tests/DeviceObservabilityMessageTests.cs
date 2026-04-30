using System.Text.Json;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceObservabilityMessageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void DeserializeStats_ShouldReadAllFields()
    {
        const string json = """
            {
              "deviceId": "mp-01",
              "chipModel": "ESP32-S3",
              "chipRevision": 2,
              "chipCores": 2,
              "cpuFreqMHz": 240,
              "sdkVersion": "5.5.0",
              "heapTotalBytes": 327680,
              "psramTotalBytes": 8388608,
              "flashTotalBytes": 16777216,
              "sketchSizeBytes": 901120,
              "freeSketchBytes": 9437184
            }
            """;

        var stats = JsonSerializer.Deserialize<DeviceStatsMessage>(json, JsonOptions);

        Assert.NotNull(stats);
        Assert.Equal("mp-01", stats!.DeviceId);
        Assert.Equal("ESP32-S3", stats.ChipModel);
        Assert.Equal(2, stats.ChipRevision);
        Assert.Equal(2, stats.ChipCores);
        Assert.Equal(240, stats.CpuFreqMHz);
        Assert.Equal("5.5.0", stats.SdkVersion);
        Assert.Equal(327680L, stats.HeapTotalBytes);
        Assert.Equal(8388608L, stats.PsramTotalBytes);
        Assert.Equal(16777216L, stats.FlashTotalBytes);
        Assert.Equal(901120L, stats.SketchSizeBytes);
        Assert.Equal(9437184L, stats.FreeSketchBytes);
    }

    [Fact]
    public void DeserializeDeviceLog_ShouldReadStructuredPayload()
    {
        const string json = """
            {
              "deviceId": "mp-01",
              "sequence": 14,
              "level": "warning",
              "category": "mqtt",
              "eventCode": "mqtt_retry",
              "message": "Broker indisponivel; tentando novamente.",
              "uptimeSeconds": 45,
              "telemetrySequence": 8
            }
            """;

        var log = JsonSerializer.Deserialize<DeviceLogMessage>(json, JsonOptions);

        Assert.NotNull(log);
        Assert.Equal("mp-01", log!.DeviceId);
        Assert.Equal(14u, log.Sequence);
        Assert.Equal("warning", log.Level);
        Assert.Equal("mqtt", log.Category);
        Assert.Equal("mqtt_retry", log.EventCode);
        Assert.Equal("Broker indisponivel; tentando novamente.", log.Message);
        Assert.Equal(45, log.UptimeSeconds);
        Assert.Equal(8u, log.TelemetrySequence);
    }
}
