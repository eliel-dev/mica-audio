namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-comandos-tracked
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-command-state-store.md
public interface ICommandStateStore
{
    int Count { get; }

    void Add(TrackedCommandState command);

    bool TryGetValue(string commandId, out TrackedCommandState? command);

    bool Remove(string commandId, out TrackedCommandState? command);

    TrackedCommandState[] Drain();
}
