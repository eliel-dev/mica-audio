using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-sessoes-de-device
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
public interface ISessionStateStore
{
    int Count { get; }

    DeviceSessionState? Upsert(DeviceSessionState session);

    bool TryGetValue(string deviceId, out DeviceSessionState? session);

    bool Remove(string deviceId, out DeviceSessionState? session);

    DeviceSnapshot[] CreateSnapshots(DateTimeOffset now, TimeSpan offlineTimeout);

    DeviceRecord[] CreateRecords();

    DeviceSessionState[] Drain();
}
