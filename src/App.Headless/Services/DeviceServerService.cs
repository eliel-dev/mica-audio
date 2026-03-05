using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace App.Headless.Services;

/// <summary>
/// Starts DeviceServerHost on 0.0.0.0:5174 and manages device registry persistence.
/// </summary>
internal sealed class DeviceServerService : IHostedService, IAsyncDisposable
{
    private readonly IDeviceServerHost serverHost;
    private readonly HeadlessDeviceRegistryStore registryStore;
    private readonly ILogger<DeviceServerService> logger;

    private static readonly TimeSpan RegistrySaveInterval = TimeSpan.FromSeconds(30);
    private Timer? saveTimer;

    public DeviceServerService(
        IDeviceServerHost serverHost,
        HeadlessDeviceRegistryStore registryStore,
        ILogger<DeviceServerService> logger)
    {
        this.serverHost = serverHost;
        this.registryStore = registryStore;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var existing = await registryStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        serverHost.SeedDevices(existing);

        await serverHost.StartAsync(new ServerConfig
        {
            ListenHost = "0.0.0.0",
            Port = 5174,
            MaxDevices = 5,
            MdnsServiceName = "_micaaudio._tcp",
            PublicHost = ResolvePublicHost(),
        }, cancellationToken).ConfigureAwait(false);

        serverHost.DevicesChanged += OnDevicesChanged;
        serverHost.LogMessage += OnLogMessage;

        saveTimer = new Timer(_ => _ = SaveRegistrySafeAsync(), null, RegistrySaveInterval, RegistrySaveInterval);

        logger.LogInformation("DeviceServerHost ativo em http://0.0.0.0:5174");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (saveTimer is not null)
        {
            await saveTimer.DisposeAsync().ConfigureAwait(false);
        }

        serverHost.DevicesChanged -= OnDevicesChanged;
        serverHost.LogMessage -= OnLogMessage;

        await SaveRegistrySafeAsync().ConfigureAwait(false);
        await serverHost.StopAsync().ConfigureAwait(false);

        logger.LogInformation("DeviceServerHost parado.");
    }

    private void OnDevicesChanged(object? sender, EventArgs e) => _ = SaveRegistrySafeAsync();
    private void OnLogMessage(object? sender, string msg) => logger.LogInformation("{Message}", msg);

    private async Task SaveRegistrySafeAsync()
    {
        try
        {
            var records = serverHost.GetDeviceRecords();
            await registryStore.SaveAsync(records).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao salvar registro de dispositivos.");
        }
    }

    private static string ResolvePublicHost()
    {
        try
        {
            var host = System.Net.Dns.GetHostName();
            return string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (saveTimer is not null)
        {
            await saveTimer.DisposeAsync().ConfigureAwait(false);
        }

        await serverHost.DisposeAsync().ConfigureAwait(false);
    }
}
