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
    /// can keep autonomous widgets (Clock, GIF/Image) alive after the WinUI client
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

    /// <summary>
    /// Returns the panel currently stored on the server for the device, plus
    /// its server-side capability classification ("ServerCapable",
    /// "RequiresClient", "Empty"). Returns <see langword="null"/> when no panel
    /// is stored. The server is the source of truth: clients should sync local
    /// UI state with what the server is actually rendering on the device.
    /// </summary>
    Task<ServerPanelSnapshot?> GetServerPanelAsync(string deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult<ServerPanelSnapshot?>(null);

    /// <summary>
    /// Uploads a raw media file (GIF/PNG/JPG/BMP) to the server media store so
    /// gifhub75 widgets can be rendered autonomously. The <paramref name="mediaId"/>
    /// must include the extension (e.g. "abc123.gif"). Default no-op for tests.
    /// </summary>
    Task UploadMediaAsync(string deviceId, string mediaId, byte[] bytes, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Removes a previously uploaded media file. Default no-op for tests.
    /// </summary>
    Task<bool> DeleteMediaAsync(string deviceId, string mediaId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    // ── Panel catalog ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw JSON array from GET /api/v1/admin/panels — the full
    /// server panel catalog including activeOnDeviceId annotations. Returns
    /// null when the server is unreachable or returns a non-success status.
    /// Default no-op for tests (returns null).
    /// </summary>
    Task<string?> GetPanelCatalogJsonAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    /// <summary>
    /// Creates or updates a panel in the server catalog (PUT /panels/{panelId}).
    /// The <paramref name="panelJson"/> must match the shape of
    /// <c>Panels.Composition.Models.PanelDefinition</c>.
    /// Returns true on success. Default no-op for tests (returns false).
    /// </summary>
    Task<bool> UpsertCatalogPanelAsync(string panelId, string panelJson, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <summary>
    /// Removes a panel from the server catalog (DELETE /panels/{panelId}).
    /// Returns true when the panel existed and was removed.
    /// Default no-op for tests (returns false).
    /// </summary>
    Task<bool> DeleteCatalogPanelAsync(string panelId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
