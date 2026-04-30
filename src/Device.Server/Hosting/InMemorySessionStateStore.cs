using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-sessoes-de-device
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
public sealed class InMemorySessionStateStore : ISessionStateStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, DeviceSessionState> sessions = new(StringComparer.OrdinalIgnoreCase);

    public int Count
    {
        get
        {
            lock (gate)
            {
                return sessions.Count;
            }
        }
    }

    public DeviceSessionState? Upsert(DeviceSessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (gate)
        {
            sessions.TryGetValue(session.Record.DeviceId, out var previous);
            sessions[session.Record.DeviceId] = session;
            return previous;
        }
    }

    public bool TryGetValue(string deviceId, out DeviceSessionState? session)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            session = null;
            return false;
        }

        lock (gate)
        {
            return sessions.TryGetValue(deviceId, out session);
        }
    }

    public bool Remove(string deviceId, out DeviceSessionState? session)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            session = null;
            return false;
        }

        lock (gate)
        {
            return sessions.Remove(deviceId, out session);
        }
    }

    public DeviceSnapshot[] CreateSnapshots(DateTimeOffset now, TimeSpan offlineTimeout)
    {
        lock (gate)
        {
            return sessions.Values
                .Select(session => session.ToSnapshot(now, offlineTimeout))
                .OrderByDescending(snapshot => snapshot.LastSeenUtc)
                .ToArray();
        }
    }

    public DeviceRecord[] CreateRecords()
    {
        lock (gate)
        {
            return sessions.Values
                .Select(session => session.Record)
                .OrderByDescending(record => record.LastSeenUtc)
                .ToArray();
        }
    }

    public DeviceSessionState[] Drain()
    {
        lock (gate)
        {
            var drained = sessions.Values.ToArray();
            sessions.Clear();
            return drained;
        }
    }
}
