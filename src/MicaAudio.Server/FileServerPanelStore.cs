using System.Collections.Concurrent;
using System.Text.Json;
using Device.Server.Hosting;
using Panels.Composition.Models;

namespace MicaAudio.Server;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/handoffs/2026-05-12-display-state-gif-panels.md
//
// Filesystem-backed mirror of the in-memory store. One JSON file per device
// under {StorageRoot}/panels/. The cache is hot in memory after the first
// successful load so per-frame reads in PanelCompositorHostedService stay
// allocation-free; writes always persist to disk before returning so the
// next process boot can resume rendering exactly the last uploaded panel.
public sealed class FileServerPanelStore : IServerPanelStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string panelsDirectory;
    private readonly ConcurrentDictionary<string, PanelDefinition> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public FileServerPanelStore(string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        panelsDirectory = Path.Combine(storageRoot, "panels");
        Directory.CreateDirectory(panelsDirectory);
        WarmCache();
    }

    public PanelDefinition? TryGet(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return cache.TryGetValue(deviceId, out var panel) ? panel.Clone() : null;
    }

    public void Save(string deviceId, PanelDefinition panel)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("deviceId is required.", nameof(deviceId));
        }

        ArgumentNullException.ThrowIfNull(panel);
        var snapshot = panel.Clone();
        cache[deviceId] = snapshot;
        var path = ResolvePath(deviceId);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(path, json);

        // Panel upload cancels any prior explicit-clear tombstone.
        var clearedPath = ResolveClearedPath(deviceId);
        if (File.Exists(clearedPath))
        {
            File.Delete(clearedPath);
        }
    }

    public bool Remove(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        var existed = cache.TryRemove(deviceId, out _);
        var path = ResolvePath(deviceId);
        if (File.Exists(path))
        {
            File.Delete(path);
            existed = true;
        }

        // Write tombstone so the ESP32 can distinguish "explicitly cleared"
        // from "never had a panel" on the next display-state query. A DELETE is
        // an explicit clear even when there was no panel file to remove.
        File.WriteAllText(ResolveClearedPath(deviceId), string.Empty);

        return existed;
    }

    public IReadOnlyCollection<string> EnumerateDeviceIds()
    {
        return cache.Keys.ToArray();
    }

    public DevicePanelDisplayState GetDisplayState(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return DevicePanelDisplayState.NeverSet;
        }

        if (cache.ContainsKey(deviceId))
        {
            return DevicePanelDisplayState.Active;
        }

        return File.Exists(ResolveClearedPath(deviceId))
            ? DevicePanelDisplayState.ExplicitlyCleared
            : DevicePanelDisplayState.NeverSet;
    }

    private string ResolvePath(string deviceId)
    {
        // Sanitize: only allow [A-Za-z0-9_-]. Anything else gets replaced so we
        // never write outside panelsDirectory regardless of the deviceId source.
        var sanitized = SanitizeFileName(deviceId);
        return Path.Combine(panelsDirectory, sanitized + ".json");
    }

    private string ResolveClearedPath(string deviceId)
    {
        var sanitized = SanitizeFileName(deviceId);
        return Path.Combine(panelsDirectory, sanitized + ".cleared");
    }

    private static string SanitizeFileName(string deviceId)
    {
        var sb = new System.Text.StringBuilder(deviceId.Length);
        foreach (var ch in deviceId)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
        }

        var sanitized = sb.ToString();
        return string.IsNullOrEmpty(sanitized) ? "device" : sanitized;
    }

    private void WarmCache()
    {
        foreach (var path in Directory.EnumerateFiles(panelsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var panel = JsonSerializer.Deserialize<PanelDefinition>(stream, JsonOptions);
                if (panel is null)
                {
                    continue;
                }

                var deviceId = Path.GetFileNameWithoutExtension(path);
                cache[deviceId] = panel;
            }
            catch (JsonException)
            {
                // Skip corrupt files; they will be overwritten on the next upload.
            }
            catch (IOException)
            {
                // Transient IO errors; leave cache as-is.
            }
        }
    }
}
