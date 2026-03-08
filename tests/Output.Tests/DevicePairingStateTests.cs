using Device.Protocol.Contracts;
using Device.Server.Hosting;

namespace Output.Tests;

public class DevicePairingStateTests
{
    [Fact]
    public void TryConsume_ShouldFailAfterExpiryWithControlledTime()
    {
        var timeProvider = new ManualTimeProvider();
        var state = new DevicePairingState();
        var info = state.CreateCode("123456", TimeSpan.FromSeconds(30), timeProvider);

        Assert.Equal("123456", info.Code);
        Assert.True(state.TryConsume("123456", timeProvider));

        info = state.CreateCode("999999", TimeSpan.FromSeconds(30), timeProvider);
        timeProvider.Advance(TimeSpan.FromSeconds(31));

        Assert.False(state.TryConsume("999999", timeProvider));
    }

    [Fact]
    public void TryRegisterAttempt_ShouldThrottleWithinWindowAndResetAfterAdvance()
    {
        var timeProvider = new ManualTimeProvider();
        var config = DeviceServerRuntimeConfig.From(new ServerConfig
        {
            PairingAttemptsPerWindow = 2,
            PairingAttemptWindowSeconds = 30,
        });
        var state = new DevicePairingState();

        Assert.True(state.TryRegisterAttempt("127.0.0.1", config, timeProvider, out var retryAfterSeconds));
        Assert.Equal(0, retryAfterSeconds);
        Assert.True(state.TryRegisterAttempt("127.0.0.1", config, timeProvider, out retryAfterSeconds));
        Assert.Equal(0, retryAfterSeconds);

        Assert.False(state.TryRegisterAttempt("127.0.0.1", config, timeProvider, out retryAfterSeconds));
        Assert.InRange(retryAfterSeconds, 1, 30);

        timeProvider.Advance(TimeSpan.FromSeconds(31));

        Assert.True(state.TryRegisterAttempt("127.0.0.1", config, timeProvider, out retryAfterSeconds));
        Assert.Equal(0, retryAfterSeconds);
    }
}
