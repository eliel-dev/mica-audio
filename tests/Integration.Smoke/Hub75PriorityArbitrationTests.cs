using App.WinUI.Models.Panels;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Panels;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Integration.Smoke;

public sealed class Hub75PriorityArbitrationTests
{
    [Fact]
    public async Task SuppressAsync_ShouldPreventPanelsReactivationUntilResumeAsync()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var service = new PanelsDeviceSessionService(coordinator);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForDeviceAsync(coordinator, "device-1");

        await service.StartAsync("device-1");
        await WaitForConditionAsync(
            () => runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId) >= 1,
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, PanelsDeviceSessionService.PanelsAppId, PanelsDeviceSessionService.PanelsAppName),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static device =>
                string.Equals(device.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(device.ActiveAppId, PanelsDeviceSessionService.PanelsAppId, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(3));

        await service.SuppressAsync();

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Offline, PanelsDeviceSessionService.PanelsAppId, PanelsDeviceSessionService.PanelsAppName),
        ]);
        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        Assert.True(service.IsSuppressed);
        Assert.Equal(1, runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId));

        await service.ResumeAsync();

        await WaitForConditionAsync(
            () => runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId) >= 2,
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task StartAsync_WhenVisualizerHub75IsActive_ShouldNotDispatchPanelsCommand()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var visualizerSessionService = new Hub75VisualizerSessionService(coordinator);
        using var panelsDeviceSessionService = new PanelsDeviceSessionService(coordinator);
        using var playbackService = CreatePlaybackService(panelsDeviceSessionService, visualizerSessionService);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForDeviceAsync(coordinator, "device-1");

        await visualizerSessionService.SetHub75ModeAsync(enabled: true);

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", Hub75VisualizerSessionService.VisualizerAppId),
            TimeSpan.FromSeconds(3));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            playbackService.StartAsync(CreatePanel("panel-1"), "device-1"));

        Assert.Contains("prioridade", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId));
    }

    [Fact]
    public async Task DisableVisualizerAndResumeSuspendedPanel_ShouldRestoreSamePanelOnSameDevice()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var visualizerSessionService = new Hub75VisualizerSessionService(coordinator);
        using var panelsDeviceSessionService = new PanelsDeviceSessionService(coordinator);
        using var playbackService = CreatePlaybackService(panelsDeviceSessionService, visualizerSessionService);
        var panel = CreatePanel("panel-1");

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForDeviceAsync(coordinator, "device-1");

        await playbackService.StartAsync(panel, "device-1");

        await WaitForConditionAsync(
            () => runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId) >= 1,
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, PanelsDeviceSessionService.PanelsAppId, PanelsDeviceSessionService.PanelsAppName),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static device =>
                string.Equals(device.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(device.ActiveAppId, PanelsDeviceSessionService.PanelsAppId, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(3));

        await playbackService.SuspendAsync();

        Assert.False(playbackService.IsRunning);
        Assert.Null(playbackService.GetActivePanelSnapshot());
        Assert.NotNull(playbackService.GetSuspendedPanelSnapshot());

        await visualizerSessionService.SetHub75ModeAsync(enabled: true);

        await WaitForConditionAsync(
            () => runtime.HasActivateCommand("device-1", Hub75VisualizerSessionService.VisualizerAppId),
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, Hub75VisualizerSessionService.VisualizerAppId, Hub75VisualizerSessionService.VisualizerAppName),
        ]);

        var panelsCommandCountBeforeResume = runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId);

        await visualizerSessionService.SetHub75ModeAsync(enabled: false);
        await playbackService.ResumeSuspendedAsync();

        await WaitForConditionAsync(
            () => runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId) > panelsCommandCountBeforeResume,
            TimeSpan.FromSeconds(3));

        Assert.Equal("device-1", playbackService.TargetDeviceId);
        Assert.Equal(panel.PanelId, playbackService.GetActivePanelSnapshot()?.PanelId);
        Assert.Null(playbackService.GetSuspendedPanelSnapshot());
    }

    [Fact]
    public async Task StopAsync_WhilePanelIsSuspended_ShouldDiscardResumeState()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var visualizerSessionService = new Hub75VisualizerSessionService(coordinator);
        using var panelsDeviceSessionService = new PanelsDeviceSessionService(coordinator);
        using var playbackService = CreatePlaybackService(panelsDeviceSessionService, visualizerSessionService);

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForDeviceAsync(coordinator, "device-1");

        await playbackService.StartAsync(CreatePanel("panel-1"), "device-1");

        await WaitForConditionAsync(
            () => runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId) >= 1,
            TimeSpan.FromSeconds(3));

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, PanelsDeviceSessionService.PanelsAppId, PanelsDeviceSessionService.PanelsAppName),
        ]);

        await WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static device =>
                string.Equals(device.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(device.ActiveAppId, PanelsDeviceSessionService.PanelsAppId, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(3));

        await playbackService.SuspendAsync();
        Assert.NotNull(playbackService.GetSuspendedPanelSnapshot());

        await playbackService.StopAsync();

        var panelsCommandCount = runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId);
        Assert.Null(playbackService.GetSuspendedPanelSnapshot());

        await playbackService.ResumeSuspendedAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(1300));

        Assert.Equal(panelsCommandCount, runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId));
        Assert.Null(playbackService.GetActivePanelSnapshot());
        Assert.Null(playbackService.TargetDeviceId);
    }

    [Fact]
    public async Task StartAsync_InLocalOnlyMode_ShouldKeepPreviewStateWithoutTakingOverDevice()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        using var visualizerSessionService = new Hub75VisualizerSessionService(coordinator);
        using var panelsDeviceSessionService = new PanelsDeviceSessionService(coordinator);
        using var playbackService = CreatePlaybackService(panelsDeviceSessionService, visualizerSessionService, enableMatrixTransport: false);
        var panel = CreatePanel("panel-local");

        runtime.SetDevices([
            CreateSnapshot("device-1", DeviceStatus.Online, "analogclock", "Relogio"),
        ]);

        await WaitForDeviceAsync(coordinator, "device-1");
        await playbackService.StartAsync(panel, "device-1");

        await WaitForConditionAsync(() => playbackService.IsRunning, TimeSpan.FromSeconds(3));

        Assert.Equal(0, runtime.CountActivateCommands("device-1", PanelsDeviceSessionService.PanelsAppId));
        Assert.Equal(panel.PanelId, playbackService.GetActivePanelSnapshot()?.PanelId);
        Assert.Equal("device-1", playbackService.TargetDeviceId);

        await playbackService.StopAsync();
    }

    private static PanelsPlaybackService CreatePlaybackService(
        PanelsDeviceSessionService panelsDeviceSessionService,
        Hub75VisualizerSessionService visualizerSessionService,
        bool enableMatrixTransport = true)
    {
        return new PanelsPlaybackService(
            new FakeDeviceServerClient(),
            new FakeDeviceFrameTransport(),
            new PanelsFrameComposer(),
            panelsDeviceSessionService,
            visualizerSessionService,
            enableMatrixTransport);
    }

    private static PanelDefinition CreatePanel(string panelId)
    {
        return new PanelDefinition
        {
            PanelId = panelId,
            Name = $"Painel {panelId}",
        };
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

    private static Task WaitForDeviceAsync(DeviceOperationsCoordinator coordinator, string deviceId)
    {
        return WaitForConditionAsync(
            () => coordinator.GetStateSnapshot().DeviceListSnapshot.Any(device =>
                string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                && device.Status == DeviceStatus.Online),
            TimeSpan.FromSeconds(3));
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

    private sealed class FakeDeviceServerClient : IDeviceServerClient
    {
        public event EventHandler? DevicesChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? LogMessage
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived
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

        public IReadOnlyList<DeviceSnapshot> GetDevices() => Array.Empty<DeviceSnapshot>();

        public bool RemoveDevice(string deviceId) => false;

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
            => Task.FromResult(new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = "cmd-fake",
                Accepted = true,
                Completed = true,
                Success = true,
                ProgressPercent = 100,
                Stage = "done",
                Message = "ok",
            });

        public PanelsBatchRegistration RegisterPanelsBatch(
            string deviceId,
            string panelsSessionId,
            ulong batchSequence,
            byte[] payload,
            int frameCount,
            int durationMs,
            string contentType = "image/webp")
            => new(panelsSessionId, batchSequence, payload.LongLength, string.Empty, contentType, frameCount, durationMs, string.Empty);

        public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
        {
        }
    }

    private sealed class FakeDeviceFrameTransport : IDeviceFrameTransport
    {
        public void SendFrame(string deviceId, byte[] framePayload)
        {
        }

        public void BroadcastFrame(byte[] framePayload)
        {
        }
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

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived
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
                    .Where(device => !string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
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

        public int CountActivateCommands(string deviceId, string appId)
        {
            lock (gate)
            {
                return commands.Count(command =>
                    command.CommandType == DeviceCommandType.ActivateApp
                    && string.Equals(command.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(command.AppId, appId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private sealed record CommandRecord(
            string DeviceId,
            DeviceCommandType CommandType,
            string? AppId,
            string? DisplayName);
    }
}
