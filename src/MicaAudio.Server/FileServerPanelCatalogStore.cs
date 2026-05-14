using System.Collections.Concurrent;
using System.Text.Json;
using Device.Server.Hosting;
using Panels.Composition.Models;

namespace MicaAudio.Server;

// Filesystem-backed panel catalog. One JSON file per panel under
// {StorageRoot}/panel-catalog/. The in-memory cache is hot after the first
// boot so list/get calls stay allocation-light. Writes always hit disk before
// returning so a restart never loses a created or edited panel.
public sealed class FileServerPanelCatalogStore : IServerPanelCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string catalogDirectory;
    private readonly ConcurrentDictionary<string, PanelDefinition> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public FileServerPanelCatalogStore(string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        catalogDirectory = Path.Combine(storageRoot, "panel-catalog");
        Directory.CreateDirectory(catalogDirectory);
        WarmCache();
    }

    public IReadOnlyCollection<PanelDefinition> ListAll()
        => cache.Values.Select(p => p.Clone()).ToArray();

    public PanelDefinition? TryGet(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return null;
        }

        return cache.TryGetValue(panelId, out var panel) ? panel.Clone() : null;
    }

    public PanelDefinition Upsert(PanelDefinition panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        if (string.IsNullOrWhiteSpace(panel.PanelId))
        {
            throw new ArgumentException("Panel.PanelId is required.", nameof(panel));
        }

        var snapshot = panel.Clone();
        cache[snapshot.PanelId] = snapshot;
        var path = ResolvePath(snapshot.PanelId);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(path, json);
        return snapshot.Clone();
    }

    public bool Remove(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return false;
        }

        var existed = cache.TryRemove(panelId, out _);
        var path = ResolvePath(panelId);
        if (File.Exists(path))
        {
            File.Delete(path);
            existed = true;
        }

        return existed;
    }

    private string ResolvePath(string panelId)
    {
        var sanitized = SanitizeFileName(panelId);
        return Path.Combine(catalogDirectory, sanitized + ".json");
    }

    private static string SanitizeFileName(string panelId)
    {
        var sb = new System.Text.StringBuilder(panelId.Length);
        foreach (var ch in panelId)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
        }

        var sanitized = sb.ToString();
        return string.IsNullOrEmpty(sanitized) ? "panel" : sanitized;
    }

    private void WarmCache()
    {
        foreach (var path in Directory.EnumerateFiles(catalogDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var panel = JsonSerializer.Deserialize<PanelDefinition>(stream, JsonOptions);
                if (panel is null || string.IsNullOrWhiteSpace(panel.PanelId))
                {
                    continue;
                }

                cache[panel.PanelId] = panel;
            }
            catch (JsonException)
            {
                // Skip corrupt files; will be overwritten on next upsert.
            }
            catch (IOException)
            {
                // Transient; leave cache as-is.
            }
        }
    }
}
