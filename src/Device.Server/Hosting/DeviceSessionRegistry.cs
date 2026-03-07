using System.Net.WebSockets;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#responsabilidades
internal sealed class DeviceSessionRegistry
{
    private readonly Dictionary<string, DeviceSession> sessions = new(StringComparer.OrdinalIgnoreCase);

    public int Count => sessions.Count;

    public DeviceSession? Set(DeviceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        sessions.TryGetValue(session.Record.DeviceId, out var previous);
        sessions[session.Record.DeviceId] = session;
        return previous;
    }

    public bool TryGetValue(string deviceId, out DeviceSession? session)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            session = null;
            return false;
        }

        return sessions.TryGetValue(deviceId, out session);
    }

    public bool Remove(string deviceId, out DeviceSession? session)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            session = null;
            return false;
        }

        return sessions.Remove(deviceId, out session);
    }

    public DeviceSnapshot[] CreateSnapshots(TimeSpan offlineTimeout)
    {
        return sessions.Values
            .Select(session => session.ToSnapshot(offlineTimeout))
            .OrderByDescending(snapshot => snapshot.LastSeenUtc)
            .ToArray();
    }

    public DeviceRecord[] CreateRecords()
    {
        return sessions.Values
            .Select(session => session.Record)
            .OrderByDescending(record => record.LastSeenUtc)
            .ToArray();
    }

    public DeviceSession[] GetOpenSocketSessions()
    {
        return sessions.Values
            .Where(session => session.Socket is { State: WebSocketState.Open })
            .ToArray();
    }

    public DeviceSession[] Drain()
    {
        var drained = sessions.Values.ToArray();
        sessions.Clear();
        return drained;
    }
}
