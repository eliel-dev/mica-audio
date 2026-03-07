namespace App.WinUI.Services.Devices;

internal sealed class DeviceCommandExecutionContext
{
    public string DeviceId { get; init; } = string.Empty;

    public int Percent { get; set; }

    public string Status { get; set; } = "Comandos: pronto";

    public string? Stage { get; set; }

    public string? CommandId { get; set; }

    public string? ErrorCode { get; set; }

    public DeviceCommandExecutionState ToSnapshot()
    {
        return new DeviceCommandExecutionState
        {
            InProgress = true,
            Percent = Math.Clamp(Percent, 0, 100),
            Status = Status,
            Stage = Stage,
            CommandId = CommandId,
            ErrorCode = ErrorCode,
        };
    }
}
