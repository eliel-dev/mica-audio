namespace App.WinUI.Models.Apps;

public sealed class AppModifierDefinition
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public AppModifierFieldType Type { get; init; } = AppModifierFieldType.Text;

    public string? Description { get; init; }

    public bool Required { get; init; }

    public string? Placeholder { get; init; }

    public string? DefaultValue { get; init; }

    public bool? DefaultToggle { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public double? Step { get; init; }

    public IReadOnlyList<AppModifierOption> Options { get; init; } = Array.Empty<AppModifierOption>();

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Label))
        {
            return false;
        }

        if (Type == AppModifierFieldType.Select && (Options is null || Options.Count == 0))
        {
            return false;
        }

        return true;
    }
}
