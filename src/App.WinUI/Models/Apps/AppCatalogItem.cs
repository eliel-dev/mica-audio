namespace App.WinUI.Models.Apps;

public sealed class AppCatalogItem
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public string PackageName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public int RecommendedIntervalMinutes { get; init; }

    public string Category { get; init; } = "geral";

    public AppPreviewDefinition? Preview { get; init; }

    public AppRuntimeDefinition? Runtime { get; init; }

    public IReadOnlyList<AppModifierDefinition> Modifiers { get; init; } = Array.Empty<AppModifierDefinition>();

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Id)
            && !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(PackageName);
    }
}
