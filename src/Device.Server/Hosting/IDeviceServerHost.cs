using Device.Protocol.Contracts;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

public interface IDeviceServerHost : IAsyncDisposable
{
    event EventHandler? DevicesChanged;

    event EventHandler<string>? LogMessage;

    event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    Task StartAsync(ServerConfig config, CancellationToken cancellationToken = default);

    Task StopAsync();

    PairingCodeInfo CreatePairingCode(TimeSpan ttl);

    IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot();

    IReadOnlyList<DeviceRecord> GetDeviceRecords();

    void SeedDevices(IEnumerable<DeviceRecord> devices);

    Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default);

    Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    bool SetOtaArtifact(string mergedBinPath, string version);

    bool RemoveDevice(string deviceId);

    void BroadcastFrame(byte[] framePayload);
}
