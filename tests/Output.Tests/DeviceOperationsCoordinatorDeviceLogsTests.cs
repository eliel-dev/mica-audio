using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceOperationsCoordinatorDeviceLogsTests
{
    [Fact]
    public async Task PerDeviceLogs_ShouldRecordOnlineTransition()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);

        runtime.SetDevices([CreateSnapshot("device-1", DeviceStatus.Offline, lastTelemetryUtc: null)]);
        await WaitForConditionAsync(() =>
            coordinator.GetStateSnapshot().DeviceListSnapshot.Any(static d => string.Equals(d.DeviceId, "device-1", StringComparison.OrdinalIgnoreCase)
                && d.Status == DeviceStatus.Offline), TimeSpan.FromSeconds(3));

        runtime.SetDevices([CreateSnapshot("device-1", DeviceStatus.Online, lastTelemetryUtc: DateTimeOffset.UtcNow)]);

        await WaitForConditionAsync(() =>
            coordinator.GetDeviceLogs("device-1").Any(line => line.Contains("Primeira telemetria recebida apos reconexao.", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(3));

        var logs = coordinator.GetDeviceLogs("device-1");
        Assert.Contains(logs, line => line.Contains("Dispositivo autenticado e online.", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("Dispositivo voltou a aparecer apos ficar offline.", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("Primeira telemetria recebida apos reconexao.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PerDeviceLogs_ShouldRecordCommandLifecycle()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        runtime.SetDevices([CreateSnapshot("device-cmd", DeviceStatus.Online, DateTimeOffset.UtcNow)]);
        runtime.SendCommandHandler = async (deviceId, commandType, _, _, _) =>
        {
            runtime.EmitCommandProgress(new DeviceCommandProgressMessage
            {
                DeviceId = deviceId,
                CommandId = "cmd-lifecycle",
                ProgressPercent = 25,
                Stage = "received",
                Message = "Comando recebido no device.",
            });

            await Task.Delay(10).ConfigureAwait(false);

            runtime.EmitCommandProgress(new DeviceCommandProgressMessage
            {
                DeviceId = deviceId,
                CommandId = "cmd-lifecycle",
                ProgressPercent = 80,
                Stage = "running",
                Message = "Executando etapa principal.",
            });

            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = "cmd-lifecycle",
                Accepted = true,
                Completed = true,
                Success = true,
                ProgressPercent = 100,
                Stage = "done",
                Message = "Concluido com sucesso.",
            };
        };

        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        await coordinator.RunCommandAsync("device-cmd", DeviceCommandType.TestLed);

        var logs = coordinator.GetDeviceLogs("device-cmd");
        Assert.Contains(logs, line => line.Contains("Comando iniciado (testar LED).", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("Comando recebido no device.", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("Executando etapa principal.", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("Comando concluido com sucesso", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PerDeviceLogs_ShouldReturnOnlySelectedDeviceEntries()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        runtime.SetDevices([
            CreateSnapshot("device-a", DeviceStatus.Online, DateTimeOffset.UtcNow),
            CreateSnapshot("device-b", DeviceStatus.Online, DateTimeOffset.UtcNow),
        ]);
        runtime.SendCommandHandler = (deviceId, commandType, _, _, _) =>
        {
            var marker = string.Equals(deviceId, "device-a", StringComparison.OrdinalIgnoreCase)
                ? "marker-A"
                : "marker-B";

            runtime.EmitCommandProgress(new DeviceCommandProgressMessage
            {
                DeviceId = deviceId,
                CommandId = $"cmd-{deviceId}",
                ProgressPercent = 50,
                Stage = "running",
                Message = marker,
            });

            return Task.FromResult(new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = $"cmd-{deviceId}",
                Accepted = true,
                Completed = true,
                Success = true,
                ProgressPercent = 100,
                Stage = "done",
                Message = marker,
            });
        };

        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);

        await coordinator.RunCommandAsync("device-a", DeviceCommandType.TestLed);
        await coordinator.RunCommandAsync("device-b", DeviceCommandType.RevokeAndRestart);

        var logsA = coordinator.GetDeviceLogs("device-a");
        var logsB = coordinator.GetDeviceLogs("device-b");

        Assert.Contains(logsA, line => line.Contains("marker-A", StringComparison.Ordinal));
        Assert.DoesNotContain(logsA, line => line.Contains("marker-B", StringComparison.Ordinal));
        Assert.Contains(logsB, line => line.Contains("marker-B", StringComparison.Ordinal));
        Assert.DoesNotContain(logsB, line => line.Contains("marker-A", StringComparison.Ordinal));
    }

    private static DeviceSnapshot CreateSnapshot(string deviceId, DeviceStatus status, DateTimeOffset? lastTelemetryUtc)
    {
        return new DeviceSnapshot
        {
            DeviceId = deviceId,
            Name = deviceId,
            Profile = "dma_exp",
            Status = status,
            IsRegistered = true,
            FirstSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-20),
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastTelemetryUtc = lastTelemetryUtc,
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
        private IReadOnlyList<DeviceSnapshot> devices = Array.Empty<DeviceSnapshot>();

        public event EventHandler? DevicesChanged;

        public event EventHandler<string>? LogMessage;

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

        public Func<string, DeviceCommandType, IReadOnlyDictionary<string, string>?, TimeSpan, CancellationToken, Task<CommandDispatchResult>>? SendCommandHandler { get; set; }

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
            if (SendCommandHandler is not null)
            {
                return SendCommandHandler(deviceId, commandType, parameters, timeout, cancellationToken);
            }

            return Task.FromResult(new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = "cmd-default",
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

        public void EmitCommandProgress(DeviceCommandProgressMessage progress)
        {
            CommandProgressChanged?.Invoke(this, progress);
        }

        public void EmitLog(string message)
        {
            LogMessage?.Invoke(this, message);
        }
    }
}
