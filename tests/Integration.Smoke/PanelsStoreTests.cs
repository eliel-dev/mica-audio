using App.WinUI.Models.Panels;
using App.WinUI.Services.Panels;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Integration.Smoke;

public sealed class PanelsStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_ShouldPersistPanelsWidgetsAndGifRuntimeState()
    {
        var root = CreateTempDirectory();
        try
        {
            var storePath = Path.Combine(root, "panels", "panels.json");
            using var writer = CreateStore(root, storePath);

            var document = new PanelsStoreDocument
            {
                LastSelectedPanelId = "panel-b",
                Panels =
                [
                    new PanelDefinition
                    {
                        PanelId = "panel-b",
                        Name = "Painel B",
                        Widgets =
                        [
                            new PanelWidgetDefinition
                            {
                                WidgetId = "gif-1",
                                AppId = "gifhub75",
                                X = 4,
                                Y = 5,
                                Width = 24,
                                Height = 12,
                                ZIndex = 2,
                                ConfigValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["sourceType"] = "single",
                                },
                                RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["sourcePath"] = @"C:\media\one.gif",
                                },
                            },
                            new PanelWidgetDefinition
                            {
                                WidgetId = "gif-2",
                                AppId = "gifhub75",
                                X = 30,
                                Y = 10,
                                Width = 20,
                                Height = 20,
                                ZIndex = 3,
                                RuntimeState = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["sourcePath"] = @"C:\media\two.gif",
                                },
                            },
                        ],
                    },
                    new PanelDefinition
                    {
                        PanelId = "panel-a",
                        Name = "Painel A",
                    },
                ],
            };

            await writer.SaveAsync(document);

            using var reader = CreateStore(root, storePath);
            var loaded = await reader.LoadAsync();

            Assert.Equal(PanelsStoreDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal("panel-b", loaded.LastSelectedPanelId);
            Assert.Equal(2, loaded.Panels.Count);
            Assert.Equal(["Painel A", "Painel B"], loaded.Panels.Select(static panel => panel.Name).ToArray());

            var selectedPanel = Assert.Single(loaded.Panels, static panel => panel.PanelId == "panel-b");
            Assert.Equal(2, selectedPanel.Widgets.Count);
            Assert.All(selectedPanel.Widgets, static widget => Assert.Equal("gifhub75", widget.AppId));
            Assert.Equal(@"C:\media\one.gif", selectedPanel.Widgets[0].RuntimeState["sourcePath"]);
            Assert.Equal(@"C:\media\two.gif", selectedPanel.Widgets[1].RuntimeState["sourcePath"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldNormalizeInvalidSelectedPanelId_ToFirstPanel()
    {
        var root = CreateTempDirectory();
        try
        {
            var storePath = Path.Combine(root, "panels", "panels.json");
            using var store = CreateStore(root, storePath);

            await store.SaveAsync(new PanelsStoreDocument
            {
                LastSelectedPanelId = "missing",
                Panels =
                [
                    new PanelDefinition { PanelId = "panel-z", Name = "Zulu" },
                    new PanelDefinition { PanelId = "panel-a", Name = "Alpha" },
                ],
            });

            var loaded = await store.LoadAsync();

            Assert.Equal("panel-a", loaded.LastSelectedPanelId);
            Assert.Equal(["Alpha", "Zulu"], loaded.Panels.Select(static panel => panel.Name).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PanelsStore CreateStore(string appDataRoot, string storePath)
    {
        return new PanelsStore(Options.Create(new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            PanelsFilePath = storePath,
        }));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
