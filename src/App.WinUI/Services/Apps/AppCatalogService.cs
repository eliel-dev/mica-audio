using System.Text.Json;
using System.Text.Json.Serialization;
using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
internal sealed class AppCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Catálogo simplificado: apenas 2 apps habilitados na UI.
    private static readonly HashSet<string> EnabledAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "accuweather",
        "analogclock",
    };

    // Defaults para cobrir catálogos antigos que não tragam preview/modifiers.
    private static readonly IReadOnlyDictionary<string, AppCatalogItem> DefaultsById = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase)
    {
        ["accuweather"] = new AppCatalogItem
        {
            Id = "accuweather",
            Name = "Clima",
            Summary = "Previsão do tempo",
            Description = "Mostra condições e previsão do tempo para a cidade selecionada.",
            Author = "mica audio",
            PackageName = "accuweather",
            FileName = "accuweather.star",
            RecommendedIntervalMinutes = 5,
            Category = "clima",
            Preview = new AppPreviewDefinition { Kind = "weather", Speed = 1f },
            Modifiers =
            [
                new AppModifierDefinition
                {
                    Key = "city",
                    Label = "Cidade",
                    Type = AppModifierFieldType.CityAutocomplete,
                    Description = "Cidade base para consulta do clima.",
                    Placeholder = "Ex: São Paulo",
                    DefaultValue = string.Empty,
                    Required = true,
                },
                new AppModifierDefinition
                {
                    Key = "units",
                    Label = "Unidades",
                    Type = AppModifierFieldType.Select,
                    Description = "Sistema de unidades de temperatura.",
                    DefaultValue = "metric",
                    Required = true,
                    Options =
                    [
                        new AppModifierOption { Label = "Métrico (C)", Value = "metric" },
                        new AppModifierOption { Label = "Imperial (F)", Value = "imperial" },
                    ],
                },
                new AppModifierDefinition
                {
                    Key = "lang",
                    Label = "Idioma",
                    Type = AppModifierFieldType.Select,
                    Description = "Idioma dos textos mostrados.",
                    DefaultValue = "pt",
                    Required = true,
                    Options =
                    [
                        new AppModifierOption { Label = "Português", Value = "pt" },
                        new AppModifierOption { Label = "English", Value = "en" },
                        new AppModifierOption { Label = "Espanhol", Value = "es" },
                    ],
                },
            ],
        },
        ["analogclock"] = new AppCatalogItem
        {
            Id = "analogclock",
            Name = "Relógio",
            Summary = "Relógio digital",
            Description = "Exibe a hora atual em estilo de painel HUB75.",
            Author = "mica audio",
            PackageName = "analogclock",
            FileName = "analogclock.star",
            RecommendedIntervalMinutes = 0,
            Category = "relógio",
            Preview = new AppPreviewDefinition { Kind = "clock", Speed = 1f },
            Modifiers =
            [
                new AppModifierDefinition
                {
                    Key = "timezone",
                    Label = "Fuso horário",
                    Type = AppModifierFieldType.Text,
                    Description = "Identificador IANA do fuso (ex: America/Sao_Paulo).",
                    Placeholder = "America/Sao_Paulo",
                    DefaultValue = "America/Sao_Paulo",
                    Required = true,
                },
                new AppModifierDefinition
                {
                    Key = "format24h",
                    Label = "Formato 24h",
                    Type = AppModifierFieldType.Toggle,
                    Description = "Alterna entre formato 24h e 12h.",
                    Required = false,
                    DefaultToggle = true,
                },
            ],
        },
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
            ?? new AppCatalogDocument();

        return response.Apps
            .Where(static item => item is not null && item.IsValid())
            .Where(item => EnabledAppIds.Contains(item.Id))
            .Select(EnrichSupportedItem)
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AppCatalogItem EnrichSupportedItem(AppCatalogItem item)
    {
        if (!DefaultsById.TryGetValue(item.Id, out var defaults))
        {
            return item;
        }

        // Para os apps suportados na UI, usamos a definição canônica local
        // para manter preview/modificadores consistentes e em PT-BR.
        return new AppCatalogItem
        {
            Id = defaults.Id,
            Name = defaults.Name,
            Summary = defaults.Summary,
            Description = defaults.Description,
            Author = string.IsNullOrWhiteSpace(item.Author) ? defaults.Author : item.Author,
            PackageName = string.IsNullOrWhiteSpace(item.PackageName) ? defaults.PackageName : item.PackageName,
            FileName = string.IsNullOrWhiteSpace(item.FileName) ? defaults.FileName : item.FileName,
            RecommendedIntervalMinutes = defaults.RecommendedIntervalMinutes,
            Category = defaults.Category,
            Preview = defaults.Preview,
            Modifiers = defaults.Modifiers,
        };
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
            var empty = new AppCatalogDocument { SchemaVersion = 2, Apps = Array.Empty<AppCatalogItem>() };
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
        public int SchemaVersion { get; init; } = 2;

        public IReadOnlyList<AppCatalogItem> Apps { get; init; } = Array.Empty<AppCatalogItem>();
    }
}
