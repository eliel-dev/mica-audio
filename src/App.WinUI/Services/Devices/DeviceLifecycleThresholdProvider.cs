namespace App.WinUI.Services.Devices;

internal sealed class DeviceLifecycleThresholdProvider : IDisposable
{
    private readonly SettingsRepository? settingsRepository;
    private readonly AppSettingsDomainService? settingsDomainService;
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private DeviceLifecycleThresholds thresholds = DeviceLifecycleThresholds.Default;
    private bool loaded;
    private bool disposed;

    public DeviceLifecycleThresholdProvider(
        SettingsRepository? settingsRepository,
        AppSettingsDomainService? settingsDomainService)
    {
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;

        if (settingsRepository is null || settingsDomainService is null)
        {
            loaded = true;
        }
    }

    public async Task<DeviceLifecycleThresholds> EnsureLoadedAsync()
    {
        if (loaded)
        {
            return thresholds;
        }

        await loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (loaded)
            {
                return thresholds;
            }

            if (settingsRepository is null || settingsDomainService is null)
            {
                thresholds = DeviceLifecycleThresholds.Default;
            }
            else
            {
                try
                {
                    var loadedSettings = await settingsRepository.LoadAsync().ConfigureAwait(false);
                    var settings = settingsDomainService.Migrate(loadedSettings);
                    thresholds = DeviceLifecycleThresholds.FromSettings(settings);
                }
                catch
                {
                    thresholds = DeviceLifecycleThresholds.Default;
                }
            }

            loaded = true;
            return thresholds;
        }
        finally
        {
            loadGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        loadGate.Dispose();
    }
}
