using System.Globalization;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/reference/device-observability-dashboard.md#aba-logs
internal enum DeviceLogSource
{
    App,
    Device,
    Server,
}

internal enum DeviceLogSeverity
{
    Info,
    Warning,
    Error,
}

internal sealed class DeviceLogEntry
{
    public DeviceLogEntry(
        DateTimeOffset timestamp,
        string? deviceId,
        DeviceLogSource source,
        DeviceLogSeverity severity,
        string category,
        string? code,
        string message)
    {
        Timestamp = timestamp;
        DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        Source = source;
        Severity = severity;
        Category = string.IsNullOrWhiteSpace(category) ? "device" : category.Trim();
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? "-" : message.Trim();
    }

    public DateTimeOffset Timestamp { get; }

    public string? DeviceId { get; }

    public DeviceLogSource Source { get; }

    public DeviceLogSeverity Severity { get; }

    public string Category { get; }

    public string? Code { get; }

    public string Message { get; }

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

internal static class DeviceLogEntryFormatter
{
    public static string Format(DeviceLogEntry entry, bool includeDeviceId = false)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var metadata = new List<string>(capacity: 5)
        {
            $"[{entry.FormattedTimestamp}]",
            $"[{FormatSource(entry.Source)}]",
            $"[{FormatSeverity(entry.Severity)}]",
            $"[{entry.Category}]",
        };

        if (!string.IsNullOrWhiteSpace(entry.Code))
        {
            metadata.Add($"[{entry.Code}]");
        }

        if (includeDeviceId && !string.IsNullOrWhiteSpace(entry.DeviceId))
        {
            metadata.Add($"[{entry.DeviceId}]");
        }

        return string.Concat(string.Join(" ", metadata), " ", entry.Message);
    }

    private static string FormatSource(DeviceLogSource source)
        => source switch
        {
            DeviceLogSource.App => "app",
            DeviceLogSource.Device => "device",
            DeviceLogSource.Server => "server",
            _ => "app",
        };

    private static string FormatSeverity(DeviceLogSeverity severity)
        => severity switch
        {
            DeviceLogSeverity.Warning => "warn",
            DeviceLogSeverity.Error => "error",
            _ => "info",
        };
}
