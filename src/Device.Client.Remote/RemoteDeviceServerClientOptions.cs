namespace Device.Client.Remote;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed class RemoteDeviceServerClientOptions
{
    public string BaseAddress { get; set; } = "http://127.0.0.1:5272";

    public string AdminToken { get; set; } = string.Empty;

    public int FrameQueueCapacity { get; set; } = 4;

    public TimeSpan EventReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan FrameReconnectDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
