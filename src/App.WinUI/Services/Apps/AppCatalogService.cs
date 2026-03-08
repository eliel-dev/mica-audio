using System.Text.Json;
using System.Text.Json.Serialization;
using App.WinUI.Models.Apps;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
internal sealed class AppCatalogService : IAppCatalogService
{
    private const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly MicaAudioOptions options;

    public AppCatalogService(IOptions<MicaAudioOptions> options)
    {
        this.options = options.Value;
    }

    public string CatalogPath
        => string.IsNullOrWhiteSpace(options.AppsCatalogPath)
            ? Path.Combine(options.AppDataRoot, "apps", "catalog.json")
            : options.AppsCatalogPath;

    // DOCS: docs/wiki/guides/add-app-catalog-item.md#passos
    public async Task<IReadOnlyList<AppCatalogItem>> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        var seed = await LoadSeedDocumentAsync(cancellationToken).ConfigureAwait(false);
        await EnsureCatalogSeededAsync(seed, cancellationToken).ConfigureAwait(false);

        var userDocument = await TryLoadDocumentAsync(CatalogPath, cancellationToken).ConfigureAwait(false);
        var mergedApps = MergeWithSeed(seed, userDocument);

        if (RequiresNormalization(seed, userDocument, mergedApps))
        {
            await SaveDocumentAsync(new AppCatalogDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Apps = mergedApps,
            }, cancellationToken).ConfigureAwait(false);
        }

        return mergedApps
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool RequiresNormalization(AppCatalogDocument seed, AppCatalogDocument? userDocument, IReadOnlyList<AppCatalogItem> mergedApps)
    {
        if (userDocument is null)
        {
            return true;
        }

        if (!IsSchemaSupported(userDocument.SchemaVersion))
        {
            return true;
        }

        var validUserIds = userDocument.Apps
            .Where(static app => app is not null && app.IsValid())
            .Select(app => app.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var mergedIds = mergedApps
            .Select(app => app.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!validUserIds.SetEquals(mergedIds))
        {
            return true;
        }

        // legacy/partial catalogs are normalized to include preview/modifier/runtime from seed.
        return userDocument.Apps.Any(static app => app.Preview is null || app.Modifiers.Count == 0 || app.Runtime is null);
    }

    private static AppCatalogItem[] MergeWithSeed(AppCatalogDocument seed, AppCatalogDocument? userDocument)
    {
        var seedById = seed.Apps
            .Where(static app => app is not null && app.IsValid())
            .ToDictionary(app => app.Id, StringComparer.OrdinalIgnoreCase);

        var selected = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);

        if (userDocument is not null && IsSchemaSupported(userDocument.SchemaVersion))
        {
            foreach (var item in userDocument.Apps.Where(static app => app is not null && app.IsValid()))
            {
                if (!seedById.TryGetValue(item.Id, out var seedItem))
                {
                    continue;
                }

                selected[item.Id] = MergeApp(seedItem, item);
            }
        }

        foreach (var seedItem in seedById.Values)
        {
            if (!selected.ContainsKey(seedItem.Id))
            {
                selected[seedItem.Id] = seedItem;
            }
        }

        return selected.Values.ToArray();
    }

    private static AppCatalogItem MergeApp(AppCatalogItem seedItem, AppCatalogItem userItem)
    {
        return new AppCatalogItem
        {
            Id = seedItem.Id,
            Name = seedItem.Name,
            Summary = seedItem.Summary,
            Description = seedItem.Description,
            Author = seedItem.Author,
            PackageName = string.IsNullOrWhiteSpace(userItem.PackageName) ? seedItem.PackageName : userItem.PackageName,
            FileName = string.IsNullOrWhiteSpace(userItem.FileName) ? seedItem.FileName : userItem.FileName,
            RecommendedIntervalMinutes = seedItem.RecommendedIntervalMinutes,
            Category = seedItem.Category,
            Preview = seedItem.Preview,
            Runtime = userItem.Runtime is null || string.IsNullOrWhiteSpace(userItem.Runtime.Kind)
                ? seedItem.Runtime
                : userItem.Runtime,
            Modifiers = seedItem.Modifiers,
        };
    }

    private static bool IsSchemaSupported(int schemaVersion) => schemaVersion is >= 1 and <= CurrentSchemaVersion;

    private async Task EnsureCatalogSeededAsync(AppCatalogDocument seed, CancellationToken cancellationToken)
    {
        if (File.Exists(CatalogPath))
        {
            return;
        }

        await SaveDocumentAsync(seed, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveDocumentAsync(AppCatalogDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
        await using var target = File.Create(CatalogPath);
        await JsonSerializer.SerializeAsync(target, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AppCatalogDocument?> TryLoadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<AppCatalogDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task<AppCatalogDocument> LoadSeedDocumentAsync(CancellationToken cancellationToken)
    {
        var seedPath = ResolveSeedPath();
        if (seedPath is null)
        {
            throw new FileNotFoundException("Nao foi possivel localizar o catalogo seed de apps.");
        }

        await using var stream = File.OpenRead(seedPath);
        var seed = await JsonSerializer.DeserializeAsync<AppCatalogDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new AppCatalogDocument();

        if (!IsSchemaSupported(seed.SchemaVersion))
        {
            throw new InvalidDataException($"SchemaVersion de seed nao suportada: {seed.SchemaVersion}.");
        }

        if (seed.Apps.Length == 0)
        {
            throw new InvalidDataException("Catalogo seed nao possui apps validos.");
        }

        var validApps = seed.Apps
            .Where(static app => app is not null && app.IsValid())
            .ToArray();

        if (validApps.Length == 0)
        {
            throw new InvalidDataException("Catalogo seed nao possui apps validos apos filtragem.");
        }

        return new AppCatalogDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Apps = validApps,
        };
    }

    private static string? ResolveSeedPath()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "AppData", "apps-catalog.seed.json"),
            Path.Combine(Environment.CurrentDirectory, "src", "App.WinUI", "AppData", "apps-catalog.seed.json"),
            Path.Combine(Environment.CurrentDirectory, "AppData", "apps-catalog.seed.json"),
        };

        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 10 && probe is not null; depth++)
        {
            candidates.Add(Path.Combine(probe.FullName, "src", "App.WinUI", "AppData", "apps-catalog.seed.json"));
            candidates.Add(Path.Combine(probe.FullName, "AppData", "apps-catalog.seed.json"));
            probe = probe.Parent;
        }

        return candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private sealed class AppCatalogDocument
    {
        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        public AppCatalogItem[] Apps { get; init; } = [];
    }
}
