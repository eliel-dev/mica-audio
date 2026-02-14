using System.Text.Json;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services;

internal sealed class SettingsRepository
{
    private readonly string settingsFile;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public SettingsRepository(string appDataRoot)
    {
        Directory.CreateDirectory(appDataRoot);
        settingsFile = Path.Combine(appDataRoot, "settings.json");
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
