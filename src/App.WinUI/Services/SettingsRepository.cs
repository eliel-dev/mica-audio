using System.Text.Json;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Microsoft.Extensions.Options;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#settingsjson
internal sealed class SettingsRepository
{
    private readonly string settingsFile;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public SettingsRepository(IOptions<MicaAudioOptions> options)
    {
        var appDataRoot = options.Value.AppDataRoot;
        Directory.CreateDirectory(appDataRoot);

        settingsFile = string.IsNullOrWhiteSpace(options.Value.SettingsFilePath)
            ? Path.Combine(appDataRoot, "settings.json")
            : options.Value.SettingsFilePath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsFile))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(settingsFile);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, jsonOptions, cancellationToken).ConfigureAwait(false)
               ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(settingsFile);
        await JsonSerializer.SerializeAsync(stream, settings, jsonOptions, cancellationToken).ConfigureAwait(false);
    }
}


