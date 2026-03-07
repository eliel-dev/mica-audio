using App.WinUI.Services;
using App.WinUI.Services.Devices;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class DeviceLifecycleThresholdProviderTests
{
    [Fact]
    public async Task EnsureLoadedAsync_ShouldFallbackToDefaultWhenDependenciesMissing()
    {
        using var provider = new DeviceLifecycleThresholdProvider(settingsRepository: null, settingsDomainService: null);

        var thresholds = await provider.EnsureLoadedAsync();

        Assert.Equal(DeviceLifecycleThresholds.Default, thresholds);
    }

    [Fact]
    public async Task EnsureLoadedAsync_ShouldLoadThresholdsFromSettingsRepository()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var options = Options.Create(new MicaAudioOptions
            {
                AppDataRoot = tempRoot,
                SettingsFilePath = Path.Combine(tempRoot, "settings.json"),
            });

            var repository = new SettingsRepository(options);
            var domainService = new AppSettingsDomainService();

            await repository.SaveAsync(new AppSettings
            {
                DeviceFreshThresholdSeconds = 15,
                DeviceStaleThresholdMinutes = 3,
                DeviceDormantThresholdHours = 6,
            });

            using var provider = new DeviceLifecycleThresholdProvider(repository, domainService);
            var thresholds = await provider.EnsureLoadedAsync();

            Assert.Equal(TimeSpan.FromSeconds(15), thresholds.Fresh);
            Assert.Equal(TimeSpan.FromMinutes(3), thresholds.Stale);
            Assert.Equal(TimeSpan.FromHours(6), thresholds.Dormant);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
