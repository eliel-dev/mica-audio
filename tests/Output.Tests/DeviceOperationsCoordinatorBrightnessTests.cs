using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceOperationsCoordinatorBrightnessTests
{
    [Fact]
    public async Task SetBrightnessAsync_ShouldClampAndSendSetBrightnessCommand()
    {
        var runtime = new CaptureRuntime();
        runtime.SetDevices([CreateOnlineSnapshot("device-1")]);

        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        var result = await coordinator.SetBrightnessAsync("device-1", 999);

        Assert.True(result.Success);
        Assert.Equal("device-1", runtime.LastDeviceId);
        Assert.Equal(DeviceCommandType.SetBrightness, runtime.LastCommandType);
        Assert.NotNull(runtime.LastParameters);
        Assert.Equal("160", runtime.LastParameters!["brightness"]);
    }

    [Fact]
    public async Task SetTestLedEnabledAsync_ShouldSendToggleParameter()
    {
        var runtime = new CaptureRuntime();
        runtime.SetDevices([CreateOnlineSnapshot("device-1")]);

        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        var result = await coordinator.SetTestLedEnabledAsync("device-1", enabled: true);

        Assert.True(result.Success);
        Assert.Equal(DeviceCommandType.TestLed, runtime.LastCommandType);
        Assert.NotNull(runtime.LastParameters);
        Assert.Equal("true", runtime.LastParameters!["enabled"]);
    }

    [Fact]
    public async Task TriggerTestLedAsync_ShouldSendCommandWithoutParameters()
    {
        var runtime = new CaptureRuntime();
        runtime.SetDevices([CreateOnlineSnapshot("device-1")]);

        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        var result = await coordinator.TriggerTestLedAsync("device-1");

        Assert.True(result.Success);
        Assert.Equal("device-1", runtime.LastDeviceId);
        Assert.Equal(DeviceCommandType.TestLed, runtime.LastCommandType);
        Assert.Null(runtime.LastParameters);
    }

    [Fact]
    public async Task ActivateAppAsync_ShouldEmitDeviceCommandActivity()
    {
        var runtime = new CaptureRuntime();
        runtime.SetDevices([CreateOnlineSnapshot("device-1")]);
        using var capture = new ActivityCapture(AppObservability.ActivitySourceName);

        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        var result = await coordinator.ActivateAppAsync("device-1", "weather-app", "Clima");

        Assert.True(result.Success);
        var activity = Assert.Single(capture.CompletedActivities, item => item.OperationName == "device.command");
        Assert.Equal(AppObservability.DeviceIntegrationComponent, activity.GetTagItem(AppObservability.ComponentKey));
        Assert.Equal("device-1", activity.GetTagItem(AppObservability.DeviceIdKey));
        Assert.Equal("weather-app", activity.GetTagItem(AppObservability.AppIdKey));
        Assert.Equal("cmd-capture", activity.GetTagItem(AppObservability.CommandIdKey));
    }

    private static DeviceSnapshot CreateOnlineSnapshot(string deviceId)
    {
        return new DeviceSnapshot
        {
            DeviceId = deviceId,
            Name = deviceId,
            Profile = "dma_exp",
            Status = DeviceStatus.Online,
            IsRegistered = true,
            FirstSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastTelemetryUtc = DateTimeOffset.UtcNow,
        };
    }

    private sealed class CaptureRuntime : IDeviceOperationsRuntime
    {
        private IReadOnlyList<DeviceSnapshot> devices = Array.Empty<DeviceSnapshot>();

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

        public string? LastDeviceId { get; private set; }
        public DeviceCommandType? LastCommandType { get; private set; }
        public Dictionary<string, string>? LastParameters { get; private set; }

        public string GetServerBaseAddress() => "http://127.0.0.1:5272";

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
            => new() { Code = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public bool RemoveDevice(string deviceId)
        {
            devices = devices.Where(d => !string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            DevicesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public IReadOnlyList<DeviceSnapshot> GetDevices() => devices;

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastDeviceId = deviceId;
            LastCommandType = commandType;
            LastParameters = parameters is null
                ? null
                : new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = "cmd-capture",
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
            devices = snapshots;
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
