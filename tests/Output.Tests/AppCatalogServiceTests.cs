using System.Text.Json;
using App.WinUI.Services.Apps;

namespace Output.Tests;

public sealed class AppCatalogServiceTests
{
    [Fact]
    public async Task LoadCatalogAsync_ShouldInjectEnabledDefaultsWhenCatalogIsOutdated()
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
                        name = "Clima custom",
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
                        id = "analogclock",
                        name = "Relogio custom",
                        summary = "relogio",
                        description = "relogio",
                        author = "tests",
                        packageName = "analogclock",
                        fileName = "analogclock.star",
                        recommendedIntervalMinutes = 0,
                        category = "relogio",
                    },
                },
            };

            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(document));

            var service = new AppCatalogService(root);
            var items = await service.LoadCatalogAsync();

            Assert.Contains(items, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(3, items.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadCatalogAsync_ShouldIncludeGifHub75WhenEnabled()
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
                        id = "gifhub75",
                        name = "GIF HUB75 custom",
                        summary = "gif",
                        description = "gif",
                        author = "tests",
                        packageName = "gifhub75",
                        fileName = "gifhub75.star",
                        recommendedIntervalMinutes = 0,
                        category = "midia",
                    },
                    new
                    {
                        id = "not-enabled",
                        name = "not enabled",
                        summary = "n/a",
                        description = "n/a",
                        author = "tests",
                        packageName = "not-enabled",
                        fileName = "not-enabled.star",
                        recommendedIntervalMinutes = 0,
                        category = "geral",
                    },
                },
            };

            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(document));

            var service = new AppCatalogService(root);
            var items = await service.LoadCatalogAsync();

            Assert.Contains(items, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(items, item => string.Equals(item.Id, "not-enabled", StringComparison.OrdinalIgnoreCase));
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
}
