using Device.Protocol.Models;

namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#tokens-de-dispositivo-em-repouso
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
public interface IEmbeddedDeviceRegistryStore
{
    Task<IReadOnlyList<DeviceRecord>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<DeviceRecord> devices, CancellationToken cancellationToken = default);
}
