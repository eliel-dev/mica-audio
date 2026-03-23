using System.Text.Json;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#overrides-locais-de-nome-para-built-ins
internal sealed class BuiltInPresetNameOverrideStore
{
    private readonly string overridesFilePath;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public BuiltInPresetNameOverrideStore(IOptions<MicaAudioOptions> options)
    {
        var presetsDirectory = string.IsNullOrWhiteSpace(options.Value.PresetsDirectory)
            ? Path.Combine(options.Value.AppDataRoot, "presets")
            : options.Value.PresetsDirectory;
        overridesFilePath = Path.Combine(presetsDirectory, "built-in-name-overrides.json");
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(overridesFilePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(overridesFilePath);
        var raw = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, jsonOptions, cancellationToken).ConfigureAwait(false);
        if (raw is null || raw.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in raw)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var value = pair.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalized[pair.Key.Trim()] = value;
        }

        return normalized;
    }

    public async Task SaveAsync(IReadOnlyDictionary<string, string> overrides, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var normalized = overrides
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static pair => pair.Key.Trim(),
                static pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);

        var directory = Path.GetDirectoryName(overridesFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (normalized.Count == 0)
        {
            if (File.Exists(overridesFilePath))
            {
                File.Delete(overridesFilePath);
            }

            return;
        }

        await using var stream = File.Create(overridesFilePath);
        await JsonSerializer.SerializeAsync(stream, normalized, jsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
