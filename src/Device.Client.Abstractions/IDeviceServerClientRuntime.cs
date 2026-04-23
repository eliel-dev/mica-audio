namespace Device.Client;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public interface IDeviceServerClientRuntime : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
