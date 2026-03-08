using System.Text.Json;
using App.WinUI.Services.Apps;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Output.Tests;

public sealed class AppCatalogServiceTests
{
    [Fact]
    public async Task LoadCatalogAsync_ShouldKeepOnlySeedSupportedApps()
    {
        var root = CreateTempRoot();
        try
        {
            var appsDir = Path.Combine(root, "apps");
            Directory.CreateDirectory(appsDir);

            var catalogPath = Path.Combine(appsDir, "catalog.json");
            var document = new
            {
                schemaVersion = 2,
                apps = new object[]
                {
                    new
                    {
                        id = "accuweather",
                        name = "Clima",
                        summary = "clima",
                        description = "clima",
                        author = "tests",
                        packageName = "accuweather",
                        fileName = "accuweather.star",
                        recommendedIntervalMinutes = 5,
                        category = "clima",
                    },
                    new
                    {
                        id = "newapp",
                        name = "App Nova",
                        summary = "nova",
                        description = "nova",
                        author = "tests",
                        packageName = "newapp",
                        fileName = "newapp.star",
                        recommendedIntervalMinutes = 1,
                        category = "geral",
                    },
                },
            };

            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(document));

            var service = new AppCatalogService(CreateOptions(root));
            var items = await service.LoadCatalogAsync();

            Assert.Equal(3, items.Count);
            Assert.DoesNotContain(items, item => string.Equals(item.Id, "newapp", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "accuweather", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "analogclock", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadCatalogAsync_ShouldFallbackToSeed_WhenSchemaVersionIsUnsupported()
    {
        var root = CreateTempRoot();
        try
        {
            var appsDir = Path.Combine(root, "apps");
            Directory.CreateDirectory(appsDir);

            var catalogPath = Path.Combine(appsDir, "catalog.json");
            var document = new
            {
                schemaVersion = 999,
                apps = new object[]
                {
                    new
                    {
                        id = "accuweather",
                        name = "Clima",
                        packageName = "accuweather",
                    },
                },
            };

            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(document));

            var service = new AppCatalogService(CreateOptions(root));
            var items = await service.LoadCatalogAsync();

            Assert.Equal(3, items.Count);
            Assert.Contains(items, item => string.Equals(item.Id, "accuweather", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "analogclock", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadCatalogAsync_ShouldFallbackToSeed_WhenCatalogIsInvalidJson()
    {
        var root = CreateTempRoot();
        try
        {
            var appsDir = Path.Combine(root, "apps");
            Directory.CreateDirectory(appsDir);

            var catalogPath = Path.Combine(appsDir, "catalog.json");
            await File.WriteAllTextAsync(catalogPath, "{ invalid json }");

            var service = new AppCatalogService(CreateOptions(root));
            var items = await service.LoadCatalogAsync();

            Assert.Equal(3, items.Count);
            Assert.Contains(items, item => string.Equals(item.Id, "accuweather", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "analogclock", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mica-audio-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
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
