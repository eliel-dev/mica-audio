using System.Text.Json;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace MicaAudio.Server;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
public sealed class StandalonePanelLibraryStore : IPanelLibraryStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim ioGate = new(1, 1);
    private readonly string filePath;
    private readonly string tempPath;
    private readonly string backupPath;

    public StandalonePanelLibraryStore(string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        filePath = Path.Combine(storageRoot, "panels", "panels.json");
        tempPath = filePath + ".tmp";
        backupPath = filePath + ".bak";
    }

    public async Task<PanelLibraryDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return new PanelLibraryDocument();
            }

            try
            {
                await using var stream = File.OpenRead(filePath);
                var loaded = await JsonSerializer.DeserializeAsync<PanelLibraryDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? new PanelLibraryDocument();
                return Normalize(loaded);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return new PanelLibraryDocument();
            }
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task SaveAsync(PanelLibraryDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            TryDeleteTempFile();

            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, Normalize(document), JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            ReplaceTempFile();
        }
        finally
        {
            TryDeleteTempFile();
            ioGate.Release();
        }
    }

    private void ReplaceTempFile()
    {
        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, backupPath, ignoreMetadataErrors: true);
            return;
        }

        File.Move(tempPath, filePath);
    }

    private static PanelLibraryDocument Normalize(PanelLibraryDocument source)
    {
        return new PanelLibraryDocument
        {
            SchemaVersion = source.SchemaVersion,
            LastSelectedPanelId = NormalizeOptional(source.LastSelectedPanelId),
            ActivePanels = (source.ActivePanels ?? Array.Empty<PanelDeviceState>())
                .Where(state => !string.IsNullOrWhiteSpace(state.DeviceId))
                .GroupBy(state => state.DeviceId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var state = group.Last();
                    return new PanelDeviceState
                    {
                        DeviceId = state.DeviceId.Trim(),
                        ActivePanelId = NormalizeOptional(state.ActivePanelId),
                        ActiveAppId = NormalizeOptional(state.ActiveAppId),
                        LastServerOwnedPanelId = NormalizeOptional(state.LastServerOwnedPanelId),
                        UpdatedAtUtc = state.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : state.UpdatedAtUtc,
                    };
                })
                .ToArray(),
            Panels = (source.Panels ?? Array.Empty<PanelLibraryItem>())
                .Where(panel => !string.IsNullOrWhiteSpace(panel.PanelId))
                .Select(panel => new PanelLibraryItem
                {
                    PanelId = panel.PanelId.Trim(),
                    Name = string.IsNullOrWhiteSpace(panel.Name) ? "Painel" : panel.Name.Trim(),
                    Width = panel.Width <= 0 ? 128 : panel.Width,
                    Height = panel.Height <= 0 ? 64 : panel.Height,
                    IsEnabled = panel.IsEnabled,
                    Widgets = (panel.Widgets ?? Array.Empty<PanelWidgetItem>())
                        .Where(widget => !string.IsNullOrWhiteSpace(widget.WidgetId))
                        .Select(widget => new PanelWidgetItem
                        {
                            WidgetId = widget.WidgetId.Trim(),
                            AppId = NormalizeOptional(widget.AppId) ?? string.Empty,
                            DataSource = PanelWidgetDataSources.Normalize(widget.DataSource),
                            X = widget.X,
                            Y = widget.Y,
                            Width = widget.Width,
                            Height = widget.Height,
                            ZIndex = widget.ZIndex,
                            ConfigValues = new Dictionary<string, string>(widget.ConfigValues, StringComparer.OrdinalIgnoreCase),
                            RuntimeState = widget.RuntimeState
                                .Where(static entry => PanelWidgetRuntimeStateKeys.IsServerSafe(entry.Key))
                                .ToDictionary(static entry => entry.Key.Trim(), static entry => entry.Value, StringComparer.OrdinalIgnoreCase),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void TryDeleteTempFile()
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        ioGate.Dispose();
    }
}
