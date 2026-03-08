using App.WinUI.Services.Apps;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Output.Tests;

public sealed class AppCatalogRuntimeKindTests
{
    [Fact]
    public async Task LoadCatalogAsync_AssignsRuntimeKindByCapability()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var appsDir = Path.Combine(root, "apps");
            Directory.CreateDirectory(appsDir);

            var catalog = """
            {
              "schemaVersion": 2,
              "apps": [
                { "id": "accuweather", "name": "Clima", "packageName": "accuweather" },
                { "id": "analogclock", "name": "Relogio", "packageName": "analogclock" },
                { "id": "gifhub75", "name": "Gif", "packageName": "gifhub75", "runtime": { "kind": "gifhub75" } }
              ]
            }
            """;

            await File.WriteAllTextAsync(Path.Combine(appsDir, "catalog.json"), catalog);

            var service = new AppCatalogService(CreateOptions(root));
            var items = await service.LoadCatalogAsync();

            Assert.Equal("none", items.Single(x => x.Id == "accuweather").Runtime?.Kind);
            Assert.Equal("none", items.Single(x => x.Id == "analogclock").Runtime?.Kind);
            Assert.Equal("gifhub75", items.Single(x => x.Id == "gifhub75").Runtime?.Kind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IOptions<MicaAudioOptions> CreateOptions(string root)
    {
        return Options.Create(new MicaAudioOptions
        {
            AppDataRoot = root,
            AppsCatalogPath = Path.Combine(root, "apps", "catalog.json"),
        });
    }
}
