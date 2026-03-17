namespace App.WinUI.Infrastructure.Serial;

// DOCS: docs/wiki/guides/setup-new-device.md#passos
internal interface ISerialPortCatalogService
{
    Task<IReadOnlyList<SerialPortDescriptor>> ListAsync(bool includeAllPorts, CancellationToken cancellationToken = default);
}
