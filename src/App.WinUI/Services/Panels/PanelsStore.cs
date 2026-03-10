using System.Text.Json;
using App.WinUI.Models.Panels;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#persistencia-do-layout
internal sealed class PanelsStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim ioGate = new(1, 1);
    private readonly string path;
    private PanelsStoreDocument document = new();
    private bool loaded;

    public PanelsStore(IOptions<MicaAudioOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        path = string.IsNullOrWhiteSpace(options.Value.PanelsFilePath)
            ? Path.Combine(options.Value.AppDataRoot, "panels", "panels.json")
            : options.Value.PanelsFilePath;
    }

    public async Task<PanelsStoreDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!loaded)
            {
                document = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
                loaded = true;
            }

            return document.Clone();
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task SaveAsync(PanelsStoreDocument nextDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nextDocument);

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var normalized = nextDocument.Clone();
            normalized.Normalize();

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken).ConfigureAwait(false);

            document = normalized;
            loaded = true;
        }
        finally
        {
            ioGate.Release();
        }
    }

    public void Dispose()
    {
        ioGate.Dispose();
    }

    private async Task<PanelsStoreDocument> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new PanelsStoreDocument();
        }

        await using var stream = File.OpenRead(path);
        var loadedDocument = await JsonSerializer.DeserializeAsync<PanelsStoreDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new PanelsStoreDocument();
        loadedDocument.Normalize();
        return loadedDocument;
    }
}
