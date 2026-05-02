using System.Text.Json;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace MicaAudio.Server;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/handoffs/2026-04-30-remote-only-server-panel-runtime.md
public sealed class StandalonePanelRuntimeStateStore : IPanelRuntimeStateStore, IDisposable
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

    public StandalonePanelRuntimeStateStore(string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        filePath = Path.Combine(storageRoot, "panels", "runtime-state.json");
        tempPath = filePath + ".tmp";
        backupPath = filePath + ".bak";
    }

    public async Task<PanelRuntimeStateDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return new PanelRuntimeStateDocument();
            }

            try
            {
                await using var stream = File.OpenRead(filePath);
                var loaded = await JsonSerializer.DeserializeAsync<PanelRuntimeStateDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? new PanelRuntimeStateDocument();
                return Normalize(loaded);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return new PanelRuntimeStateDocument();
            }
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task SaveAsync(PanelRuntimeStateDocument document, CancellationToken cancellationToken = default)
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

            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }
        finally
        {
            TryDeleteTempFile();
            ioGate.Release();
        }
    }

    private static PanelRuntimeStateDocument Normalize(PanelRuntimeStateDocument source)
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

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose()
    {
        ioGate.Dispose();
    }
}
