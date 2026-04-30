using Device.Client;
using Device.Client.Embedded;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Output.Tests;

public sealed class EmbeddedDeviceServerClientTests
{
    [Fact]
    public async Task StartAsync_ShouldUseEmbeddedDefaultsAndSeedRegistry()
    {
        var seededRecord = CreateRecord("device-1");
        await using var host = new FakeDeviceServerHost();
        var registry = new FakeEmbeddedDeviceRegistryStore([seededRecord]);
        var settings = new FakeEmbeddedDeviceServerSettingsProvider(new EmbeddedDeviceServerSettings
        {
            AllowLegacyWebSocketQueryToken = true,
            DeviceFreshThresholdSeconds = 42,
        });
        await using var client = CreateClient(host, registry, settings, "192.168.15.20");

        await client.StartAsync();

        Assert.Single(host.SeededDevices);
        Assert.Equal("device-1", host.SeededDevices[0].DeviceId);
        Assert.NotNull(host.LastStartConfig);
        Assert.Equal("0.0.0.0", host.LastStartConfig!.ListenHost);
        Assert.Equal(5272, host.LastStartConfig.Port);
        Assert.Equal(5273, host.LastStartConfig.MqttPort);
        Assert.Equal(5, host.LastStartConfig.MaxDevices);
        Assert.Equal("_micaaudio._tcp", host.LastStartConfig.MdnsServiceName);
        Assert.Equal("192.168.15.20", host.LastStartConfig.PublicHost);
        Assert.Equal("mica/v1/devices", host.LastStartConfig.MqttRootTopic);
        Assert.Equal(42, host.LastStartConfig.DeviceFreshThresholdSeconds);
        Assert.True(host.LastStartConfig.AllowLegacyWebSocketQueryToken);
        Assert.Equal("http://192.168.15.20:5272", client.GetServerBaseAddress());
    }

    [Fact]
    public async Task StartAsync_ShouldDisableLegacyWsQueryToken_ByDefault()
    {
        await using var host = new FakeDeviceServerHost();
        await using var client = CreateClient(host, new FakeEmbeddedDeviceRegistryStore([]));

        await client.StartAsync();

        Assert.NotNull(host.LastStartConfig);
        Assert.False(host.LastStartConfig!.AllowLegacyWebSocketQueryToken);
    }

    [Fact]
    public async Task StopAsync_ShouldSaveDeviceRecordsAndStopHost()
    {
        var savedRecord = CreateRecord("device-stop");
        await using var host = new FakeDeviceServerHost
        {
            DeviceRecords = [savedRecord],
        };
        var registry = new FakeEmbeddedDeviceRegistryStore([]);
        await using var client = CreateClient(host, registry);

        await client.StartAsync();
        await client.StopAsync();

        Assert.Equal(1, host.StopCallCount);
        Assert.Single(registry.SavedDevices);
        Assert.Equal("device-stop", registry.SavedDevices[0].DeviceId);
    }

    [Fact]
    public async Task ClientMethods_ShouldForwardToEmbeddedHost()
    {
        var snapshot = new DeviceSnapshot { DeviceId = "device-forward", Name = "Forward" };
        var commandResult = new CommandDispatchResult
        {
            DeviceId = "device-forward",
            CommandId = "cmd-1",
            Accepted = true,
            Completed = true,
            Success = true,
            ProgressPercent = 100,
            Stage = "done",
        };
        var batch = new PanelsBatchRegistration(
            "session-1",
            BatchSequence: 7,
            FileSizeBytes: 3,
            Sha256: "abc",
            ContentType: "image/webp",
            FrameCount: 30,
            DurationMs: 1000,
            DownloadUrl: "http://127.0.0.1:5272/batch.webp");
        await using var host = new FakeDeviceServerHost
        {
            Snapshots = [snapshot],
            CommandResult = commandResult,
            RemoveDeviceResult = true,
            BatchRegistration = batch,
        };
        await using var client = CreateClient(host, new FakeEmbeddedDeviceRegistryStore([]));

        Assert.Same(snapshot, Assert.Single(client.GetDevices()));
        Assert.True(client.RemoveDevice("device-forward"));
        Assert.Same(commandResult, await client.SendCommandTrackedAsync(
            "device-forward",
            DeviceCommandType.TestLed,
            new Dictionary<string, string> { ["enabled"] = "true" },
            TimeSpan.FromSeconds(5),
            CancellationToken.None));
        Assert.Same(batch, client.RegisterPanelsBatch(
            "device-forward",
            "session-1",
            7,
            [1, 2, 3],
            30,
            1000));

        Assert.Equal("device-forward", host.LastRemovedDeviceId);
        Assert.Equal(DeviceCommandType.TestLed, host.LastTrackedCommandType);
        Assert.Equal("true", host.LastTrackedParameters!["enabled"]);
        Assert.Equal("device-forward", host.LastBatchDeviceId);
    }

