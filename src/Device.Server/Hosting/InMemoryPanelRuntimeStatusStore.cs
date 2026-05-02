using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/device-server-protocol.md#runtime-remoto-de-paineis
public sealed class InMemoryPanelRuntimeStatusStore : IPanelRuntimeStatusStore
{
    private readonly object gate = new();
    private PanelRuntimeStatusDocument document = new();

    public Task<PanelRuntimeStatusDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(Clone(document));
        }
    }

    public Task SaveAsync(PanelRuntimeStatusDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            this.document = Clone(document);
        }

        return Task.CompletedTask;
    }

    private static PanelRuntimeStatusDocument Clone(PanelRuntimeStatusDocument source)
    {
        return new PanelRuntimeStatusDocument
        {
            SchemaVersion = PanelRuntimeStatusDocument.CurrentSchemaVersion,
            Running = source.Running,
            PanelId = NormalizeOptional(source.PanelId),
            TargetDeviceId = NormalizeOptional(source.TargetDeviceId),
            SkippedWidgets = source.SkippedWidgets
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            LastError = NormalizeOptional(source.LastError),
            LastRenderedAtUtc = source.LastRenderedAtUtc,
        };
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
