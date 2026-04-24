using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using Microsoft.Extensions.Logging;

namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed partial class EmbeddedDeviceServerClient : IDeviceServerClient, IEmbeddedDeviceServerClientRuntime
{
    private static readonly TimeSpan RegistrySaveMinInterval = TimeSpan.FromSeconds(10);

    private readonly IDeviceServerHost serverHost;
    private readonly IEmbeddedDeviceRegistryStore registryStore;
    private readonly IEmbeddedDeviceServerSettingsProvider settingsProvider;
    private readonly IEmbeddedDevicePublicHostResolver publicHostResolver;
    private readonly ILogger<EmbeddedDeviceServerClient> logger;
    private readonly EmbeddedDeviceServerClientOptions options;
    private readonly object registrySaveGate = new();

    private bool started;
    private string publicHost = "127.0.0.1";
    private DateTimeOffset lastRegistrySaveUtc = DateTimeOffset.MinValue;

    public EmbeddedDeviceServerClient(
        IDeviceServerHost serverHost,
        IEmbeddedDeviceRegistryStore registryStore,
        IEmbeddedDeviceServerSettingsProvider settingsProvider,
        IEmbeddedDevicePublicHostResolver publicHostResolver,
        ILogger<EmbeddedDeviceServerClient> logger,
        EmbeddedDeviceServerClientOptions? options = null)
    {
        this.serverHost = serverHost;
        this.registryStore = registryStore;
        this.settingsProvider = settingsProvider;
        this.publicHostResolver = publicHostResolver;
        this.logger = logger;
        this.options = options ?? new EmbeddedDeviceServerClientOptions();

        serverHost.DevicesChanged += OnDevicesChanged;
        serverHost.LogMessage += OnServerHostLogMessage;
        serverHost.DeviceLogReceived += OnDeviceLogReceived;
    }

    public event EventHandler? DevicesChanged;

    public event EventHandler<string>? LogMessage;

    public event EventHandler<DeviceLogMessage>? DeviceLogReceived;

    public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
    {
        add => serverHost.CommandProgressChanged += value;
        remove => serverHost.CommandProgressChanged -= value;
    }

    public string GetServerBaseAddress() => $"http://{publicHost}:{options.HttpPort}";

    // DOCS: docs/wiki/architecture/05-device-session-and-reconnect.md#ciclo-de-sessao
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (started)
        {
            return;
        }

        var existing = await registryStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        serverHost.SeedDevices(existing);

        publicHost = publicHostResolver.ResolvePublicHost();

        var settings = await settingsProvider.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (settings.AllowLegacyWebSocketQueryToken)
        {
            LogLegacyWebSocketQueryTokenEnabled(logger);
        }

        await serverHost.StartAsync(new ServerConfig
        {
            ListenHost = options.ListenHost,
            Port = options.HttpPort,
            MqttPort = options.MqttPort,
            MaxDevices = options.MaxDevices,
            MdnsServiceName = options.MdnsServiceName,
            PublicHost = publicHost,
            MqttRootTopic = options.MqttRootTopic,
            DeviceFreshThresholdSeconds = settings.DeviceFreshThresholdSeconds,
            AllowLegacyWebSocketQueryToken = settings.AllowLegacyWebSocketQueryToken,
        }, cancellationToken).ConfigureAwait(false);

        var baseAddress = GetServerBaseAddress();
        LogPublicServerBaseAddress(logger, baseAddress);
        LogMessage?.Invoke(this, $"Servidor HTTP publico: {baseAddress}");
        LogMessage?.Invoke(this, $"Broker MQTT publico: mqtt://{publicHost}:{options.MqttPort} ({options.MqttRootTopic}/{{deviceId}})");
        started = true;
    }

    public async Task StopAsync()
    {
        if (!started)
        {
            return;
        }

        await SaveRegistryAsync().ConfigureAwait(false);
        await serverHost.StopAsync().ConfigureAwait(false);
        started = false;
    }

    public PairingCodeInfo CreatePairingCode(TimeSpan ttl) => serverHost.CreatePairingCode(ttl);

    public Task<PairingCodeInfo> CreatePairingCodeAsync(TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.FromResult(CreatePairingCode(ttl));

    public IReadOnlyList<DeviceSnapshot> GetDevices() => serverHost.GetDevicesSnapshot();

    public Task<IReadOnlyList<DeviceSnapshot>> GetDevicesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(GetDevices());

    public async Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default)
        => await serverHost.SendCommandAsync(deviceId, commandType, cancellationToken).ConfigureAwait(false);

    public Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => serverHost.SendCommandTrackedAsync(deviceId, commandType, parameters, timeout, cancellationToken);

    public Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        DeviceCommandSessionContext? sessionContext,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => serverHost.SendCommandTrackedAsync(deviceId, commandType, parameters, sessionContext, timeout, cancellationToken);

    public bool RemoveDevice(string deviceId) => serverHost.RemoveDevice(deviceId);

    public Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(RemoveDevice(deviceId));

    public PanelsBatchRegistration RegisterPanelsBatch(
        string deviceId,
        string panelsSessionId,
        ulong batchSequence,
        byte[] payload,
        int frameCount,
        int durationMs,
        string contentType = "image/webp")
        => serverHost.RegisterPanelsBatch(deviceId, panelsSessionId, batchSequence, payload, frameCount, durationMs, contentType);

    public Task<PanelsBatchRegistration> RegisterPanelsBatchAsync(
        string deviceId,
        string panelsSessionId,
        ulong batchSequence,
        byte[] payload,
        int frameCount,
        int durationMs,
        string contentType = "image/webp",
        CancellationToken cancellationToken = default)
        => Task.FromResult(RegisterPanelsBatch(deviceId, panelsSessionId, batchSequence, payload, frameCount, durationMs, contentType));

    public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
        => serverHost.ClearPanelsBatches(deviceId, panelsSessionId);

    public Task ClearPanelsBatchesAsync(string deviceId, string? panelsSessionId = null, CancellationToken cancellationToken = default)
    {
        ClearPanelsBatches(deviceId, panelsSessionId);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        serverHost.DevicesChanged -= OnDevicesChanged;
        serverHost.LogMessage -= OnServerHostLogMessage;
        serverHost.DeviceLogReceived -= OnDeviceLogReceived;
        await StopAsync().ConfigureAwait(false);
        await serverHost.DisposeAsync().ConfigureAwait(false);
    }

    private void OnServerHostLogMessage(object? sender, string message)
    {
        LogServerHostMessage(logger, message);
        LogMessage?.Invoke(this, message);
    }

    private void OnDeviceLogReceived(object? sender, DeviceLogMessage log)
    {
        DeviceLogReceived?.Invoke(this, log);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);

        var shouldSave = false;
        lock (registrySaveGate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - lastRegistrySaveUtc >= RegistrySaveMinInterval)
            {
                shouldSave = true;
                lastRegistrySaveUtc = now;
            }
        }

        if (shouldSave)
        {
            _ = SaveRegistryAsync();
        }
    }

    private async Task SaveRegistryAsync()
    {
        try
        {
            await registryStore.SaveAsync(serverHost.GetDeviceRecords()).ConfigureAwait(false);
            lock (registrySaveGate)
            {
                lastRegistrySaveUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            LogRegistrySaveFailed(logger, ex);
            LogMessage?.Invoke(this, $"Falha ao salvar devices.json: {ex.Message}");
        }
    }

    [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "Device server log: {Message}")]
    private static partial void LogServerHostMessage(ILogger logger, string message);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning, Message = "Modo legado de autenticacao WS via query-string esta ATIVO por settings.json.")]
    private static partial void LogLegacyWebSocketQueryTokenEnabled(ILogger logger);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "Servidor HTTP publico: {BaseAddress}")]
    private static partial void LogPublicServerBaseAddress(ILogger logger, string baseAddress);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Error, Message = "Falha ao salvar devices.json")]
    private static partial void LogRegistrySaveFailed(ILogger logger, Exception exception);
}
