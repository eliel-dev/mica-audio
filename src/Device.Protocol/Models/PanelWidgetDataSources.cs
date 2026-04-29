namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-29-lan-panel-architecture-realignment.md
public static class PanelWidgetDataSources
{
    public const string Server = "server";
    public const string WindowsClient = "windows-client";
    public const string AndroidClient = "android-client";
    public const string Device = "device";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Server;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            Server => Server,
            WindowsClient => WindowsClient,
            AndroidClient => AndroidClient,
            Device => Device,
            _ => Server,
        };
    }
}
