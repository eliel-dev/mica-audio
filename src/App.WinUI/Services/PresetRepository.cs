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

        var output = new List<PresetDefinition>();
        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(file);
            var preset = await JsonSerializer.DeserializeAsync<PresetDefinition>(stream, jsonOptions, cancellationToken).ConfigureAwait(false);
            if (preset is not null)
            {
                output.Add(preset);
            }
        }

        if (output.Count == 0)
        {
            await SaveAllAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        if (NeedsCatalogReset(output, defaults))
        {
            await ReplaceAllAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        return output;
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

    private async Task ReplaceAllAsync(IReadOnlyList<PresetDefinition> presets, CancellationToken cancellationToken)
    {
        foreach (var file in Directory.GetFiles(presetsDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
        }

        await SaveAllAsync(presets, cancellationToken).ConfigureAwait(false);
    }

    private static bool NeedsCatalogReset(IReadOnlyList<PresetDefinition> loaded, IReadOnlyList<PresetDefinition> defaults)
    {
        if (loaded.Count != defaults.Count)
        {
            return true;
        }

        var loadedById = loaded
            .Where(p => !string.IsNullOrWhiteSpace(p.PresetId))
            .ToDictionary(p => p.PresetId, StringComparer.OrdinalIgnoreCase);

        foreach (var defaultPreset in defaults)
        {
            if (!loadedById.TryGetValue(defaultPreset.PresetId, out var loadedPreset))
            {
                return true;
            }

            if (loadedPreset.SchemaVersion < defaultPreset.SchemaVersion)
            {
                return true;
            }

            if (!string.Equals(loadedPreset.RendererId, defaultPreset.RendererId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars);
    }
}



