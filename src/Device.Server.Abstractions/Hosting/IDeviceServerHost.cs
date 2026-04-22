using Device.Protocol.Contracts;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/handoffs/2026-04-22-device-server-client-boundary.md
public interface IDeviceServerHost : IDeviceFrameTransport, IAsyncDisposable
{
    event EventHandler? DevicesChanged;

    event EventHandler<string>? LogMessage;

    event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    event EventHandler<DeviceLogMessage>? DeviceLogReceived;

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

    Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    bool RemoveDevice(string deviceId);

    PanelsBatchRegistration RegisterPanelsBatch(
        string deviceId,
        string panelsSessionId,
        ulong batchSequence,
        byte[] payload,
        int frameCount,
        int durationMs,
        string contentType = "image/webp");

    void ClearPanelsBatches(string deviceId, string? panelsSessionId = null);
}
