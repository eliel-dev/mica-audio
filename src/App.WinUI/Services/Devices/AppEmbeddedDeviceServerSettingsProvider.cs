using Device.Client.Embedded;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/app-winui.md#referencias-de-codigo
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
internal sealed class AppEmbeddedDeviceServerSettingsProvider(
    SettingsRepository settingsRepository,
    AppSettingsDomainService settingsDomainService)
    : IEmbeddedDeviceServerSettingsProvider
{
    public async Task<EmbeddedDeviceServerSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = settingsDomainService.Migrate(await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false));
        return new EmbeddedDeviceServerSettings
        {
            DeviceFreshThresholdSeconds = settings.DeviceFreshThresholdSeconds,
            AllowLegacyWebSocketQueryToken = settings.AllowLegacyWebSocketQueryToken,
        };
    }
}
