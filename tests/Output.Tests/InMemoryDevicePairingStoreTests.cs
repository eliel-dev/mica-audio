using Device.Server.Hosting;

namespace Output.Tests;

public sealed class InMemoryDevicePairingStoreTests
{
    [Fact]
    public void SaveCode_ShouldReturnPairingCodeInfoWithExpiry()
    {
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryDevicePairingStore();

        var info = store.SaveCode("123456", TimeSpan.FromSeconds(30), now);

        Assert.Equal("123456", info.Code);
        Assert.Equal(now.AddSeconds(30), info.ExpiresAtUtc);
    }

    [Fact]
    public void TryConsumeCode_ShouldConsumeOnlyOnceCaseInsensitive()
    {
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryDevicePairingStore();
        store.SaveCode("ABC123", TimeSpan.FromSeconds(30), now);

        Assert.True(store.TryConsumeCode("abc123", now.AddSeconds(1)));
        Assert.False(store.TryConsumeCode("ABC123", now.AddSeconds(2)));
    }

    [Fact]
    public void TryConsumeCode_ShouldFailAfterExpiry()
    {
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryDevicePairingStore();
        store.SaveCode("999999", TimeSpan.FromSeconds(30), now);

        Assert.False(store.TryConsumeCode("999999", now.AddSeconds(31)));
    }

    [Fact]
    public void TryRegisterAttempt_ShouldThrottleWithinWindowAndResetAfterWindow()
    {
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryDevicePairingStore();

        Assert.True(store.TryRegisterAttempt("127.0.0.1", 2, TimeSpan.FromSeconds(30), now, out var retryAfterSeconds));
        Assert.Equal(0, retryAfterSeconds);
        Assert.True(store.TryRegisterAttempt("127.0.0.1", 2, TimeSpan.FromSeconds(30), now.AddSeconds(1), out retryAfterSeconds));
        Assert.Equal(0, retryAfterSeconds);

        Assert.False(store.TryRegisterAttempt("127.0.0.1", 2, TimeSpan.FromSeconds(30), now.AddSeconds(2), out retryAfterSeconds));
        Assert.InRange(retryAfterSeconds, 1, 30);

        Assert.True(store.TryRegisterAttempt("127.0.0.1", 2, TimeSpan.FromSeconds(30), now.AddSeconds(31), out retryAfterSeconds));
        Assert.Equal(0, retryAfterSeconds);
    }

    [Fact]
    public void ResetAttempts_ShouldClearOnlyMatchingRemoteIp()
    {
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryDevicePairingStore();

        Assert.True(store.TryRegisterAttempt("127.0.0.1", 1, TimeSpan.FromSeconds(30), now, out _));
        Assert.True(store.TryRegisterAttempt("127.0.0.2", 1, TimeSpan.FromSeconds(30), now, out _));
        Assert.False(store.TryRegisterAttempt("127.0.0.1", 1, TimeSpan.FromSeconds(30), now.AddSeconds(1), out _));
        Assert.False(store.TryRegisterAttempt("127.0.0.2", 1, TimeSpan.FromSeconds(30), now.AddSeconds(1), out _));

        store.ResetAttempts("127.0.0.1");

        Assert.True(store.TryRegisterAttempt("127.0.0.1", 1, TimeSpan.FromSeconds(30), now.AddSeconds(2), out _));
        Assert.False(store.TryRegisterAttempt("127.0.0.2", 1, TimeSpan.FromSeconds(30), now.AddSeconds(2), out _));
    }

    [Fact]
    public void Clear_ShouldRemoveCodesAndAttempts()
    {
        var now = new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero);
        var store = new InMemoryDevicePairingStore();
        store.SaveCode("123456", TimeSpan.FromSeconds(30), now);
        Assert.True(store.TryRegisterAttempt("127.0.0.1", 1, TimeSpan.FromSeconds(30), now, out _));
        Assert.False(store.TryRegisterAttempt("127.0.0.1", 1, TimeSpan.FromSeconds(30), now.AddSeconds(1), out _));

        store.Clear();

        Assert.False(store.TryConsumeCode("123456", now.AddSeconds(2)));
        Assert.True(store.TryRegisterAttempt("127.0.0.1", 1, TimeSpan.FromSeconds(30), now.AddSeconds(2), out var retryAfterSeconds));
        Assert.Equal(0, retryAfterSeconds);
    }
}
