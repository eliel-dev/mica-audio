namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#contrato-mqtt-de-controle
internal static class DeviceMqttTopics
{
    public static string Commands(string rootTopic, string deviceId)
        => $"{rootTopic}/{deviceId}/commands";

    public static string CommandEvents(string rootTopic, string deviceId)
        => $"{rootTopic}/{deviceId}/command-events";

    public static string Status(string rootTopic, string deviceId)
        => $"{rootTopic}/{deviceId}/status";

    public static string Presence(string rootTopic, string deviceId)
        => $"{rootTopic}/{deviceId}/presence";

    public static string Stats(string rootTopic, string deviceId)
        => $"{rootTopic}/{deviceId}/stats";

    public static string Logs(string rootTopic, string deviceId)
        => $"{rootTopic}/{deviceId}/logs";

    public static string Shadow(string rootTopic, string deviceId)
        => $"{rootTopic}/{deviceId}/shadow";

    public static bool TryParse(string rootTopic, string topic, out DeviceMqttTopicDescriptor descriptor)
    {
        descriptor = default;
        if (string.IsNullOrWhiteSpace(rootTopic) || string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        var normalizedRoot = rootTopic.Trim().Trim('/');
        var normalizedTopic = topic.Trim().Trim('/');
        var prefix = $"{normalizedRoot}/";
        if (!normalizedTopic.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = normalizedTopic[prefix.Length..];
        var parts = suffix.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return false;
        }

        var channel = parts[1] switch
        {
            "commands" => DeviceMqttChannel.Commands,
            "command-events" => DeviceMqttChannel.CommandEvents,
            "status" => DeviceMqttChannel.Status,
            "presence" => DeviceMqttChannel.Presence,
            "stats" => DeviceMqttChannel.Stats,
            "logs" => DeviceMqttChannel.Logs,
            "shadow" => DeviceMqttChannel.Shadow,
            _ => DeviceMqttChannel.Unknown,
        };

        if (channel == DeviceMqttChannel.Unknown)
        {
            return false;
        }

        descriptor = new DeviceMqttTopicDescriptor(parts[0], channel);
        return true;
    }
}

internal readonly record struct DeviceMqttTopicDescriptor(string DeviceId, DeviceMqttChannel Channel);

internal enum DeviceMqttChannel
{
    Unknown = 0,
    Commands = 1,
    CommandEvents = 2,
    Status = 3,
    Presence = 4,
    Stats = 5,
    Logs = 6,
    Shadow = 7,
}
