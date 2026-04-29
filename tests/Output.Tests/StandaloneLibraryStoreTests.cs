using Device.Protocol.Models;
using MicaAudio.Server;

namespace Output.Tests;

public sealed class StandaloneLibraryStoreTests
{
    [Fact]
    public async Task PanelLibraryStore_ShouldSaveAtomicallyAndRecoverCorruptJsonAsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-library-tests", Guid.NewGuid().ToString("N"));
        using var store = new StandalonePanelLibraryStore(root);
        var document = new PanelLibraryDocument
        {
            LastSelectedPanelId = "panel-a",
            ActivePanels =
            [
                new PanelDeviceState
                {
                    DeviceId = "device-a",
                    ActivePanelId = "panel-a",
                    ActiveAppId = "panels-hub75",
                    LastServerOwnedPanelId = "panel-a",
                    UpdatedAtUtc = new DateTimeOffset(2026, 4, 29, 13, 0, 0, TimeSpan.Zero),
                },
            ],
            Panels =
            [
                new PanelLibraryItem
                {
                    PanelId = "panel-a",
                    Name = "Persisted panel",
                    Width = 128,
                    Height = 64,
                    IsEnabled = true,
                    Widgets =
                    [
                        new PanelWidgetItem
                        {
                            WidgetId = "clock-a",
                            AppId = "clockhub75",
                            DataSource = PanelWidgetDataSources.Server,
                        },
                    ],
                },
            ],
        };

        await store.SaveAsync(document);

        var loaded = await store.LoadAsync();
        Assert.Equal("panel-a", loaded.LastSelectedPanelId);
        var activePanel = Assert.Single(loaded.ActivePanels);
        Assert.Equal("device-a", activePanel.DeviceId);
        Assert.Equal("panel-a", activePanel.ActivePanelId);
        var loadedPanel = Assert.Single(loaded.Panels);
        Assert.Equal("Persisted panel", loadedPanel.Name);
        Assert.Equal(PanelWidgetDataSources.Server, Assert.Single(loadedPanel.Widgets).DataSource);

        await File.WriteAllTextAsync(Path.Combine(root, "panels", "panels.json"), "{ broken json");
        var recovered = await store.LoadAsync();
        Assert.Empty(recovered.Panels);
        Assert.Empty(recovered.ActivePanels);
        Assert.Null(recovered.LastSelectedPanelId);
    }

    [Fact]
    public async Task MediaLibraryStore_ShouldDeduplicateBySha256AndPersistIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-library-tests", Guid.NewGuid().ToString("N"));
        using var store = new StandaloneMediaLibraryStore(root);
        var payload = "GIF89a-shared"u8.ToArray();

        var first = await store.SaveAsync("first.gif", "image/gif", payload, maxUploadBytes: 1024);
        var second = await store.SaveAsync("second.gif", "image/gif", payload, maxUploadBytes: 1024);

        Assert.Equal(first.MediaId, second.MediaId);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(payload.LongLength, first.SizeBytes);

        var indexed = await store.LoadIndexAsync();
        Assert.Single(indexed);
        Assert.Equal(first.MediaId, indexed[0].MediaId);

        var loadedPayload = await store.ReadBytesAsync(first.MediaId);
        Assert.Equal(payload, loadedPayload);
    }
}
