using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class Hub75VisualizerSessionServiceTests
{
    [Fact]
    public async Task Enable_ShouldActivateVisualizer_ForOnlineDevice()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var service = new Hub75VisualizerSessionService(coordinator);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static d =>
                string.Equals(d.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && d.Status == DeviceStatus.Online),
            TimeSpan.FromSeconds(3));

        await service.SetHub75ModeAsync(enabled: true);

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", Hub75VisualizerSessionService.VisualizerAppId),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Disable_ShouldRestorePreviousApp_WhenDeviceIsOnline()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var service = new Hub75VisualizerSessionService(coordinator);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static d =>
                string.Equals(d.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && d.Status == DeviceStatus.Online),
            TimeSpan.FromSeconds(3));

        await service.SetHub75ModeAsync(enabled: true);

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", Hub75VisualizerSessionService.VisualizerAppId),
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, Hub75VisualizerSessionService.VisualizerAppId, Hub75VisualizerSessionService.VisualizerAppName),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static d =>
                string.Equals(d.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.ActiveAppId, Hub75VisualizerSessionService.VisualizerAppId, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(3));

        await service.SetHub75ModeAsync(enabled: false);

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", "analogclock"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Disable_ShouldRestoreAfterReconnect_WhenDeviceReturnsOnline()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var service = new Hub75VisualizerSessionService(coordinator);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static d =>
                string.Equals(d.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && d.Status == DeviceStatus.Online),
            TimeSpan.FromSeconds(3));

        await service.SetHub75ModeAsync(enabled: true);

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", Hub75VisualizerSessionService.VisualizerAppId),
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, Hub75VisualizerSessionService.VisualizerAppId, Hub75VisualizerSessionService.VisualizerAppName),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static d =>
                string.Equals(d.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.ActiveAppId, Hub75VisualizerSessionService.VisualizerAppId, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Offline, Hub75VisualizerSessionService.VisualizerAppId, Hub75VisualizerSessionService.VisualizerAppName),
        ]);

        await service.SetHub75ModeAsync(enabled: false);

        Assert.False(runtime.HasActivateCommand("device-1", "analogclock"));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, Hub75VisualizerSessionService.VisualizerAppId, Hub75VisualizerSessionService.VisualizerAppName),
        ]);

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", "analogclock"),
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

            await Task.Delay(30).ConfigureAwait(false);
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
                devices = devices
                    .Where(d => !string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
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
                devices = snapshots.ToArray();
            }

            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool HasActivateCommand(string deviceId, string appId)
        {
            lock (gate)
            {
                return commands.Any(command =>
                    command.CommandType == DeviceCommandType.ActivateApp
                    && string.Equals(command.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(command.AppId, appId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private sealed record CommandRecord(string DeviceId, DeviceCommandType CommandType, string? AppId, string? DisplayName);
    }
}
