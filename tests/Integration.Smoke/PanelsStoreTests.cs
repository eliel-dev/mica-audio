using App.WinUI.Models.Panels;
using App.WinUI.Services.Panels;
using Device.Client;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task LoadAsync_ShouldReturnEmptyDocument_WhenFileDoesNotExist()
    {
        var root = CreateTempDirectory();
        try
        {
            var storePath = Path.Combine(root, "panels", "panels.json");
            using var store = CreateStore(root, storePath);

            var loaded = await store.LoadAsync();

            Assert.Equal(PanelsStoreDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Empty(loaded.Panels);
            Assert.Null(loaded.LastSelectedPanelId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnEmptyDocument_WhenFileIsZeroBytes()
    {
        var root = CreateTempDirectory();
        try
        {
            var storePath = Path.Combine(root, "panels", "panels.json");
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);
            await File.WriteAllBytesAsync(storePath, []);

            using var store = CreateStore(root, storePath);
            var loaded = await store.LoadAsync();

            Assert.Equal(PanelsStoreDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Empty(loaded.Panels);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(storePath)!, "panels.json.corrupt-*.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldQuarantineInvalidJson_AndReturnEmptyDocument()
    {
        var root = CreateTempDirectory();
        try
        {
            var storePath = Path.Combine(root, "panels", "panels.json");
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);
            await File.WriteAllTextAsync(storePath, "{ invalid json }");

            using var store = CreateStore(root, storePath);
            var loaded = await store.LoadAsync();

            Assert.Equal(PanelsStoreDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Empty(loaded.Panels);
            Assert.False(File.Exists(storePath));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(storePath)!, "panels.json.corrupt-*.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldUseAtomicReplace_AndLeaveNoTempFile()
    {
        var root = CreateTempDirectory();
        try
        {
            var storePath = Path.Combine(root, "panels", "panels.json");
            using var store = CreateStore(root, storePath);

            await store.SaveAsync(new PanelsStoreDocument
            {
                Panels =
                [
                    new PanelDefinition { PanelId = "panel-a", Name = "Alpha" },
                ],
            });

            await store.SaveAsync(new PanelsStoreDocument
            {
                LastSelectedPanelId = "panel-b",
                Panels =
                [
                    new PanelDefinition { PanelId = "panel-b", Name = "Beta" },
                ],
            });

            Assert.True(File.Exists(storePath));
            Assert.True(File.Exists(storePath + ".bak"));
            Assert.False(File.Exists(storePath + ".tmp"));

            var json = await File.ReadAllTextAsync(storePath);
            Assert.Contains("Beta", json, StringComparison.Ordinal);

            var loaded = await store.LoadAsync();
            Assert.Equal("panel-b", loaded.LastSelectedPanelId);
            Assert.Equal("Beta", Assert.Single(loaded.Panels).Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldMigrateLocalPanelsToServer_WhenServerLibraryIsEmpty()
    {
        var root = CreateTempDirectory();
        try
        {
            var storePath = Path.Combine(root, "panels", "panels.json");
            using (var localWriter = CreateStore(root, storePath))
            {
                await localWriter.SaveAsync(new PanelsStoreDocument
                {
                    LastSelectedPanelId = "panel-local",
                    Panels =
                    [
                        new PanelDefinition
                        {
                            PanelId = "panel-local",
                            Name = "Local importado",
                            Widgets =
                            [
                                new PanelWidgetDefinition
                                {
                                    WidgetId = "widget-local",
                                    AppId = "gifhub75",
                                    X = 3,
                                    Y = 4,
                                    Width = 32,
                                    Height = 24,
                                    ConfigValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        ["mediaId"] = "media-local",
                                    },
                                },
                            ],
                        },
                    ],
                });
            }

            var server = new CapturingDeviceServerClient(new PanelLibraryDocument());
            using var store = CreateStore(root, storePath, server);

            var loaded = await store.LoadAsync();

            Assert.Equal("panel-local", loaded.LastSelectedPanelId);
            Assert.NotNull(server.SavedDocument);
            var saved = server.SavedDocument!;
            Assert.Equal("panel-local", saved.LastSelectedPanelId);
            var panel = Assert.Single(saved.Panels);
            Assert.Equal("Local importado", panel.Name);
            var widget = Assert.Single(panel.Widgets);
            Assert.Equal("gifhub75", widget.AppId);
            Assert.Equal("media-local", widget.ConfigValues["mediaId"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PanelsStore CreateStore(string appDataRoot, string storePath, IDeviceServerClient? deviceServerClient = null)
    {
        return new PanelsStore(Options.Create(new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            PanelsFilePath = storePath,
        }), NullLogger<PanelsStore>.Instance, deviceServerClient);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CapturingDeviceServerClient : IDeviceServerClient
    {
        private readonly PanelLibraryDocument document;

        public CapturingDeviceServerClient(PanelLibraryDocument document)
        {
            this.document = document;
        }

        public event EventHandler? DevicesChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? LogMessage
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
        {
            add { }
            remove { }
        }

        public PanelLibraryDocument? SavedDocument { get; private set; }

        public string GetServerBaseAddress() => "http://127.0.0.1:5272";

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
            => Task.FromResult(new CommandDispatchResult());

        public Task<PanelLibraryDocument> GetPanelLibraryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(document);

        public Task SavePanelLibraryAsync(PanelLibraryDocument document, CancellationToken cancellationToken = default)
        {
            SavedDocument = document;
            return Task.CompletedTask;
        }
    }
}
