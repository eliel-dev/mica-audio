using System.Text.Json;
using Device.Server.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MicaAudio.Server;

// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#fase-1---server-standalone-cloud-ready-sem-mudar-wire
// DOCS: docs/handoffs/2026-04-22-micaudio-server-standalone.md
public sealed partial class MicaAudioServerRuntime : IHostedService, IAsyncDisposable
{
    private readonly IDeviceServerHost serverHost;
    private readonly ServerPanelRuntimeService panelRuntimeService;
    private readonly StandaloneDeviceRegistryStore registryStore;
    private readonly MicaAudioServerOptions options;
    private readonly ILogger<MicaAudioServerRuntime> logger;
    private readonly object saveGate = new();

    private bool started;
    private bool saveScheduled;

    public MicaAudioServerRuntime(
        IDeviceServerHost serverHost,
        ServerPanelRuntimeService panelRuntimeService,
        StandaloneDeviceRegistryStore registryStore,
        MicaAudioServerOptions options,
        ILogger<MicaAudioServerRuntime> logger)
    {
        this.serverHost = serverHost;
        this.panelRuntimeService = panelRuntimeService;
        this.registryStore = registryStore;
        this.options = options;
        this.logger = logger;

        serverHost.DevicesChanged += OnDevicesChanged;
        serverHost.LogMessage += OnServerLogMessage;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (started)
        {
            return;
        }

        var records = await registryStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        serverHost.SeedDevices(records);

        await serverHost.StartAsync(MicaAudioServerBootstrap.CreateServerConfig(options), cancellationToken).ConfigureAwait(false);
        await panelRuntimeService.StartAsync(cancellationToken).ConfigureAwait(false);

        if (options.StartupPairCodeTtlSeconds > 0)
        {
            var pairing = serverHost.CreatePairingCode(TimeSpan.FromSeconds(options.StartupPairCodeTtlSeconds));
            LogStartupPairCode(logger, pairing.Code, pairing.ExpiresAtUtc);
            Console.WriteLine($"MicaAudio startup pair code: {pairing.Code} (expires: {pairing.ExpiresAtUtc:O})");
        }

        LogStorageRoot(logger, options.StorageRoot);
        started = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!started)
        {
            return;
        }

        await SaveRegistryAsync(cancellationToken).ConfigureAwait(false);
        await panelRuntimeService.StopAsync().ConfigureAwait(false);
        await serverHost.StopAsync().ConfigureAwait(false);
        started = false;
    }

    public async ValueTask DisposeAsync()
    {
        serverHost.DevicesChanged -= OnDevicesChanged;
        serverHost.LogMessage -= OnServerLogMessage;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await panelRuntimeService.DisposeAsync().ConfigureAwait(false);
        await serverHost.DisposeAsync().ConfigureAwait(false);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        lock (saveGate)
        {
            if (saveScheduled)
            {
                return;
            }

            saveScheduled = true;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await SaveRegistryAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                lock (saveGate)
                {
                    saveScheduled = false;
                }
            }
        });
    }

    private async Task SaveRegistryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await registryStore.SaveAsync(serverHost.GetDeviceRecords(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LogRegistrySaveFailed(logger, ex);
        }
    }

    private void OnServerLogMessage(object? sender, string message)
    {
        LogDeviceServerMessage(logger, message);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "MicaAudio startup pair code: {PairCode} expires at {ExpiresAtUtc:O}")]
    private static partial void LogStartupPairCode(ILogger logger, string pairCode, DateTimeOffset expiresAtUtc);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "MicaAudio standalone registry root: {StorageRoot}")]
    private static partial void LogStorageRoot(ILogger logger, string storageRoot);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Device server: {Message}")]
    private static partial void LogDeviceServerMessage(ILogger logger, string message);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Failed to save standalone device registry.")]
    private static partial void LogRegistrySaveFailed(ILogger logger, Exception exception);
}
