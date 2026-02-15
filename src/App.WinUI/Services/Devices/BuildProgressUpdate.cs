namespace App.WinUI.Services.Devices;

internal sealed class BuildProgressUpdate
{
    public int Percent { get; init; }

    public string Stage { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool IsTerminal { get; init; }
}
