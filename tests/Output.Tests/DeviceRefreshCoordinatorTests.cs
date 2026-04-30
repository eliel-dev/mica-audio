using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceRefreshCoordinatorTests
{
    [Fact]
    public void Apply_ShouldKeepEquivalentOnlineSnapshotWithoutPublish()
    {
        var coordinator = new DeviceRefreshCoordinator();
        var now = DateTimeOffset.UtcNow;
        var snapshot = CreateSnapshot("device-1", DeviceStatus.Online, now);

        var first = coordinator.Apply([snapshot], forcePublish: false, refreshedAtUtc: now);
        var second = coordinator.Apply([snapshot], forcePublish: false, refreshedAtUtc: now.AddSeconds(1));

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Single(second.CurrentSnapshot);
    }

    [Fact]
    public void Apply_ShouldForcePublishWhenOfflineSnapshotRemainsVisible()
    {
        var coordinator = new DeviceRefreshCoordinator();
        var snapshot = CreateSnapshot("device-1", DeviceStatus.Offline, DateTimeOffset.UtcNow);

        var first = coordinator.Apply([snapshot], forcePublish: false, refreshedAtUtc: DateTimeOffset.UtcNow);
        var second = coordinator.Apply([snapshot], forcePublish: false, refreshedAtUtc: DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.True(first.Changed);
        Assert.True(second.Changed);
    }

    [Fact]
    public void Apply_ShouldIgnoreWebSocketConnectivityEventChurn()
    {
        var coordinator = new DeviceRefreshCoordinator();
        var now = DateTimeOffset.UtcNow;
        var stableSnapshot = CreateSnapshot("device-1", DeviceStatus.Online, now, lastWifiEvent: "wifi_connected");
        var wsChurnSnapshot = CreateSnapshot("device-1", DeviceStatus.Online, now, lastWifiEvent: "ws_disconnected");

        var first = coordinator.Apply([stableSnapshot], forcePublish: false, refreshedAtUtc: now);
        var second = coordinator.Apply([wsChurnSnapshot], forcePublish: false, refreshedAtUtc: now.AddSeconds(1));

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal("wifi_connected", second.CurrentSnapshot.Single().LastWifiEvent);
    }

    [Fact]
    public void TryEnterRefresh_ShouldRejectConcurrentRefresh()
    {
        var coordinator = new DeviceRefreshCoordinator();

        Assert.True(coordinator.TryEnterRefresh());
        Assert.False(coordinator.TryEnterRefresh());

        coordinator.ExitRefresh();

        Assert.True(coordinator.TryEnterRefresh());
        coordinator.ExitRefresh();
    }

    private static DeviceSnapshot CreateSnapshot(
        string deviceId,
        DeviceStatus status,
        DateTimeOffset now,
        string? lastWifiEvent = null)
    {
        return new DeviceSnapshot
        {
            DeviceId = deviceId,
            Name = deviceId,
            Profile = "dma_exp",
            Status = status,
            IsRegistered = true,
            FirstSeenUtc = now.AddMinutes(-10),
            LastSeenUtc = now,
            LastTelemetryUtc = status == DeviceStatus.Online ? now : null,
            LastWifiEvent = lastWifiEvent,
        };
    }
}
