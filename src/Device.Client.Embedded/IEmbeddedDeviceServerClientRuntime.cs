namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
public interface IEmbeddedDeviceServerClientRuntime : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
