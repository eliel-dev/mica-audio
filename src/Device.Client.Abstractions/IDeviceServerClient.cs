using Device.Protocol.Models;

namespace Device.Client;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
// DOCS: docs/wiki/modules/paineis.md#runtime-em-background
// DOCS: docs/handoffs/2026-04-22-device-client-abstractions.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
public interface IDeviceServerClient
{
    event EventHandler? DevicesChanged;

    event EventHandler<string>? LogMessage;

    event EventHandler<DeviceLogMessage>? DeviceLogReceived;

    event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    string GetServerBaseAddress();

    PairingCodeInfo CreatePairingCode(TimeSpan ttl)
        => CreatePairingCodeAsync(ttl, CancellationToken.None).GetAwaiter().GetResult();

    Task<PairingCodeInfo> CreatePairingCodeAsync(TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.FromResult(CreatePairingCode(ttl));

    IReadOnlyList<DeviceSnapshot> GetDevices()
        => GetDevicesAsync(CancellationToken.None).GetAwaiter().GetResult();

    Task<IReadOnlyList<DeviceSnapshot>> GetDevicesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(GetDevices());

    bool RemoveDevice(string deviceId)
        => RemoveDeviceAsync(deviceId, CancellationToken.None).GetAwaiter().GetResult();

    Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(RemoveDevice(deviceId));

    Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        DeviceCommandSessionContext? sessionContext,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => SendCommandTrackedAsync(deviceId, commandType, parameters, timeout, cancellationToken);

    PanelsBatchRegistration RegisterPanelsBatch(
        string deviceId,
        string panelsSessionId,
        ulong batchSequence,
        byte[] payload,
        int frameCount,
        int durationMs,
        string contentType = "image/webp")
        => RegisterPanelsBatchAsync(
                deviceId,
                panelsSessionId,
                batchSequence,
                payload,
                frameCount,
                durationMs,
                contentType,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    Task<PanelsBatchRegistration> RegisterPanelsBatchAsync(
        string deviceId,
        string panelsSessionId,
        ulong batchSequence,
        byte[] payload,
        int frameCount,
        int durationMs,
        string contentType = "image/webp",
        CancellationToken cancellationToken = default)
        => Task.FromResult(RegisterPanelsBatch(deviceId, panelsSessionId, batchSequence, payload, frameCount, durationMs, contentType));

    void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
        => ClearPanelsBatchesAsync(deviceId, panelsSessionId, CancellationToken.None).GetAwaiter().GetResult();

    Task ClearPanelsBatchesAsync(string deviceId, string? panelsSessionId = null, CancellationToken cancellationToken = default)
    {
        ClearPanelsBatches(deviceId, panelsSessionId);
        return Task.CompletedTask;
    }

    Task<PanelLibraryDocument> GetPanelLibraryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PanelLibraryDocument());

    Task SavePanelLibraryAsync(PanelLibraryDocument document, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    Task<MediaAssetInfo> UploadMediaAsync(
        string fileName,
        string contentType,
        byte[] payload,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Media upload is not supported by this device server client.");

    Task<byte[]?> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);

    Task<bool> DeleteMediaAsync(string mediaId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
