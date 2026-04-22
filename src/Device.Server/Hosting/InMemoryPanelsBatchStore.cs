using System.Security.Cryptography;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-panels-batch-storage.md
public sealed class InMemoryPanelsBatchStore : IPanelsBatchStore
{
    private const int MaxPanelsBatchesPerDevice = 4;

    private readonly object gate = new();
    private readonly Dictionary<string, SortedDictionary<ulong, PanelsBatchEntry>> batchesByDevice =
        new(StringComparer.OrdinalIgnoreCase);

    public PanelsBatchEntry Save(PanelsBatchWrite batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.DeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.PanelsSessionId);
        ArgumentNullException.ThrowIfNull(batch.Payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.ContentType);

        var normalizedDeviceId = batch.DeviceId.Trim();
        var normalizedSessionId = batch.PanelsSessionId.Trim();
        var normalizedContentType = batch.ContentType.Trim();
        var entry = new PanelsBatchEntry(
            normalizedDeviceId,
            normalizedSessionId,
            batch.BatchSequence,
            batch.Payload,
            batch.Payload.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(batch.Payload)),
            normalizedContentType,
            batch.FrameCount,
            batch.DurationMs);

        lock (gate)
        {
            if (!batchesByDevice.TryGetValue(normalizedDeviceId, out var batches))
            {
                batches = new SortedDictionary<ulong, PanelsBatchEntry>();
                batchesByDevice[normalizedDeviceId] = batches;
            }

            foreach (var sequence in batches
                         .Where(pair => !string.Equals(pair.Value.PanelsSessionId, normalizedSessionId, StringComparison.Ordinal))
                         .Select(static pair => pair.Key)
                         .ToArray())
            {
                batches.Remove(sequence);
            }

            batches[batch.BatchSequence] = entry;

            while (batches.Count > MaxPanelsBatchesPerDevice)
            {
                batches.Remove(batches.First().Key);
            }
        }

        return entry;
    }

    public bool TryGet(string deviceId, string panelsSessionId, ulong batchSequence, out PanelsBatchEntry? batch)
    {
        batch = null;
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(panelsSessionId))
        {
            return false;
        }

        var normalizedDeviceId = deviceId.Trim();
        var normalizedSessionId = panelsSessionId.Trim();
        lock (gate)
        {
            if (!batchesByDevice.TryGetValue(normalizedDeviceId, out var batches)
                || !batches.TryGetValue(batchSequence, out var stored)
                || !string.Equals(stored.PanelsSessionId, normalizedSessionId, StringComparison.Ordinal))
            {
                return false;
            }

            batch = stored;
            return true;
        }
    }

    public void Clear(string deviceId, string? panelsSessionId = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        var normalizedDeviceId = deviceId.Trim();
        var normalizedSessionId = string.IsNullOrWhiteSpace(panelsSessionId) ? null : panelsSessionId.Trim();
        lock (gate)
        {
            if (!batchesByDevice.TryGetValue(normalizedDeviceId, out var batches))
            {
                return;
            }

            if (normalizedSessionId is null)
            {
                batchesByDevice.Remove(normalizedDeviceId);
                return;
            }

            foreach (var sequence in batches
                         .Where(pair => string.Equals(pair.Value.PanelsSessionId, normalizedSessionId, StringComparison.Ordinal))
                         .Select(static pair => pair.Key)
                         .ToArray())
            {
                batches.Remove(sequence);
            }

            if (batches.Count == 0)
            {
                batchesByDevice.Remove(normalizedDeviceId);
            }
        }
    }
}
