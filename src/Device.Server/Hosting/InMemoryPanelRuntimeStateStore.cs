using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/device-server-protocol.md#runtime-remoto-de-paineis
public sealed class InMemoryPanelRuntimeStateStore : IPanelRuntimeStateStore
{
    private readonly object gate = new();
    private PanelRuntimeStateDocument document = new();

    public Task<PanelRuntimeStateDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(Clone(document));
        }
    }

    public Task SaveAsync(PanelRuntimeStateDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            this.document = Clone(document);
        }

        return Task.CompletedTask;
    }

    private static PanelRuntimeStateDocument Clone(PanelRuntimeStateDocument source)
    {
        return new PanelRuntimeStateDocument
        {
            SchemaVersion = PanelRuntimeStateDocument.CurrentSchemaVersion,
            Enabled = source.Enabled,
            PanelId = NormalizeOptional(source.PanelId),
            TargetDeviceId = NormalizeOptional(source.TargetDeviceId),
            UpdatedAtUtc = source.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : source.UpdatedAtUtc,
        };
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
