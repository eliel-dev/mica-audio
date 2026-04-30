using System.Text.Json;
using App.WinUI.Services.Apps;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
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

            using var harness = CreateService(root);
            var service = harness.Service;
            var items = await service.LoadCatalogAsync();

            Assert.Equal(2, items.Count);
            Assert.DoesNotContain(items, item => string.Equals(item.Id, "newapp", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(items, item => string.Equals(item.Id, "accuweather", StringComparison.OrdinalIgnoreCase));
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

            using var harness = CreateService(root);
            var service = harness.Service;
            var items = await service.LoadCatalogAsync();

            Assert.Equal(2, items.Count);
            Assert.DoesNotContain(items, item => string.Equals(item.Id, "accuweather", StringComparison.OrdinalIgnoreCase));
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

            using var harness = CreateService(root);
            var service = harness.Service;
            var items = await service.LoadCatalogAsync();

            Assert.Equal(2, items.Count);
            Assert.DoesNotContain(items, item => string.Equals(item.Id, "accuweather", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "analogclock", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadCatalogAsync_ShouldReturnCachedResult_UntilExplicitReload()
    {
        var root = CreateTempRoot();
        try
        {
            var catalogPath = await WriteCatalogAsync(root, "analogclock");
            using var harness = CreateService(root);
            var service = harness.Service;

            var initialItems = await service.LoadCatalogAsync();

            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                apps = new object[]
                {
                    new
                    {
                        id = "gifhub75",
                        name = "GIF",
                        summary = "midia",
                        description = "midia",
                        author = "tests",
                        packageName = "gifhub75",
                        fileName = "gifhub75.star",
                        recommendedIntervalMinutes = 0,
                        category = "midia",
                    },
                },
            }));

            var cachedItems = await service.LoadCatalogAsync();

            Assert.Equal(initialItems.Select(static item => item.Id), cachedItems.Select(static item => item.Id));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReloadCatalogAsync_ShouldBypassCachedResult_AndObserveDiskChanges()
    {
        var root = CreateTempRoot();
        try
        {
            var catalogPath = await WriteCatalogAsync(root, "analogclock");
            using var harness = CreateService(root);
            var service = harness.Service;

            _ = await service.LoadCatalogAsync();

            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                apps = new object[]
                {
                    new
                    {
                        id = "gifhub75",
                        name = "GIF",
                        summary = "midia",
                        description = "midia",
                        author = "tests",
                        packageName = "gifhub75",
                        fileName = "gifhub75.star",
                        recommendedIntervalMinutes = 0,
                        category = "midia",
                    },
                },
            }));

            var reloadedItems = await service.ReloadCatalogAsync();
            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                apps = new object[]
                {
                    new
                    {
                        id = "analogclock",
                        name = "Relogio",
                        summary = "tempo",
                        description = "tempo",
                        author = "tests",
                        packageName = "analogclock",
                        fileName = "analogclock.star",
                        recommendedIntervalMinutes = 0,
                        category = "geral",
                    },
                },
            }));

            var cachedAfterReload = await service.LoadCatalogAsync();

            Assert.Contains(reloadedItems, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(cachedAfterReload, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReloadCatalogAsync_ShouldInvalidateAfterNormalizationWrite()
    {
        var root = CreateTempRoot();
        try
        {
            var appsDir = Path.Combine(root, "apps");
            Directory.CreateDirectory(appsDir);

            var catalogPath = Path.Combine(appsDir, "catalog.json");
            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 999,
                apps = new object[]
                {
                    new
                    {
                        id = "analogclock",
                        name = "Relogio",
                        packageName = "analogclock",
                    },
                },
            }));

            using var harness = CreateService(root);
            var service = harness.Service;

            _ = await service.LoadCatalogAsync();
            await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                apps = new object[]
                {
                    new
                    {
                        id = "gifhub75",
                        name = "GIF",
                        summary = "midia",
                        description = "midia",
                        author = "tests",
                        packageName = "gifhub75",
                        fileName = "gifhub75.star",
                        recommendedIntervalMinutes = 0,
                        category = "midia",
                    },
                },
            }));

            var reloadedItems = await service.ReloadCatalogAsync();

            Assert.Contains(reloadedItems, item => string.Equals(item.Id, "gifhub75", StringComparison.OrdinalIgnoreCase));
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

    private static CatalogServiceHarness CreateService(string root)
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        return new CatalogServiceHarness(
            new AppCatalogService(CreateOptions(root), provider.GetRequiredService<HybridCache>()),
            provider);
    }

    private static async Task<string> WriteCatalogAsync(string root, string appId)
    {
        var appsDir = Path.Combine(root, "apps");
        Directory.CreateDirectory(appsDir);

        var catalogPath = Path.Combine(appsDir, "catalog.json");
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            apps = new object[]
            {
                new
                {
                    id = appId,
                    name = appId,
                    summary = "summary",
                    description = "description",
                    author = "tests",
                    packageName = appId,
                    fileName = appId + ".star",
                    recommendedIntervalMinutes = 0,
                    category = "geral",
                },
            },
        }));

        return catalogPath;
    }

    private sealed class CatalogServiceHarness : IDisposable
    {
        private readonly ServiceProvider provider;

        public CatalogServiceHarness(AppCatalogService service, ServiceProvider provider)
        {
            Service = service;
            this.provider = provider;
        }

        public AppCatalogService Service { get; }

        public void Dispose()
        {
            provider.Dispose();
        }
    }
}
