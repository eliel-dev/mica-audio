using App.WinUI.Services;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class SettingsRepositoryTests
{
    [Fact]
    public async Task LoadAsync_ShouldDefaultUseMicaBackdropToTrue_WhenPropertyIsMissing()
    {
        var appDataRoot = CreateTestDirectory();
        try
        {
            var settingsFile = Path.Combine(appDataRoot, "settings.json");
            await File.WriteAllTextAsync(settingsFile, "{\"activePresetId\":\"legacy\"}");

            var repository = CreateRepository(appDataRoot);

            var result = await repository.LoadAsync();

            Assert.True(result.UseMicaBackdrop);
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveAsync_ThenLoadAsync_ShouldRoundTripUseMicaBackdrop(bool useMicaBackdrop)
    {
        var appDataRoot = CreateTestDirectory();
        try
        {
            var repository = CreateRepository(appDataRoot);
            var settings = new AppSettings
            {
                UseMicaBackdrop = useMicaBackdrop,
                ActivePresetId = "custom",
            };

            await repository.SaveAsync(settings);
            var result = await repository.LoadAsync();

            Assert.Equal(useMicaBackdrop, result.UseMicaBackdrop);
            Assert.Equal("custom", result.ActivePresetId);
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    private static SettingsRepository CreateRepository(string appDataRoot)
    {
        var options = Options.Create(new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            SettingsFilePath = Path.Combine(appDataRoot, "settings.json"),
        });

        return new SettingsRepository(options);
    }

    private static string CreateTestDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
