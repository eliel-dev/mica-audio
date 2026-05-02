using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
internal sealed class InMemoryPanelLibraryStore : IPanelLibraryStore
{
    private readonly object gate = new();
    private PanelLibraryDocument document = new();

    public Task<PanelLibraryDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(Clone(document));
        }
    }

    public Task SaveAsync(PanelLibraryDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            this.document = Clone(document);
        }

        return Task.CompletedTask;
    }

    private static PanelLibraryDocument Clone(PanelLibraryDocument source)
    {
        return new PanelLibraryDocument
        {
            SchemaVersion = source.SchemaVersion,
            LastSelectedPanelId = NormalizeOptional(source.LastSelectedPanelId),
            Panels = source.Panels
                .Where(panel => !string.IsNullOrWhiteSpace(panel.PanelId))
                .Select(panel => new PanelLibraryItem
                {
                    PanelId = panel.PanelId.Trim(),
                    Name = string.IsNullOrWhiteSpace(panel.Name) ? "Painel" : panel.Name.Trim(),
                    Width = panel.Width <= 0 ? 128 : panel.Width,
                    Height = panel.Height <= 0 ? 64 : panel.Height,
                    IsEnabled = panel.IsEnabled,
                    Widgets = panel.Widgets
                        .Where(widget => !string.IsNullOrWhiteSpace(widget.WidgetId))
                        .Select(widget => new PanelWidgetItem
                        {
                            WidgetId = widget.WidgetId.Trim(),
                            AppId = NormalizeOptional(widget.AppId) ?? string.Empty,
                            X = widget.X,
                            Y = widget.Y,
                            Width = widget.Width,
                            Height = widget.Height,
                            ZIndex = widget.ZIndex,
                            ConfigValues = new Dictionary<string, string>(widget.ConfigValues, StringComparer.OrdinalIgnoreCase),
                            RuntimeState = new Dictionary<string, string>(widget.RuntimeState, StringComparer.OrdinalIgnoreCase),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
