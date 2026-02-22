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

    private static readonly HashSet<string> EnabledAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "accuweather",
        "analogclock",
        "gifhub75",
    };

    private static readonly IReadOnlyDictionary<string, AppCatalogItem> DefaultsById = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase)
    {
        ["accuweather"] = new AppCatalogItem
        {
            Id = "accuweather",
            Name = "Clima",
            Summary = "Previsão do tempo",
            Description = "Mostra cidade, temperatura e condição atual em formato HUB75.",
            Author = "Mica Audio",
            PackageName = "accuweather",
            FileName = "accuweather.star",
            RecommendedIntervalMinutes = 5,
            Category = "clima",
            Preview = new AppPreviewDefinition { Kind = "weather", Speed = 1f },
            Runtime = new AppRuntimeDefinition { Kind = "none" },
            Modifiers =
            [
                new AppModifierDefinition
                {
                    Key = "city",
                    Label = "Cidade",
                    Type = AppModifierFieldType.CityAutocomplete,
                    Description = "Cidade usada para consulta no Open-Meteo.",
                    Placeholder = "Ex.: São Paulo",
                    DefaultValue = "São Paulo, São Paulo, Brasil|-23.5505|-46.6333",
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
                        new AppModifierOption { Label = "Métrico (°C)", Value = "metric" },
                        new AppModifierOption { Label = "Imperial (°F)", Value = "imperial" },
                    ],
                },
            ],
        },
        ["analogclock"] = new AppCatalogItem
        {
            Id = "analogclock",
            Name = "Relógio",
            Summary = "Relógio digital HUB75",
            Description = "Exibe a hora de Brasília com watchfaces inspiradas em mostradores de smartwatch.",
            Author = "Mica Audio",
            PackageName = "analogclock",
            FileName = "analogclock.star",
            RecommendedIntervalMinutes = 0,
            Category = "relógio",
            Preview = new AppPreviewDefinition { Kind = "clock", Speed = 1f },
            Runtime = new AppRuntimeDefinition { Kind = "none" },
            Modifiers =
            [
                new AppModifierDefinition
                {
                    Key = "format24h",
                    Label = "Formato 24h",
                    Type = AppModifierFieldType.Toggle,
                    Description = "Alterna entre 24h e 12h.",
                    DefaultToggle = true,
                    Required = false,
                },
                new AppModifierDefinition
                {
                    Key = "watchfaceStyle",
                    Label = "Estilo da watchface",
                    Type = AppModifierFieldType.Select,
                    Description = "Define o estilo visual dos dígitos.",
                    DefaultValue = "pixel",
                    Required = true,
                    Options =
                    [
                        new AppModifierOption { Label = "Pixel", Value = "pixel" },
                        new AppModifierOption { Label = "Segmentado", Value = "segment" },
                        new AppModifierOption { Label = "Arredondado", Value = "rounded" },
                    ],
                },
                new AppModifierDefinition
                {
                    Key = "fontColor",
                    Label = "Cor da fonte",
                    Type = AppModifierFieldType.Select,
                    Description = "Paleta pronta para os dígitos no painel.",
                    DefaultValue = "cyan",
                    Required = true,
                    Options =
                    [
                        new AppModifierOption { Label = "Ciano", Value = "cyan" },
                        new AppModifierOption { Label = "Branco", Value = "white" },
                        new AppModifierOption { Label = "Verde", Value = "green" },
                        new AppModifierOption { Label = "Amarelo", Value = "yellow" },
                        new AppModifierOption { Label = "Laranja", Value = "orange" },
                        new AppModifierOption { Label = "Magenta", Value = "magenta" },
                    ],
                },
            ],
        },
        ["gifhub75"] = new AppCatalogItem
        {
            Id = "gifhub75",
            Name = "GIF HUB75",
            Summary = "GIF por URL ou arquivo local",
            Description = "Executa GIF no desktop com stream HUB75 64x32 para devices conectados (12 FPS fixos).",
            Author = "Mica Audio",
            PackageName = "gifhub75",
            FileName = "gifhub75.star",
            RecommendedIntervalMinutes = 0,
            Category = "midia",
            Preview = new AppPreviewDefinition { Kind = "gif", Speed = 1f },
            Runtime = new AppRuntimeDefinition { Kind = "gifhub75" },
            Modifiers =
            [
                new AppModifierDefinition
                {
                    Key = "sourceMode",
                    Label = "Fonte",
                    Type = AppModifierFieldType.Select,
                    Description = "Escolhe entre URL direta e arquivo local.",
                    DefaultValue = "url",
                    Required = true,
                    Options =
                    [
                        new AppModifierOption { Label = "URL direta", Value = "url" },
                        new AppModifierOption { Label = "Arquivo local", Value = "file" },
                    ],
                },
                new AppModifierDefinition
                {
                    Key = "gifUrl",
                    Label = "URL do GIF",
                    Type = AppModifierFieldType.Text,
                    Description = "URL direta do GIF (http/https).",
                    Placeholder = "https://exemplo.com/animacao.gif",
                    DefaultValue = string.Empty,
                    Required = false,
                },
                new AppModifierDefinition
                {
                    Key = "scaleMode",
                    Label = "Escala",
                    Type = AppModifierFieldType.Select,
                    Description = "Ajuste do frame para HUB75 64x32.",
                    DefaultValue = "fit",
                    Required = true,
                    Options =
                    [
                        new AppModifierOption { Label = "Fit", Value = "fit" },
                        new AppModifierOption { Label = "Fill", Value = "fill" },
                        new AppModifierOption { Label = "Stretch", Value = "stretch" },
                    ],
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

        var selected = response.Apps
            .Where(static item => item is not null && item.IsValid())
            .Where(item => EnabledAppIds.Contains(item.Id))
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var enabledId in EnabledAppIds)
        {
            if (!selected.ContainsKey(enabledId) && DefaultsById.TryGetValue(enabledId, out var fallback))
            {
                selected[enabledId] = fallback;
            }
        }

        return selected.Values
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

        return new AppCatalogItem
        {
            Id = defaults.Id,
            Name = defaults.Name,
            Summary = defaults.Summary,
            Description = defaults.Description,
            Author = defaults.Author,
            PackageName = string.IsNullOrWhiteSpace(item.PackageName) ? defaults.PackageName : item.PackageName,
            FileName = string.IsNullOrWhiteSpace(item.FileName) ? defaults.FileName : item.FileName,
            RecommendedIntervalMinutes = defaults.RecommendedIntervalMinutes,
            Category = defaults.Category,
            Preview = defaults.Preview,
            Runtime = item.Runtime is null || string.IsNullOrWhiteSpace(item.Runtime.Kind) ? defaults.Runtime : item.Runtime,
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

