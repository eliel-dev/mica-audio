namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed class AdminPanelsBatchRegistrationResponse
{
    public string PanelsSessionId { get; init; } = string.Empty;

    public ulong BatchSequence { get; init; }

    public long FileSizeBytes { get; init; }

    public string DownloadUrl { get; init; } = string.Empty;

    public string ContentType { get; init; } = "image/webp";

    public int FrameCount { get; init; }

    public int DurationMs { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}
