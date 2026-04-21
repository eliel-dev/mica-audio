using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
internal sealed partial class DeviceIntegrationService : IAsyncDisposable
{
    private readonly IDeviceServerHost serverHost;
    private readonly IDeviceRegistryStore registryStore;
    private readonly SettingsRepository settingsRepository;
    private readonly AppSettingsDomainService settingsDomainService;
    private readonly ILogger<DeviceIntegrationService> logger;

    private const int ServerPort = 5272;
    private const int ServerMqttPort = 5273;
    private const string ServerMqttRootTopic = "mica/v1/devices";
    private static readonly TimeSpan RegistrySaveMinInterval = TimeSpan.FromSeconds(10);

    private readonly object registrySaveGate = new();

    private bool started;
    private string publicHost = "127.0.0.1";
    private DateTimeOffset lastRegistrySaveUtc = DateTimeOffset.MinValue;

    public DeviceIntegrationService(
        IDeviceServerHost serverHost,
        IDeviceRegistryStore registryStore,
        SettingsRepository settingsRepository,
        AppSettingsDomainService settingsDomainService,
        ILogger<DeviceIntegrationService> logger)
    {
        this.serverHost = serverHost;
        this.registryStore = registryStore;
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;
        this.logger = logger;

        serverHost.DevicesChanged += OnDevicesChanged;
        serverHost.LogMessage += OnServerHostLogMessage;
        serverHost.DeviceLogReceived += OnDeviceLogReceived;
    }

    public IDeviceServerHost Host => serverHost;

    public event EventHandler? DevicesChanged;

    public event EventHandler<string>? LogMessage;

    public event EventHandler<DeviceLogMessage>? DeviceLogReceived;

    public string GetServerBaseAddress() => $"http://{publicHost}:{ServerPort}";

    // DOCS: docs/wiki/architecture/05-device-session-and-reconnect.md#ciclo-de-sessao
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (started)
        {
            return;
        }

        var existing = await registryStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        serverHost.SeedDevices(existing);

        publicHost = ResolvePublicHost();

        var settings = settingsDomainService.Migrate(await settingsRepository.LoadAsync(cancellationToken).ConfigureAwait(false));
        if (settings.AllowLegacyWebSocketQueryToken)
        {
            LogLegacyWebSocketQueryTokenEnabled(logger);
        }

        await serverHost.StartAsync(new ServerConfig
        {
            ListenHost = "0.0.0.0",
            Port = ServerPort,
            MqttPort = ServerMqttPort,
            MaxDevices = 5,
            MdnsServiceName = "_micaaudio._tcp",
            PublicHost = publicHost,
            MqttRootTopic = ServerMqttRootTopic,
            DeviceFreshThresholdSeconds = settings.DeviceFreshThresholdSeconds,
            AllowLegacyWebSocketQueryToken = settings.AllowLegacyWebSocketQueryToken,
        }, cancellationToken).ConfigureAwait(false);

        var baseAddress = GetServerBaseAddress();
        LogPublicServerBaseAddress(logger, baseAddress);
        LogMessage?.Invoke(this, $"Servidor HTTP publico: {baseAddress}");
        LogMessage?.Invoke(this, $"Broker MQTT publico: mqtt://{publicHost}:{ServerMqttPort} ({ServerMqttRootTopic}/{{deviceId}})");
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

    public IReadOnlyList<DeviceSnapshot> GetDevices() => serverHost.GetDevicesSnapshot();

    public async Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default)
        => await serverHost.SendCommandAsync(deviceId, commandType, cancellationToken).ConfigureAwait(false);

    public bool RemoveDevice(string deviceId) => serverHost.RemoveDevice(deviceId);

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

    private static readonly string[] VirtualAdapterKeywords =
    {
        "virtual",
        "vethernet",
        "hyper-v",
        "docker",
        "wsl",
        "vmware",
        "virtualbox",
        "loopback",
        "tunnel",
        "tap"
    };

    private static string ResolvePublicHost()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            var candidates = GetIpv4Candidates(interfaces.Where(IsPreferredPhysicalAdapter));
            if (candidates.Length == 0)
            {
                candidates = GetIpv4Candidates(interfaces.Where(IsUsableAdapter).Where(nic => !IsLikelyVirtualAdapter(nic)));
            }

            if (candidates.Length == 0)
            {
                return "127.0.0.1";
            }

            var privateIp = candidates.FirstOrDefault(IsPrivateIpv4);
            return privateIp ?? candidates[0];
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static string[] GetIpv4Candidates(IEnumerable<NetworkInterface> interfaces)
    {
        return interfaces
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Where(info => info.Address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(info.Address))
            .Select(info => info.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsPreferredPhysicalAdapter(NetworkInterface nic)
    {
        if (!IsUsableAdapter(nic) || IsLikelyVirtualAdapter(nic))
        {
            return false;
        }

        return nic.NetworkInterfaceType is NetworkInterfaceType.Wireless80211
            or NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.FastEthernetT;
    }

    private static bool IsUsableAdapter(NetworkInterface nic)
    {
        return nic.OperationalStatus == OperationalStatus.Up
            && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback
            && nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel;
    }

    private static bool IsLikelyVirtualAdapter(NetworkInterface nic)
    {
        var descriptor = $"{nic.Name} {nic.Description}";
        return VirtualAdapterKeywords.Any(keyword => descriptor.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivateIpv4(string candidate)
    {
        if (!IPAddress.TryParse(candidate, out var address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
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
