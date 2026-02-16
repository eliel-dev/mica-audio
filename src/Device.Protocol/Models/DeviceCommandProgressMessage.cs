namespace Device.Protocol.Models;

// DOCS: docs/wiki/reference/ws-protocol-v1.md#tipos-de-mensagem
public sealed class DeviceCommandProgressMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;

    // DOCS: docs/wiki/architecture/06-errors-timeouts-and-recovery.md#politica-de-timeout
    public int ProgressPercent { get; init; }

    public string? Stage { get; init; }

    public string? Message { get; init; }

    public bool? Success { get; init; }
}
