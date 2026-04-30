namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-server-owned
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
public sealed class PanelRuntimeDiagnosticsResponse
{
    public IReadOnlyList<PanelRuntimeDeviceDiagnostic> Devices { get; init; } =
        Array.Empty<PanelRuntimeDeviceDiagnostic>();
}

public sealed class PanelRuntimeDeviceDiagnostic
{
    public string DeviceId { get; init; } = string.Empty;

    public string? PanelId { get; init; }

    public string State { get; init; } = "idle";

    public ulong LastBatchSequence { get; init; }

    public string? LastError { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
