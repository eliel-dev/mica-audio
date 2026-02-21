using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
internal sealed class DeviceIntegrationService : IAsyncDisposable
{
    private readonly IDeviceServerHost serverHost;
    private readonly IDeviceRegistryStore registryStore;

    private const int ServerPort = 5272;
    private static readonly TimeSpan RegistrySaveMinInterval = TimeSpan.FromSeconds(10);

    private readonly object registrySaveGate = new();

    private bool started;
    private string publicHost = "127.0.0.1";
    private DateTimeOffset lastRegistrySaveUtc = DateTimeOffset.MinValue;

    public DeviceIntegrationService(IDeviceServerHost serverHost, IDeviceRegistryStore registryStore)
    {
        this.serverHost = serverHost;
        this.registryStore = registryStore;

        serverHost.DevicesChanged += OnDevicesChanged;
        serverHost.LogMessage += (_, msg) => LogMessage?.Invoke(this, msg);
    }

    public IDeviceServerHost Host => serverHost;

    public event EventHandler? DevicesChanged;

    public event EventHandler<string>? LogMessage;

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

        await serverHost.StartAsync(new ServerConfig
        {
            ListenHost = "0.0.0.0",
            Port = ServerPort,
            MaxDevices = 5,
            MdnsServiceName = "_micaaudio._tcp",
            PublicHost = publicHost,
        }, cancellationToken).ConfigureAwait(false);

        LogMessage?.Invoke(this, $"Servidor HTTP publico: {GetServerBaseAddress()}");
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
        await StopAsync().ConfigureAwait(false);
        await serverHost.DisposeAsync().ConfigureAwait(false);
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
            LogMessage?.Invoke(this, $"Falha ao salvar devices.json: {ex.Message}");
        }
    }

    private static string ResolvePublicHost()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                    && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Where(info => info.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(info.Address))
                .Select(info => info.Address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

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

    private static bool IsPrivateIpv4(string ipString)
    {
        if (!IPAddress.TryParse(ipString, out var address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
