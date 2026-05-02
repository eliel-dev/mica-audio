using Device.Client;
using Device.Protocol.Contracts;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/handoffs/2026-04-22-device-server-client-boundary.md
// DOCS: docs/handoffs/2026-04-22-device-client-abstractions.md
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

    Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        DeviceCommandSessionContext? sessionContext,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => SendCommandTrackedAsync(deviceId, commandType, parameters, timeout, cancellationToken);

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

    Task<PanelLibraryDocument> GetPanelLibraryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PanelLibraryDocument());

    Task SavePanelLibraryAsync(PanelLibraryDocument document, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    Task<PanelRuntimeStateDocument> GetPanelRuntimeStateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PanelRuntimeStateDocument());

    Task SavePanelRuntimeStateAsync(PanelRuntimeStateDocument document, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    Task<PanelRuntimeStatusDocument> GetPanelRuntimeStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PanelRuntimeStatusDocument());

    Task<MediaAssetInfo> UploadMediaAsync(
        string fileName,
        string contentType,
        byte[] payload,
        long maxUploadBytes,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Media upload is not supported by this device server host.");

    Task<byte[]?> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);

    Task<bool> DeleteMediaAsync(string mediaId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
