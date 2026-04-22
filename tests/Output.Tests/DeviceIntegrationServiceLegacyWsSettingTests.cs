using App.WinUI.Services;
using App.WinUI.Services.Devices;
using Device.Client;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace Output.Tests;

public sealed class DeviceIntegrationServiceLegacyWsSettingTests
{
    [Fact]
    public async Task StartAsync_ShouldDisableLegacyWsQueryToken_ByDefault()
    {
        var appDataRoot = CreateTestDirectory();
        try
        {
            var settingsRepository = CreateSettingsRepository(appDataRoot);
            using var fakeHost = new FakeDeviceServerHost();
            var fakeRegistry = new FakeDeviceRegistryStore();
            var domainService = new AppSettingsDomainService();

            await using var service = new DeviceIntegrationService(
                fakeHost,
                fakeRegistry,
                settingsRepository,
                domainService,
                NullLogger<DeviceIntegrationService>.Instance);

            await service.StartAsync();

            Assert.NotNull(fakeHost.LastStartConfig);
            Assert.False(fakeHost.LastStartConfig!.AllowLegacyWebSocketQueryToken);
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_ShouldEnableLegacyWsQueryToken_WhenSettingIsTrue()
    {
        var appDataRoot = CreateTestDirectory();
        try
        {
            var settingsRepository = CreateSettingsRepository(appDataRoot);
            await settingsRepository.SaveAsync(new AppSettings
            {
                AllowLegacyWebSocketQueryToken = true,
            });

            using var fakeHost = new FakeDeviceServerHost();
            var fakeRegistry = new FakeDeviceRegistryStore();
            var domainService = new AppSettingsDomainService();

            await using var service = new DeviceIntegrationService(
                fakeHost,
                fakeRegistry,
                settingsRepository,
                domainService,
                NullLogger<DeviceIntegrationService>.Instance);

            await service.StartAsync();

            Assert.NotNull(fakeHost.LastStartConfig);
            Assert.True(fakeHost.LastStartConfig!.AllowLegacyWebSocketQueryToken);
        }
        finally
        {
            Directory.Delete(appDataRoot, recursive: true);
        }
    }

    private static SettingsRepository CreateSettingsRepository(string appDataRoot)
    {
        var options = Options.Create(new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            SettingsFilePath = Path.Combine(appDataRoot, "settings.json"),
        });

        return new SettingsRepository(options);
    }

    private static string CreateTestDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeDeviceRegistryStore : IDeviceRegistryStore
    {
        public Task<IReadOnlyList<DeviceRecord>> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DeviceRecord>>(Array.Empty<DeviceRecord>());

        public Task SaveAsync(IReadOnlyList<DeviceRecord> devices, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeDeviceServerHost : IDeviceServerHost, IDisposable
    {
        public ServerConfig? LastStartConfig { get; private set; }

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

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived
        {
            add { }
            remove { }
        }

        public Task StartAsync(ServerConfig config, CancellationToken cancellationToken = default)
        {
            LastStartConfig = config;
            return Task.CompletedTask;
        }

        public Task StopAsync() => Task.CompletedTask;

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
            => new() { Code = "000000", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot() => Array.Empty<DeviceSnapshot>();

        public IReadOnlyList<DeviceRecord> GetDeviceRecords() => Array.Empty<DeviceRecord>();

        public void SeedDevices(IEnumerable<DeviceRecord> devices)
        {
        }

        public Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = false,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "not-implemented",
                ErrorCode = "not_implemented",
            });

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            => SendCommandTrackedAsync(deviceId, commandType, timeout, cancellationToken);

        public bool RemoveDevice(string deviceId) => false;

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
            => new(
                panelsSessionId,
                batchSequence,
                payload.LongLength,
                Sha256: string.Empty,
                contentType,
                frameCount,
                durationMs,
                DownloadUrl: string.Empty);

        public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
        {
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
