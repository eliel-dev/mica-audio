using System.Net;
using Device.Protocol.Contracts;
using Device.Server.Hosting;

namespace Output.Tests;

public class DeviceServerRuntimeConfigTests
{
    [Fact]
    public void From_ShouldNormalizeLimitsAndParseCidrs()
    {
        var config = DeviceServerRuntimeConfig.From(new ServerConfig
        {
            AllowedCidrs = ["192.168.10.0/24", "invalid"],
            PairRequestsPerMinute = 0,
            CommandAckRequestsPerSecond = -3,
            WebSocketHandshakesPerMinute = 0,
            PairingAttemptsPerWindow = 0,
            PairingAttemptWindowSeconds = 3,
            DeviceFreshThresholdSeconds = 999,
            MaxJsonBodyBytes = 32,
            MaxWebSocketMessageBytes = 16,
        });

        Assert.Equal(1, config.PairRequestsPerMinute);
        Assert.Equal(1, config.CommandAckRequestsPerSecond);
        Assert.Equal(1, config.WebSocketHandshakesPerMinute);
        Assert.Equal(1, config.PairingAttemptsPerWindow);
        Assert.Equal(TimeSpan.FromSeconds(10), config.PairingAttemptWindow);
        Assert.Equal(TimeSpan.FromSeconds(120), config.DeviceOfflineTimeout);
        Assert.Equal(1024L, config.MaxJsonBodyBytes);
        Assert.Equal(1024, config.MaxWebSocketMessageBytes);
        Assert.True(config.HasConfiguredAllowedCidrs);
        Assert.Single(config.AllowedCidrs);
        Assert.True(config.AllowedCidrs[0].Contains(IPAddress.Parse("192.168.10.42")));
        Assert.False(config.AllowedCidrs[0].Contains(IPAddress.Parse("10.0.0.42")));
    }
}
