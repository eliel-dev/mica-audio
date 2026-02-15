namespace App.WinUI.Models.Apps;

public sealed class DeviceAppAssignment
{
    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string AppId { get; init; } = string.Empty;

    public string AppName { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
