using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal interface IDeviceRegistryStore
{
    Task<IReadOnlyList<DeviceRecord>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<DeviceRecord> devices, CancellationToken cancellationToken = default);
}
