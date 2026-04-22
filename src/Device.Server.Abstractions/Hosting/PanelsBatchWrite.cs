namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
// DOCS: docs/handoffs/2026-04-22-device-server-panels-batch-storage.md
public sealed record PanelsBatchWrite(
    string DeviceId,
    string PanelsSessionId,
    ulong BatchSequence,
    byte[] Payload,
    int FrameCount,
    int DurationMs,
    string ContentType = "image/webp");
