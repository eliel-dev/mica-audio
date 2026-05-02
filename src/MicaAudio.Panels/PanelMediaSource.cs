namespace MicaAudio.Panels;

// DOCS: docs/wiki/modules/paineis.md#compositor-compartilhado
public sealed record PanelMediaSource(
    string CacheKey,
    byte[] Payload,
    string ContentType,
    string FileName);
