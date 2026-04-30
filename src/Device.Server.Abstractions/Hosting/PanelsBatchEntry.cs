namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
// DOCS: docs/handoffs/2026-04-22-device-server-panels-batch-storage.md
public sealed record PanelsBatchEntry(
    string DeviceId,
    string PanelsSessionId,
    ulong BatchSequence,
    byte[] Payload,
    long FileSizeBytes,
    string Sha256,
    string ContentType,
    int FrameCount,
    int DurationMs);
