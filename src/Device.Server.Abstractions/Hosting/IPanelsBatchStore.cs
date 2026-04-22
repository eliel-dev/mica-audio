namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-panels-batch-storage.md
public interface IPanelsBatchStore
{
    PanelsBatchEntry Save(PanelsBatchWrite batch);

    bool TryGet(string deviceId, string panelsSessionId, ulong batchSequence, out PanelsBatchEntry? batch);

    void Clear(string deviceId, string? panelsSessionId = null);
}
