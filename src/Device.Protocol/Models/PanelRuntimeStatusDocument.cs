namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/device-server-protocol.md#runtime-remoto-de-paineis
public sealed class PanelRuntimeStatusDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public bool Running { get; init; }

    public string? PanelId { get; init; }

    public string? TargetDeviceId { get; init; }

    public IReadOnlyList<string> SkippedWidgets { get; init; } = Array.Empty<string>();

    public string? LastError { get; init; }

    public DateTimeOffset? LastRenderedAtUtc { get; init; }
}
