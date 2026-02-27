using System.Text.Json;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#pontos-de-alteracao-frequente
internal sealed class PresetRepository
{
    private readonly string appDataRoot;
    private readonly string presetsDir;
    private readonly JsonSerializerOptions jsonOptions;

    public PresetRepository(IOptions<MicaAudioOptions> options)
    {
        appDataRoot = options.Value.AppDataRoot;
        presetsDir = string.IsNullOrWhiteSpace(options.Value.PresetsDirectory)
            ? Path.Combine(appDataRoot, "presets")
            : options.Value.PresetsDirectory;

        jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };
    }

    public async Task<IReadOnlyList<PresetDefinition>> LoadOrSeedAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(presetsDir);
        var defaults = DefaultPresets.Create();

        var files = Directory.GetFiles(presetsDir, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            await SaveAllAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        var loaded = new List<PresetDefinition>();
        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(file);
            var preset = await JsonSerializer.DeserializeAsync<PresetDefinition>(stream, jsonOptions, cancellationToken).ConfigureAwait(false);
            if (preset is not null && !string.IsNullOrWhiteSpace(preset.PresetId))
            {
                loaded.Add(preset);
            }
        }

        if (loaded.Count == 0)
        {
            await SaveAllAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        var (mergedPresets, catalogUpdated) = MergeWithDefaults(loaded, defaults);
        if (catalogUpdated)
        {
            await SaveAllAsync(mergedPresets, cancellationToken).ConfigureAwait(false);
        }

        return mergedPresets;
    }

    public async Task SaveAllAsync(IEnumerable<PresetDefinition> presets, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(appDataRoot);
        Directory.CreateDirectory(presetsDir);

        foreach (var preset in presets)
        {
            var file = Path.Combine(presetsDir, $"{SanitizeFileName(preset.PresetId)}.json");
            await using var stream = File.Create(file);
            await JsonSerializer.SerializeAsync(stream, preset, jsonOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    private static (IReadOnlyList<PresetDefinition> Presets, bool Updated) MergeWithDefaults(
        IReadOnlyList<PresetDefinition> loaded,
        IReadOnlyList<PresetDefinition> defaults)
    {
        var updated = false;
        var loadedById = new Dictionary<string, PresetDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var preset in loaded)
        {
            if (loadedById.TryGetValue(preset.PresetId, out _))
            {
                updated = true;
            }

            loadedById[preset.PresetId] = preset;
        }

        foreach (var defaultPreset in defaults)
        {
            if (!loadedById.TryGetValue(defaultPreset.PresetId, out var existing))
            {
                loadedById[defaultPreset.PresetId] = defaultPreset;
                updated = true;
                continue;
            }

            if (existing.SchemaVersion < defaultPreset.SchemaVersion)
            {
                loadedById[defaultPreset.PresetId] = defaultPreset;
                updated = true;
            }
        }

        var ordered = new List<PresetDefinition>(loadedById.Count);

        foreach (var defaultPreset in defaults)
        {
            if (loadedById.Remove(defaultPreset.PresetId, out var mergedPreset))
            {
                ordered.Add(mergedPreset);
            }
        }

        foreach (var customPreset in loadedById.Values.OrderBy(static p => p.PresetId, StringComparer.OrdinalIgnoreCase))
        {
            ordered.Add(customPreset);
        }

        return (ordered, updated);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars);
    }
}
