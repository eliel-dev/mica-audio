namespace Device.Protocol.Models;

public sealed class CommandDispatchResult
{
    public string DeviceId { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;

    public bool Accepted { get; init; }

    public bool Completed { get; init; }

    public bool Success { get; init; }

    public int ProgressPercent { get; init; }

    public string? Stage { get; init; }

    public string? Message { get; init; }

    public string? ErrorCode { get; init; }
}
