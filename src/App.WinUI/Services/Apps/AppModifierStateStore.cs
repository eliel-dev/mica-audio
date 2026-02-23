using System.Text.Json;
using App.WinUI.Models.Apps;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
internal sealed class AppModifierStateStore : IAppModifierStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim ioGate = new(1, 1);
    private readonly Dictionary<string, AppConfigDraft> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly string path;
    private bool loaded;

    public AppModifierStateStore(IOptions<MicaAudioOptions> options)
    {
        var configuredPath = options.Value.AppsModifierStatePath;
        path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(options.Value.AppDataRoot, "apps", "modifiers.json")
            : configuredPath;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (loaded)
            {
                return;
            }

            entries.Clear();
            if (File.Exists(path))
            {
                await using var stream = File.OpenRead(path);
                var document = await JsonSerializer.DeserializeAsync<AppModifierStateDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? new AppModifierStateDocument();

                foreach (var entry in document.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.DeviceId) || string.IsNullOrWhiteSpace(entry.AppId))
                    {
                        continue;
                    }

                    var key = BuildKey(entry.DeviceId, entry.AppId);
                    entries[key] = new AppConfigDraft
                    {
                        Values = new Dictionary<string, string>(entry.Values, StringComparer.OrdinalIgnoreCase),
                    };
                }
            }

            loaded = true;
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task<AppConfigDraft?> GetDraftAsync(string deviceId, string appId, CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        var key = BuildKey(deviceId, appId);

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!entries.TryGetValue(key, out var draft))
            {
                return null;
            }

            return new AppConfigDraft
            {
                Values = new Dictionary<string, string>(draft.Values, StringComparer.OrdinalIgnoreCase),
            };
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task SetDraftAsync(string deviceId, string appId, AppConfigDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(draft);

        await LoadAsync(cancellationToken).ConfigureAwait(false);

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = BuildKey(deviceId, appId);
            entries[key] = new AppConfigDraft
            {
                Values = new Dictionary<string, string>(draft.Values, StringComparer.OrdinalIgnoreCase),
            };

            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task ClearDraftAsync(string deviceId, string appId, CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken).ConfigureAwait(false);

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            entries.Remove(BuildKey(deviceId, appId));
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ioGate.Release();
        }
    }

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var document = new AppModifierStateDocument
        {
            Entries = entries.Select(static pair =>
            {
                var split = pair.Key.Split('|', 2, StringSplitOptions.TrimEntries);
                var deviceId = split.Length > 0 ? split[0] : string.Empty;
                var appId = split.Length > 1 ? split[1] : string.Empty;

                return new AppModifierStateEntry
                {
                    DeviceId = deviceId,
                    AppId = appId,
                    Values = new Dictionary<string, string>(pair.Value.Values, StringComparer.OrdinalIgnoreCase),
                };
            }).ToArray(),
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildKey(string deviceId, string appId)
    {
        return string.Concat(deviceId.Trim(), "|", appId.Trim());
    }
}
