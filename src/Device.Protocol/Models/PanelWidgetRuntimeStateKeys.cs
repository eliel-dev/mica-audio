namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
public static class PanelWidgetRuntimeStateKeys
{
    public const string MediaId = "mediaId";
    public const string MediaIds = "mediaIds";
    public const string SourcePath = "sourcePath";

    public static bool IsServerSafe(string? key)
    {
        return key?.Trim() switch
        {
            MediaId => true,
            MediaIds => true,
            _ => false,
        };
    }
}
