namespace App.WinUI.Infrastructure.Serial;

internal sealed record SerialPortDescriptor
{
    public required string PortName { get; init; }

    public required string DisplayName { get; init; }

    public string? PnpDeviceId { get; init; }

    public string? VidPid { get; init; }

    public bool IsPreferredDevice { get; init; }

    public int PriorityRank { get; init; }

    public string Label => string.IsNullOrWhiteSpace(DisplayName)
        ? PortName
        : $"{PortName} - {DisplayName}";
}
