using System.Text.Json;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace MicaAudio.Server;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
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
                return await JsonSerializer.DeserializeAsync<PanelLibraryDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? new PanelLibraryDocument();
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
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
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
