using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Integration.Smoke;

public sealed class PanelsDeviceSessionServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldActivatePanelsApp_ForOnlineDevice()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var service = new PanelsDeviceSessionService(coordinator);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static device =>
                string.Equals(device.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && device.Status == DeviceStatus.Online),
            TimeSpan.FromSeconds(3));

        await service.StartAsync("device-1");

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", PanelsDeviceSessionService.PanelsAppId),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task StopAsync_ShouldRestorePreviousApp_WhenPanelsWereLoaded()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var service = new PanelsDeviceSessionService(coordinator);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static device =>
                string.Equals(device.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && device.Status == DeviceStatus.Online),
            TimeSpan.FromSeconds(3));

        await service.StartAsync("device-1");
        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", PanelsDeviceSessionService.PanelsAppId),
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, PanelsDeviceSessionService.PanelsAppId, PanelsDeviceSessionService.PanelsAppName),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static device =>
                string.Equals(device.ActiveAppId, PanelsDeviceSessionService.PanelsAppId, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(3));

        await service.StopAsync();

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", "analogclock"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task StartAsync_ShouldReactivatePanelsAfterReconnect()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var service = new PanelsDeviceSessionService(coordinator);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static device =>
                string.Equals(device.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && device.Status == DeviceStatus.Online),
            TimeSpan.FromSeconds(3));

        await service.StartAsync("device-1");
        await WaitForConditionAsync(
            () => runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId) >= 1,
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Offline, "analogclock", "Relogio"),
        ]);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForConditionAsync(
            () => runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId) >= 2,
            TimeSpan.FromSeconds(3));
    }

    private static DeviceSnapshot CreateSnapshot(string deviceId, DeviceStatus status, string activeAppId, string activeAppName)
    {
        return new DeviceSnapshot
        {
            DeviceId = deviceId,
            Name = deviceId,
            Profile = "dma_exp",
            Status = status,
            IsRegistered = true,
            FirstSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastTelemetryUtc = status == DeviceStatus.Online ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddMinutes(-1),
            ActiveAppId = activeAppId,
            ActiveAppName = activeAppName,
        };
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(30);
        }

        Assert.True(predicate(), "Condition was not satisfied within timeout.");
    }

    private sealed class FakeDeviceOperationsRuntime : IDeviceOperationsRuntime
    {
        private readonly object gate = new();
        private IReadOnlyList<DeviceSnapshot> devices = Array.Empty<DeviceSnapshot>();
        private readonly List<CommandRecord> commands = new();

        public event EventHandler? DevicesChanged;

        public event EventHandler<string>? LogMessage
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
        {
            add { }
            remove { }
        }

        public string GetServerBaseAddress() => "http://127.0.0.1:5272";

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
            => new() { Code = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public bool RemoveDevice(string deviceId)
        {
            lock (gate)
            {
                devices = devices.Where(device => !string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            DevicesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public IReadOnlyList<DeviceSnapshot> GetDevices()
        {
            lock (gate)
            {
                return devices;
            }
        }

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            parameters ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            parameters.TryGetValue("appId", out var appId);
            parameters.TryGetValue("displayName", out var displayName);

            lock (gate)
            {
                commands.Add(new CommandRecord(deviceId, commandType, appId, displayName));
            }

            return Task.FromResult(new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = Guid.NewGuid().ToString("N"),
                Accepted = true,
                Completed = true,
                Success = true,
                ProgressPercent = 100,
                Stage = "done",
                Message = "ok",
            });
        }

        public void SetDevices(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            lock (gate)
            {
                devices = snapshots;
            }

            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool HasActivateCommand(string deviceId, string appId)
        {
            lock (gate)
            {
                return commands.Any(command =>
                    string.Equals(command.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                    && command.CommandType == DeviceCommandType.ActivateApp
                    && string.Equals(command.AppId, appId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public int CountActivateCommands(string deviceId, string appId)
        {
            lock (gate)
            {
                return commands.Count(command =>
                    string.Equals(command.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                    && command.CommandType == DeviceCommandType.ActivateApp
                    && string.Equals(command.AppId, appId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private readonly record struct CommandRecord(
            string DeviceId,
            DeviceCommandType CommandType,
            string? AppId,
            string? DisplayName);
    }
}
