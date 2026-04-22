namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
// DOCS: docs/handoffs/2026-04-21-server-embedded-decoupling.md
public sealed record PanelsBatchRegistration(
    string PanelsSessionId,
    ulong BatchSequence,
    long FileSizeBytes,
    string Sha256,
    string ContentType,
    int FrameCount,
    int DurationMs,
    string DownloadUrl);
