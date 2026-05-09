using Device.Protocol.Models;

namespace Device.Client;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
// DOCS: docs/wiki/modules/paineis.md#runtime-em-background
// DOCS: docs/handoffs/2026-04-22-device-client-abstractions.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
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

    /// <summary>
    /// Uploads the active panel definition for the device so MicaAudio.Server
    /// can keep autonomous widgets (Clock today) alive after the WinUI client
    /// disconnects. Default no-op so that test doubles do not have to care.
    /// </summary>
    Task UploadPanelAsync(string deviceId, string panelJson, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Removes the stored panel for the device (e.g. when the user picks a
    /// panel that requires the WinUI client). Default no-op for tests.
    /// </summary>
    Task<bool> DeletePanelAsync(string deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
