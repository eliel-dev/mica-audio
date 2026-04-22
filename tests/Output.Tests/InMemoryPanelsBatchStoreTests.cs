using System.Security.Cryptography;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class InMemoryPanelsBatchStoreTests
{
    [Fact]
    public void Save_ShouldStorePayloadAndMetadata()
    {
        var store = new InMemoryPanelsBatchStore();
        var payload = "RIFFdemoWEBP"u8.ToArray();

        var saved = store.Save(new PanelsBatchWrite(
            DeviceId: "device-1",
            PanelsSessionId: "session-a",
            BatchSequence: 7,
            Payload: payload,
            FrameCount: 30,
            DurationMs: 1000,
            ContentType: "image/webp"));

        Assert.Equal("device-1", saved.DeviceId);
        Assert.Equal("session-a", saved.PanelsSessionId);
        Assert.Equal((ulong)7, saved.BatchSequence);
        Assert.Equal(payload.LongLength, saved.FileSizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(payload)), saved.Sha256);
        Assert.Equal("image/webp", saved.ContentType);
        Assert.Equal(30, saved.FrameCount);
        Assert.Equal(1000, saved.DurationMs);
        Assert.Equal(payload, saved.Payload);

        Assert.True(store.TryGet("device-1", "session-a", 7, out var found));
        Assert.NotNull(found);
        Assert.Equal(saved, found);
    }

    [Fact]
    public void TryGet_ShouldRequireDeviceSessionAndSequence()
    {
        var store = new InMemoryPanelsBatchStore();
        store.Save(CreateWrite("device-1", "session-a", 1));

        Assert.True(store.TryGet("device-1", "session-a", 1, out _));
        Assert.False(store.TryGet("device-other", "session-a", 1, out _));
        Assert.False(store.TryGet("device-1", "session-other", 1, out _));
        Assert.False(store.TryGet("device-1", "session-a", 2, out _));
    }

    [Fact]
    public void Save_ShouldRemovePreviousSessionBatchesForSameDevice()
    {
        var store = new InMemoryPanelsBatchStore();
        store.Save(CreateWrite("device-1", "session-a", 1));
        store.Save(CreateWrite("device-1", "session-a", 2));

        store.Save(CreateWrite("device-1", "session-b", 3));

        Assert.False(store.TryGet("device-1", "session-a", 1, out _));
        Assert.False(store.TryGet("device-1", "session-a", 2, out _));
        Assert.True(store.TryGet("device-1", "session-b", 3, out _));
    }

    [Fact]
    public void Save_ShouldKeepLatestFourBatchesPerDevice()
    {
        var store = new InMemoryPanelsBatchStore();

        for (ulong sequence = 1; sequence <= 5; sequence++)
        {
            store.Save(CreateWrite("device-1", "session-a", sequence));
        }

        Assert.False(store.TryGet("device-1", "session-a", 1, out _));
        for (ulong sequence = 2; sequence <= 5; sequence++)
        {
            Assert.True(store.TryGet("device-1", "session-a", sequence, out _));
        }
    }

    [Fact]
    public void Clear_ShouldRemoveAllOrOnlyMatchingSession()
    {
        var store = new InMemoryPanelsBatchStore();
        store.Save(CreateWrite("device-1", "session-a", 1));
        store.Save(CreateWrite("device-2", "session-a", 1));

        store.Clear("device-1", "session-a");

        Assert.False(store.TryGet("device-1", "session-a", 1, out _));
        Assert.True(store.TryGet("device-2", "session-a", 1, out _));

        store.Save(CreateWrite("device-1", "session-a", 1));
        store.Clear("device-1");

        Assert.False(store.TryGet("device-1", "session-a", 1, out _));
        Assert.True(store.TryGet("device-2", "session-a", 1, out _));
    }

    private static PanelsBatchWrite CreateWrite(string deviceId, string panelsSessionId, ulong batchSequence)
        => new(
            deviceId,
            panelsSessionId,
            batchSequence,
            Payload: [(byte)batchSequence],
            FrameCount: 30,
            DurationMs: 1000,
            ContentType: "image/webp");
}
