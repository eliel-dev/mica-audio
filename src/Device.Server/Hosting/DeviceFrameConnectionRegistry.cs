namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-sessoes-de-device
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
internal sealed class DeviceFrameConnectionRegistry
{
    private readonly Dictionary<string, DeviceFrameConnection> connections = new(StringComparer.OrdinalIgnoreCase);

    public DeviceFrameConnection GetOrCreate(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (!connections.TryGetValue(deviceId, out var connection))
        {
            connection = new DeviceFrameConnection();
            connections[deviceId] = connection;
        }

        return connection;
    }

    public bool TryGetValue(string deviceId, out DeviceFrameConnection? connection)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            connection = null;
            return false;
        }

        return connections.TryGetValue(deviceId, out connection);
    }

    public bool Remove(string deviceId, out DeviceFrameConnection? connection)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            connection = null;
            return false;
        }

        return connections.Remove(deviceId, out connection);
    }

    public bool RemoveAndDispose(string deviceId)
    {
        DeviceFrameConnection? connection = null;
        try
        {
            return Remove(deviceId, out connection) && connection is not null;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    public DeviceFrameConnection[] GetOpenConnections()
    {
        return connections.Values
            .Where(connection => connection.Socket is { State: System.Net.WebSockets.WebSocketState.Open })
            .ToArray();
    }

    public (string DeviceId, DeviceFrameConnection Connection)[] GetOpenConnectionEntries()
    {
        return connections
            .Where(pair => pair.Value.Socket is { State: System.Net.WebSockets.WebSocketState.Open })
            .Select(pair => (pair.Key, pair.Value))
            .ToArray();
    }

    public DeviceFrameConnection[] Drain()
    {
        var drained = connections.Values.ToArray();
        connections.Clear();
        return drained;
    }
}
