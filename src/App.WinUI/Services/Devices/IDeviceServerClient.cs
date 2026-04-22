using Device.Protocol.Models;
using Device.Server.Hosting;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
// DOCS: docs/wiki/modules/paineis.md#runtime-em-background
// DOCS: docs/handoffs/2026-04-22-device-server-client-boundary.md
internal interface IDeviceServerClient
{
    event EventHandler? DevicesChanged;

    event EventHandler<string>? LogMessage;

    event EventHandler<DeviceLogMessage>? DeviceLogReceived;

    event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    string GetServerBaseAddress();

    PairingCodeInfo CreatePairingCode(TimeSpan ttl);

    IReadOnlyList<DeviceSnapshot> GetDevices();

    bool RemoveDevice(string deviceId);

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
        string contentType = "image/webp");

    void ClearPanelsBatches(string deviceId, string? panelsSessionId = null);
}
