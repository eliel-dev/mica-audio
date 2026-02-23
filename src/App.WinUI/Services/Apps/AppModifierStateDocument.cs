namespace App.WinUI.Services.Apps;

internal sealed class AppModifierStateDocument
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<AppModifierStateEntry> Entries { get; init; } = Array.Empty<AppModifierStateEntry>();
}

internal sealed class AppModifierStateEntry
{
    public string DeviceId { get; init; } = string.Empty;

    public string AppId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
