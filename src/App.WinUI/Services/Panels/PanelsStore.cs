using System.Globalization;
using System.Text.Json;
using App.WinUI.Models.Panels;
using Device.Client;
using Device.Protocol.Models;
using MicaAudio.Core.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#persistencia-do-layout
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
internal sealed class PanelsStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
    private static readonly Action<ILogger, string, Exception?> LogEmptyStoreDocument =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1001, nameof(LogEmptyStoreDocument)), "Panels store is empty. Returning a new document. Path: {PanelsPath}");
    private static readonly Action<ILogger, string, string, Exception?> LogWhitespaceStoreDocument =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(1002, nameof(LogWhitespaceStoreDocument)), "Panels store contains only whitespace. Returning a new document. Path: {PanelsPath}. QuarantinedPath: {QuarantinedPath}");
    private static readonly Action<ILogger, string, string, Exception?> LogInvalidJsonStoreDocument =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(1003, nameof(LogInvalidJsonStoreDocument)), "Panels store JSON is invalid. Returning a new document. Path: {PanelsPath}. QuarantinedPath: {QuarantinedPath}");
    private static readonly Action<ILogger, string, Exception?> LogUnreadableStoreDocument =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1004, nameof(LogUnreadableStoreDocument)), "Panels store could not be read. Returning a new document. Path: {PanelsPath}");
    private static readonly Action<ILogger, string, Exception?> LogUnauthorizedStoreDocument =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1005, nameof(LogUnauthorizedStoreDocument)), "Panels store could not be accessed. Returning a new document. Path: {PanelsPath}");
    private static readonly Action<ILogger, string, Exception?> LogFailedQuarantineStoreDocument =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1006, nameof(LogFailedQuarantineStoreDocument)), "Failed to quarantine corrupt panels store. Path: {PanelsPath}");
    private static readonly Action<ILogger, string, Exception?> LogFailedQuarantineDueToAccessRestrictions =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1007, nameof(LogFailedQuarantineDueToAccessRestrictions)), "Failed to quarantine corrupt panels store due to access restrictions. Path: {PanelsPath}");
    private static readonly Action<ILogger, string, Exception?> LogFailedDeleteTempFile =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1008, nameof(LogFailedDeleteTempFile)), "Failed to delete temporary panels store file. Path: {TempPath}");
    private static readonly Action<ILogger, string, Exception?> LogFailedDeleteTempFileDueToAccessRestrictions =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1009, nameof(LogFailedDeleteTempFileDueToAccessRestrictions)), "Failed to delete temporary panels store file due to access restrictions. Path: {TempPath}");

    private readonly SemaphoreSlim ioGate = new(1, 1);
    private readonly IDeviceServerClient? deviceServerClient;
    private readonly ILogger<PanelsStore> logger;
    private readonly string path;
    private readonly string tempPath;
    private readonly string backupPath;
    private PanelsStoreDocument document = new();
    private bool loaded;

    public PanelsStore(
        IOptions<MicaAudioOptions> options,
        ILogger<PanelsStore> logger,
        IDeviceServerClient? deviceServerClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.deviceServerClient = deviceServerClient;
        this.logger = logger;
        path = string.IsNullOrWhiteSpace(options.Value.PanelsFilePath)
            ? Path.Combine(options.Value.AppDataRoot, "panels", "panels.json")
            : options.Value.PanelsFilePath;
        tempPath = path + ".tmp";
        backupPath = path + ".bak";
    }

    public async Task<PanelsStoreDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!loaded)
            {
                var localDocument = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
                document = await ResolveServerFirstDocumentAsync(localDocument, cancellationToken).ConfigureAwait(false);
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
            TryDeleteTempFile();

            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }

            document = normalized;
            loaded = true;

            await TrySaveServerLibraryAsync(normalized, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteTempFile();
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
            return CreateEmptyDocument();
        }

        try
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length == 0)
            {
                LogEmptyStoreDocument(logger, path, null);
                return CreateEmptyDocument();
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                var quarantinedPath = TryQuarantineCorruptFile();
                LogWhitespaceStoreDocument(logger, path, quarantinedPath ?? string.Empty, null);
                return CreateEmptyDocument();
            }

            var loadedDocument = JsonSerializer.Deserialize<PanelsStoreDocument>(json, JsonOptions)
                ?? new PanelsStoreDocument();
            loadedDocument.Normalize();
            return loadedDocument;
        }
        catch (JsonException ex)
        {
            var quarantinedPath = TryQuarantineCorruptFile();
            LogInvalidJsonStoreDocument(logger, path, quarantinedPath ?? string.Empty, ex);
            return CreateEmptyDocument();
        }
        catch (IOException ex)
        {
            LogUnreadableStoreDocument(logger, path, ex);
            return CreateEmptyDocument();
        }
        catch (UnauthorizedAccessException ex)
        {
            LogUnauthorizedStoreDocument(logger, path, ex);
            return CreateEmptyDocument();
        }
    }

    private static PanelsStoreDocument CreateEmptyDocument()
    {
        var emptyDocument = new PanelsStoreDocument();
        emptyDocument.Normalize();
        return emptyDocument;
    }

    private async Task<PanelsStoreDocument> ResolveServerFirstDocumentAsync(
        PanelsStoreDocument localDocument,
        CancellationToken cancellationToken)
    {
        if (deviceServerClient is null)
        {
            return localDocument;
        }

        try
        {
            var serverLibrary = await deviceServerClient.GetPanelLibraryAsync(cancellationToken).ConfigureAwait(false);
            var serverDocument = FromPanelLibrary(serverLibrary);
            if (serverDocument.Panels.Count > 0)
            {
                return serverDocument;
            }

            if (localDocument.Panels.Count > 0)
            {
                await deviceServerClient.SavePanelLibraryAsync(ToPanelLibrary(localDocument), cancellationToken).ConfigureAwait(false);
            }

            return localDocument;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return localDocument;
        }
    }

    private async Task TrySaveServerLibraryAsync(PanelsStoreDocument localDocument, CancellationToken cancellationToken)
    {
        if (deviceServerClient is null)
        {
            return;
        }

        try
        {
            await deviceServerClient.SavePanelLibraryAsync(ToPanelLibrary(localDocument), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
        }
    }

    private static PanelLibraryDocument ToPanelLibrary(PanelsStoreDocument source)
    {
        return new PanelLibraryDocument
        {
            SchemaVersion = source.SchemaVersion,
            LastSelectedPanelId = source.LastSelectedPanelId,
            Panels = source.Panels.Select(static panel => new PanelLibraryItem
            {
                PanelId = panel.PanelId,
                Name = panel.Name,
                Width = panel.Width,
                Height = panel.Height,
                IsEnabled = true,
                Widgets = panel.Widgets.Select(static widget => new PanelWidgetItem
                {
                    WidgetId = widget.WidgetId,
                    AppId = widget.AppId,
                    X = widget.X,
                    Y = widget.Y,
                    Width = widget.Width,
                    Height = widget.Height,
                    ZIndex = widget.ZIndex,
                    ConfigValues = new Dictionary<string, string>(widget.ConfigValues, StringComparer.OrdinalIgnoreCase),
                }).ToArray(),
            }).ToArray(),
        };
    }

    private static PanelsStoreDocument FromPanelLibrary(PanelLibraryDocument source)
    {
        var document = new PanelsStoreDocument
        {
            SchemaVersion = source.SchemaVersion,
            LastSelectedPanelId = source.LastSelectedPanelId,
            Panels = source.Panels.Select(static panel => new PanelDefinition
            {
                PanelId = panel.PanelId,
                Name = panel.Name,
                Width = panel.Width,
                Height = panel.Height,
                Widgets = panel.Widgets.Select(static widget => new PanelWidgetDefinition
                {
                    WidgetId = widget.WidgetId,
                    AppId = widget.AppId,
                    X = widget.X,
                    Y = widget.Y,
                    Width = widget.Width,
                    Height = widget.Height,
                    ZIndex = widget.ZIndex,
                    ConfigValues = new Dictionary<string, string>(widget.ConfigValues, StringComparer.OrdinalIgnoreCase),
                }).ToList(),
            }).ToList(),
        };
        document.Normalize();
        return document;
    }

    private string? TryQuarantineCorruptFile()
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            var quarantinedPath = $"{path}.corrupt-{timestamp}.json";
            var suffix = 1;
            while (File.Exists(quarantinedPath))
            {
                quarantinedPath = $"{path}.corrupt-{timestamp}-{suffix}.json";
                suffix++;
            }

            File.Move(path, quarantinedPath);
            return quarantinedPath;
        }
        catch (IOException ex)
        {
            LogFailedQuarantineStoreDocument(logger, path, ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogFailedQuarantineDueToAccessRestrictions(logger, path, ex);
            return null;
        }
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
        catch (IOException ex)
        {
            LogFailedDeleteTempFile(logger, tempPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogFailedDeleteTempFileDueToAccessRestrictions(logger, tempPath, ex);
        }
    }
}
