using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal sealed class DeviceOperationsState
{
    public bool BuildInProgress { get; init; }

    public int BuildPercent { get; init; }

    public string BuildStatus { get; init; } = "Build: pronto";

    public IReadOnlyList<string> BuildLogs { get; init; } = Array.Empty<string>();

    public bool CommandInProgress { get; init; }

    public int CommandPercent { get; init; }

    public string CommandStatus { get; init; } = "Comandos: pronto";

    public string? LastCommandDeviceId { get; init; }

    public IReadOnlyList<DeviceSnapshot> DeviceListSnapshot { get; init; } = Array.Empty<DeviceSnapshot>();

    public DateTimeOffset LastRefreshUtc { get; init; }

    public string? LastExportDirectory { get; init; }

    public string ServerBaseAddress { get; init; } = "http://127.0.0.1:5272";

    public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();
}
