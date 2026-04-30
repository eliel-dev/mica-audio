namespace Device.Client.Remote;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed class RemoteDeviceServerRuntime : IDeviceServerClientRuntime
{
    private readonly RemoteDeviceServerClient client;
    private readonly RemoteDeviceFrameTransport frameTransport;

    public RemoteDeviceServerRuntime(RemoteDeviceServerClient client, RemoteDeviceFrameTransport frameTransport)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.frameTransport = frameTransport ?? throw new ArgumentNullException(nameof(frameTransport));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await client.StartAsync(cancellationToken).ConfigureAwait(false);
        await frameTransport.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        await frameTransport.StopAsync().ConfigureAwait(false);
        await client.StopAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
