namespace Device.Protocol.Models;

public sealed class DeviceCommandRequest
{
    public string CommandId { get; init; } = Guid.NewGuid().ToString("N");

    public string DeviceId { get; init; } = string.Empty;

    public DeviceCommandType CommandType { get; init; }

    public IDictionary<string, string>? Parameters { get; init; }
}
