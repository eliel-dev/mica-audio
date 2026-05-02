namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/device-server-protocol.md#runtime-remoto-de-paineis
public sealed class PanelRuntimeStateDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public bool Enabled { get; init; }

    public string? PanelId { get; init; }

    public string? TargetDeviceId { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
