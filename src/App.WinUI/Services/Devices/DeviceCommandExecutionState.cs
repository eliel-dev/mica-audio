namespace App.WinUI.Services.Devices;

internal sealed class DeviceCommandExecutionState
{
    public bool InProgress { get; init; }

    public int Percent { get; init; }

    public string Status { get; init; } = "Comandos: pronto";

    public string? Stage { get; init; }

    public string? CommandId { get; init; }

    public string? ErrorCode { get; init; }
}
