namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-comandos-tracked
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-command-state-store.md
public sealed class InMemoryCommandStateStore : ICommandStateStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, TrackedCommandState> pending = new(StringComparer.OrdinalIgnoreCase);

    public int Count
    {
        get
        {
            lock (gate)
            {
                return pending.Count;
            }
        }
    }

    public void Add(TrackedCommandState command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (gate)
        {
            pending[command.CommandId] = command;
        }
    }

    public bool TryGetValue(string commandId, out TrackedCommandState? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        lock (gate)
        {
            return pending.TryGetValue(commandId, out command);
        }
    }

    public bool Remove(string commandId, out TrackedCommandState? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        lock (gate)
        {
            return pending.Remove(commandId, out command);
        }
    }

    public TrackedCommandState[] Drain()
    {
        lock (gate)
        {
            var drained = pending.Values.ToArray();
            pending.Clear();
            return drained;
        }
    }
}
