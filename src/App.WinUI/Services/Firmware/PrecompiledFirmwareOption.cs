namespace App.WinUI.Services.Firmware;

internal sealed class PrecompiledFirmwareOption
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string BoardModel { get; init; } = string.Empty;

    public string PanelType { get; init; } = string.Empty;

    public string Profile { get; init; } = string.Empty;
}
