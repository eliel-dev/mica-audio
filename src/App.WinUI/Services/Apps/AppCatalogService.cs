using System.Text.Json;
using System.Text.Json.Serialization;
using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
internal sealed class AppCatalogService
{
    private const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string appDataRoot;

    public AppCatalogService(string appDataRoot)
    {
        this.appDataRoot = appDataRoot;
    }

    public string CatalogPath => Path.Combine(appDataRoot, "apps", "catalog.json");

    // DOCS: docs/wiki/guides/add-app-catalog-item.md#passos
    public async Task<IReadOnlyList<AppCatalogItem>> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCatalogSeededAsync(cancellationToken).ConfigureAwait(false);

        await using var stream = File.OpenRead(CatalogPath);
        var response = await JsonSerializer.DeserializeAsync<AppCatalogDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Catálogo de apps inválido: conteúdo vazio.");

        ValidateSchema(response);

        return response.Apps
            .Where(static item => item is not null && item.IsValid())
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ValidateSchema(AppCatalogDocument document)
    {
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Catálogo de apps com schemaVersion '{document.SchemaVersion}' não suportado. Esperado: {CurrentSchemaVersion}.");
        }
    }

    private async Task EnsureCatalogSeededAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(CatalogPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);

        var seedPath = ResolveSeedPath();
        if (seedPath is null)
        {
            var empty = new AppCatalogDocument { SchemaVersion = CurrentSchemaVersion, Apps = Array.Empty<AppCatalogItem>() };
            await using var create = File.Create(CatalogPath);
            await JsonSerializer.SerializeAsync(create, empty, JsonOptions, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var source = File.OpenRead(seedPath);
        await using var target = File.Create(CatalogPath);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    private static string? ResolveSeedPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "AppData", "apps-catalog.seed.json"),
            Path.Combine(Environment.CurrentDirectory, "src", "App.WinUI", "AppData", "apps-catalog.seed.json"),
            Path.Combine(Environment.CurrentDirectory, "AppData", "apps-catalog.seed.json"),
        }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return candidates.FirstOrDefault(File.Exists);
    }

    private sealed class AppCatalogDocument
    {
        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        public IReadOnlyList<AppCatalogItem> Apps { get; init; } = Array.Empty<AppCatalogItem>();
    }
}
