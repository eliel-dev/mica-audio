namespace App.WinUI.Models.Apps;

public sealed class AppConfigDraft
{
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
