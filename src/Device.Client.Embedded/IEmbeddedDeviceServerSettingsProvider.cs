namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/app-winui.md#referencias-de-codigo
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
public interface IEmbeddedDeviceServerSettingsProvider
{
    Task<EmbeddedDeviceServerSettings> LoadAsync(CancellationToken cancellationToken = default);
}
