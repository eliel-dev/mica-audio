using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-pairing
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-pairing-store.md
public interface IDevicePairingStore
{
    PairingCodeInfo SaveCode(string code, TimeSpan ttl, DateTimeOffset now);

    bool TryConsumeCode(string code, DateTimeOffset now);

    bool TryRegisterAttempt(
        string remoteIpKey,
        int attemptsPerWindow,
        TimeSpan window,
        DateTimeOffset now,
        out int retryAfterSeconds);

    void ResetAttempts(string remoteIpKey);

    void Clear();
}
