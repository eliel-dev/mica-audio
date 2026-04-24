using Device.Protocol.Models;

namespace Device.Client;

// DOCS: docs/wiki/modules/app-winui.md#client-owned-data-plane-lan
// DOCS: docs/wiki/modules/output-led.md#streamframev3-e-owner-epoch
// DOCS: docs/handoffs/2026-04-24-windows-client-session-ownership.md
public interface IDeviceClientSessionManager : IDisposable
{
    string ClientId { get; }

    uint? GetOwnerEpoch(string deviceId);

    IReadOnlyList<DeviceClientFrameTarget> GetFrameTargets(string? deviceId, string mode);

    DeviceCommandSessionContext? CreateCommandContext(string deviceId);

    Task<DeviceCommandSessionContext> AssumeOwnerAsync(
        string deviceId,
        string mode,
        CancellationToken cancellationToken = default);

    void ObserveDevices(IReadOnlyList<DeviceSnapshot> devices);

    void StartHeartbeat(string deviceId, string mode);

    void StopHeartbeat(string deviceId);

    Task<DeviceClientLockLease> AcquireLockAsync(
        string deviceId,
        string reason,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task ReleaseLockAsync(string deviceId, string lockToken, CancellationToken cancellationToken = default);
}

public sealed record DeviceClientFrameTarget(string DeviceId, uint OwnerEpoch);

public sealed record DeviceClientLockLease(string DeviceId, string LockToken, string Reason);
