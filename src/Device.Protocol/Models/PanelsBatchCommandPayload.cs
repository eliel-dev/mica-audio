using System.Globalization;

namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
public sealed class PanelsBatchCommandPayload
{
    public string PanelsSessionId { get; init; } = string.Empty;

    public ulong BatchSequence { get; init; }

    public string DownloadUrl { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public string ContentType { get; init; } = "image/webp";

    public int FrameCount { get; init; }

    public int DurationMs { get; init; }

    public IReadOnlyDictionary<string, string> ToParameters()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["panelsSessionId"] = PanelsSessionId,
            ["batchSequence"] = BatchSequence.ToString(CultureInfo.InvariantCulture),
            ["downloadUrl"] = DownloadUrl,
            ["sha256"] = Sha256,
            ["fileSizeBytes"] = FileSizeBytes.ToString(CultureInfo.InvariantCulture),
            ["contentType"] = ContentType,
            ["frameCount"] = FrameCount.ToString(CultureInfo.InvariantCulture),
            ["durationMs"] = DurationMs.ToString(CultureInfo.InvariantCulture),
        };
    }
}
