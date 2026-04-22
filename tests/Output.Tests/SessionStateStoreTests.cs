using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class SessionStateStoreTests
{
    [Fact]
    public void Store_ShouldSetReplaceGetRemoveAndDrainCaseInsensitive()
    {
        var now = new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero);
        var store = new InMemorySessionStateStore();
        var first = new DeviceSessionState(CreateRecord("device-1", now), TimeSpan.FromMilliseconds(500));
        var replacement = new DeviceSessionState(CreateRecord("DEVICE-1", now.AddSeconds(1)), TimeSpan.FromMilliseconds(500));
        var second = new DeviceSessionState(CreateRecord("device-2", now.AddSeconds(2)), TimeSpan.FromMilliseconds(500));

        Assert.Null(store.Upsert(first));
        Assert.Same(first, store.Upsert(replacement));
        Assert.Null(store.Upsert(second));

        Assert.Equal(2, store.Count);
        Assert.True(store.TryGetValue("device-1", out var found));
        Assert.Same(replacement, found);

        Assert.True(store.Remove("DEVICE-1", out var removed));
        Assert.Same(replacement, removed);

        var drained = store.Drain();
        Assert.Single(drained);
        Assert.Same(second, drained[0]);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void CreateRecords_ShouldOrderByLastSeenDescending()
    {
        var now = new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero);
        var store = new InMemorySessionStateStore();
        store.Upsert(new DeviceSessionState(CreateRecord("old", now), TimeSpan.FromMilliseconds(500)));
        store.Upsert(new DeviceSessionState(CreateRecord("new", now.AddSeconds(5)), TimeSpan.FromMilliseconds(500)));

        var records = store.CreateRecords();

        Assert.Collection(
            records,
            record => Assert.Equal("new", record.DeviceId),
            record => Assert.Equal("old", record.DeviceId));
    }

    [Fact]
    public void CreateSnapshots_ShouldUseSessionSnapshotState()
    {
        var now = new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero);
        var store = new InMemorySessionStateStore();
        var state = new DeviceSessionState(CreateRecord("device-1", now), TimeSpan.FromMilliseconds(500));
        state.MarkControlPlaneConnected(now.AddSeconds(1), "192.168.0.10");
        store.Upsert(state);

        var snapshot = Assert.Single(store.CreateSnapshots(now.AddSeconds(2), TimeSpan.FromSeconds(15)));

        Assert.Equal("device-1", snapshot.DeviceId);
        Assert.Equal(DeviceStatus.Online, snapshot.Status);
        Assert.Equal(DeviceControlPlaneState.MqttOnline, snapshot.ControlPlaneState);
    }

    private static DeviceRecord CreateRecord(string deviceId, DateTimeOffset lastSeenUtc)
        => new()
        {
            DeviceId = deviceId,
            Token = $"token-{deviceId}",
            Name = deviceId,
            CreatedAtUtc = lastSeenUtc,
            LastSeenUtc = lastSeenUtc,
            IsRegistered = true,
        };
}
