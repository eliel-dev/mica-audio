using System.Text.Json;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceSessionShadowMessageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Deserialize_ShouldReadSessionShadowFields()
    {
        const string json = """
            {
              "deviceId": "esp-01",
              "shadowVersion": 17,
              "mode": "visualizer",
              "activeClientId": "win-eliel",
              "activeOwnerEpoch": 4,
              "ownerLeaseRemainingMs": 4200,
              "lockHeld": true,
              "lockClientId": "win-eliel",
              "lockReason": "settings",
              "lockLeaseRemainingMs": 11000,
              "activeAppId": "visualizer-hub75",
              "fallbackState": "none"
            }
            """;

        var shadow = JsonSerializer.Deserialize<DeviceSessionShadowMessage>(json, JsonOptions);

        Assert.NotNull(shadow);
        Assert.Equal("esp-01", shadow!.DeviceId);
        Assert.Equal(17u, shadow.ShadowVersion);
        Assert.Equal("visualizer", shadow.Mode);
        Assert.Equal("win-eliel", shadow.ActiveClientId);
        Assert.Equal(4u, shadow.ActiveOwnerEpoch);
        Assert.Equal(4200, shadow.OwnerLeaseRemainingMs);
        Assert.True(shadow.LockHeld);
        Assert.Equal("win-eliel", shadow.LockClientId);
        Assert.Equal("settings", shadow.LockReason);
        Assert.Equal(11000, shadow.LockLeaseRemainingMs);
        Assert.Equal("visualizer-hub75", shadow.ActiveAppId);
        Assert.Equal("none", shadow.FallbackState);
    }
}
