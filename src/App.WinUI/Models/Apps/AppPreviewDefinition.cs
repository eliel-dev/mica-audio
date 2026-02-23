namespace App.WinUI.Models.Apps;

public sealed class AppPreviewDefinition
{
    public string Kind { get; init; } = "generic";

    public float Speed { get; init; } = 1f;

    public string? Accent { get; init; }

    public string? Subtitle { get; init; }
}
