namespace Device.Server.Hosting;

// DOCS: docs/wiki/guides/add-device-command.md#passos
internal sealed class PendingTrackedCommandStore
{
    private readonly Dictionary<string, PendingTrackedCommand> pending = new(StringComparer.OrdinalIgnoreCase);

    public int Count => pending.Count;

    public void Add(PendingTrackedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        pending[command.CommandId] = command;
    }

    public bool TryGetValue(string commandId, out PendingTrackedCommand? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        return pending.TryGetValue(commandId, out command);
    }

    public bool Remove(string commandId, out PendingTrackedCommand? command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        return pending.Remove(commandId, out command);
    }

    public PendingTrackedCommand[] Drain()
    {
        var drained = pending.Values.ToArray();
        pending.Clear();
        return drained;
    }
}