    [Fact]
    public async Task SendCommandTrackedAsync_WithSessionContext_ShouldForwardContextToEmbeddedHost()
    {
        var commandResult = new CommandDispatchResult
        {
            DeviceId = "device-session",
            CommandId = "cmd-session",
            Accepted = true,
            Completed = true,
            Success = true,
            ProgressPercent = 100,
            Stage = "done",
        };
        await using var host = new FakeDeviceServerHost
        {
            CommandResult = commandResult,
        };
        await using var client = CreateClient(host, new FakeEmbeddedDeviceRegistryStore([]));

        var result = await client.SendCommandTrackedAsync(
            "device-session",
            DeviceCommandType.SetBrightness,
            new Dictionary<string, string> { ["brightness"] = "110" },
            new DeviceCommandSessionContext("win-eliel-1234abcd", 9, "lock-embedded"),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Same(commandResult, result);
        Assert.Equal(DeviceCommandType.SetBrightness, host.LastTrackedCommandType);
        Assert.NotNull(host.LastTrackedSessionContext);
        Assert.Equal("win-eliel-1234abcd", host.LastTrackedSessionContext!.ClientId);
        Assert.Equal((uint)9, host.LastTrackedSessionContext.OwnerEpoch);
        Assert.Equal("lock-embedded", host.LastTrackedSessionContext.LockToken);
    }

    [Fact]
    public async Task HostEvents_ShouldBeForwardedThroughClient()
    {
        await using var host = new FakeDeviceServerHost();
        await using var client = CreateClient(host, new FakeEmbeddedDeviceRegistryStore([]));
        var devicesChangedCount = 0;
        var serverLog = string.Empty;
        DeviceLogMessage? deviceLog = null;
        DeviceCommandProgressMessage? progress = null;

        client.DevicesChanged += (_, _) => devicesChangedCount++;
        client.LogMessage += (_, message) => serverLog = message;
        client.DeviceLogReceived += (_, message) => deviceLog = message;
        client.CommandProgressChanged += (_, message) => progress = message;

        host.RaiseDevicesChanged();
        host.RaiseLogMessage("server-log");
        host.RaiseDeviceLog(new DeviceLogMessage { DeviceId = "device-event", Message = "device-log" });
        host.RaiseCommandProgress(new DeviceCommandProgressMessage { DeviceId = "device-event", Stage = "done" });

        Assert.Equal(1, devicesChangedCount);
        Assert.Equal("server-log", serverLog);
        Assert.Equal("device-log", deviceLog?.Message);
        Assert.Equal("done", progress?.Stage);
    }

    private static EmbeddedDeviceServerClient CreateClient(
        FakeDeviceServerHost host,
        FakeEmbeddedDeviceRegistryStore registry,
        FakeEmbeddedDeviceServerSettingsProvider? settings = null,
        string publicHost = "127.0.0.1")
    {
        return new EmbeddedDeviceServerClient(
            host,
            registry,
            settings ?? new FakeEmbeddedDeviceServerSettingsProvider(new EmbeddedDeviceServerSettings()),
            new StaticPublicHostResolver(publicHost),
            NullLogger<EmbeddedDeviceServerClient>.Instance);
    }

    private static DeviceRecord CreateRecord(string deviceId)
        => new()
        {
            DeviceId = deviceId,
            Name = "Matrix",
            Token = "token",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

    private sealed class StaticPublicHostResolver(string publicHost) : IEmbeddedDevicePublicHostResolver
    {
        public string ResolvePublicHost() => publicHost;
    }

    private sealed class FakeEmbeddedDeviceServerSettingsProvider(EmbeddedDeviceServerSettings settings)
        : IEmbeddedDeviceServerSettingsProvider
    {
        public Task<EmbeddedDeviceServerSettings> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(settings);
    }

    private sealed class FakeEmbeddedDeviceRegistryStore(IReadOnlyList<DeviceRecord> devices)
        : IEmbeddedDeviceRegistryStore
    {
        public IReadOnlyList<DeviceRecord> SavedDevices { get; private set; } = [];

        public Task<IReadOnlyList<DeviceRecord>> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(devices);

        public Task SaveAsync(IReadOnlyList<DeviceRecord> devicesToSave, CancellationToken cancellationToken = default)
        {
            SavedDevices = devicesToSave;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeviceServerHost : IDeviceServerHost, IDisposable
    {
        public ServerConfig? LastStartConfig { get; private set; }

        public DeviceRecord[] SeededDevices { get; private set; } = [];

        public IReadOnlyList<DeviceRecord> DeviceRecords { get; init; } = [];

        public IReadOnlyList<DeviceSnapshot> Snapshots { get; init; } = [];

        public CommandDispatchResult CommandResult { get; init; } = new();

        public bool RemoveDeviceResult { get; init; }

        public PanelsBatchRegistration BatchRegistration { get; init; } = new(
            string.Empty,
            0,
            0,
            string.Empty,
            string.Empty,
            0,
            0,
            string.Empty);

        public int StopCallCount { get; private set; }

        public string? LastRemovedDeviceId { get; private set; }

        public DeviceCommandType? LastTrackedCommandType { get; private set; }

        public IReadOnlyDictionary<string, string>? LastTrackedParameters { get; private set; }

        public DeviceCommandSessionContext? LastTrackedSessionContext { get; private set; }

        public string? LastBatchDeviceId { get; private set; }

        public event EventHandler? DevicesChanged;

        public event EventHandler<string>? LogMessage;

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived;

        public Task StartAsync(ServerConfig config, CancellationToken cancellationToken = default)
        {
            LastStartConfig = config;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCallCount++;
            return Task.CompletedTask;
        }

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
            => new() { Code = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot() => Snapshots;

        public IReadOnlyList<DeviceRecord> GetDeviceRecords() => DeviceRecords;

        public void SeedDevices(IEnumerable<DeviceRecord> devices)
        {
            SeededDevices = devices.ToArray();
        }

        public Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CommandResult);

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            LastTrackedCommandType = commandType;
            LastTrackedParameters = parameters;
            return Task.FromResult(CommandResult);
        }

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            DeviceCommandSessionContext? sessionContext,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            LastTrackedCommandType = commandType;
            LastTrackedParameters = parameters;
            LastTrackedSessionContext = sessionContext;
            return Task.FromResult(CommandResult);
        }

        public bool RemoveDevice(string deviceId)
        {
            LastRemovedDeviceId = deviceId;
            return RemoveDeviceResult;
        }

        public void SendFrame(string deviceId, byte[] framePayload)
        {
        }

        public void BroadcastFrame(byte[] framePayload)
        {
        }

        public PanelsBatchRegistration RegisterPanelsBatch(
            string deviceId,
            string panelsSessionId,
            ulong batchSequence,
            byte[] payload,
            int frameCount,
            int durationMs,
            string contentType = "image/webp")
        {
            LastBatchDeviceId = deviceId;
            return BatchRegistration;
        }

        public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
        {
        }

        public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

        public void RaiseLogMessage(string message) => LogMessage?.Invoke(this, message);

        public void RaiseDeviceLog(DeviceLogMessage message) => DeviceLogReceived?.Invoke(this, message);

        public void RaiseCommandProgress(DeviceCommandProgressMessage message) => CommandProgressChanged?.Invoke(this, message);

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
