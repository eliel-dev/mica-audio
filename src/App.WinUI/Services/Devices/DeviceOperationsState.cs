using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal sealed class DeviceOperationsState
{
    public bool CommandInProgress { get; init; }

    public int CommandPercent { get; init; }

    public string CommandStatus { get; init; } = "Comandos: pronto";

    public string? LastCommandDeviceId { get; init; }

    public IReadOnlyDictionary<string, DeviceCommandExecutionState> CommandByDevice { get; init; }
        = new Dictionary<string, DeviceCommandExecutionState>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<DeviceSnapshot> DeviceListSnapshot { get; init; } = Array.Empty<DeviceSnapshot>();

    public DateTimeOffset LastRefreshUtc { get; init; }

    public string ServerBaseAddress { get; init; } = "http://127.0.0.1:5272";

    public IReadOnlyList<DeviceLogEntry> Logs { get; init; } = Array.Empty<DeviceLogEntry>();
}
