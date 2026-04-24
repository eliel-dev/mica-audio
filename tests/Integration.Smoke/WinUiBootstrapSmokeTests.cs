using System.Reflection;
using App.WinUI;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Panels;
using App.WinUI.ViewModels;
using App.WinUI.Views;
using Device.Client;
using Device.Client.Embedded;
using Device.Client.Remote;
using Device.Protocol.Models;
using Device.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Output.Led;
using System.Text.Json;

namespace Integration.Smoke;

public sealed class WinUiBootstrapSmokeTests
{
    [Fact]
    public void BuildServiceProvider_ShouldResolveCoreAppServices()
    {
        var provider = App.WinUI.App.BuildServiceProvider();

        Assert.NotNull(provider.GetService<EmbeddedDeviceServerClient>());
        Assert.NotNull(provider.GetService<IDeviceServerClient>());
        Assert.NotNull(provider.GetService<IEmbeddedDeviceServerClientRuntime>());
        Assert.NotNull(provider.GetService<IDeviceServerClientRuntime>());
        Assert.NotNull(provider.GetService<IDeviceServerHost>());
        Assert.NotNull(provider.GetService<IDeviceClientSessionManager>());
        Assert.NotNull(provider.GetService<IPanelsBatchStore>());
        Assert.NotNull(provider.GetService<IDevicePairingStore>());
        Assert.NotNull(provider.GetService<ICommandStateStore>());
        Assert.NotNull(provider.GetService<ISessionStateStore>());
        Assert.NotNull(provider.GetService<IDeviceFrameTransport>());
        Assert.NotNull(provider.GetService<DeviceOperationsCoordinator>());
        Assert.NotNull(provider.GetService<IAppCatalogService>());
        Assert.NotNull(provider.GetService<IAppModifierStateStore>());
        Assert.NotNull(provider.GetService<Esp32S3LedOutput>());
        Assert.NotNull(provider.GetService<MainPageViewModel>());
        Assert.NotNull(provider.GetService<DevicesPageViewModel>());
        Assert.NotNull(provider.GetService<PanelsPageViewModel>());
        Assert.NotNull(provider.GetService<ShellPageViewModel>());
        Assert.NotNull(provider.GetService<ShellPageContentFactory>());
        Assert.NotNull(provider.GetService<PanelsStore>());
        Assert.NotNull(provider.GetService<PanelsFrameComposer>());
        Assert.NotNull(provider.GetService<PanelsPlaybackService>());
        Assert.NotNull(provider.GetService<PanelsDeviceSessionService>());
    }

    [Fact]
    public async Task BuildServiceProvider_WithRemoteSettings_ShouldResolveRemoteClientAndTransport()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-winui-bootstrap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var options = CreateTestOptions(root);
        Directory.CreateDirectory(Path.GetDirectoryName(options.SettingsFilePath)!);
        await File.WriteAllTextAsync(
            options.SettingsFilePath,
            JsonSerializer.Serialize(new AppSettings
            {
                DeviceServerMode = DeviceServerMode.Remote,
                RemoteServerBaseAddress = "http://127.0.0.1:5272",
            }));
        var secretStore = new RemoteDeviceServerSecretStore(Options.Create(options));
        await secretStore.SaveAdminTokenAsync("dev-token");

        var provider = App.WinUI.App.BuildServiceProvider(options);

        Assert.IsType<RemoteDeviceServerClient>(provider.GetRequiredService<IDeviceServerClient>());
        Assert.IsType<RemoteDeviceFrameTransport>(provider.GetRequiredService<IDeviceFrameTransport>());
        Assert.IsType<RemoteDeviceServerRuntime>(provider.GetRequiredService<IDeviceServerClientRuntime>());
        Assert.NotNull(provider.GetRequiredService<DeviceServerHost>());
    }

    [Fact]
    public void BuildServiceProvider_ShouldEnableMatrixTransportForPanelsPlaybackService()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var service = provider.GetRequiredService<PanelsPlaybackService>();

        var field = typeof(PanelsPlaybackService).GetField(
            "enableMatrixTransport",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.True(Assert.IsType<bool>(field!.GetValue(service)));
    }

    [Fact]
    public void PanelsPlaybackService_ShouldDependOnDeviceServerClientAndFrameTransport()
    {
        var constructor = typeof(PanelsPlaybackService)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(ctor => ctor.GetParameters().Length == 7);

        Assert.Equal(typeof(IDeviceServerClient), constructor.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(IDeviceFrameTransport), constructor.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(IDeviceClientSessionManager), constructor.GetParameters()[6].ParameterType);
        Assert.True(constructor.GetParameters()[6].HasDefaultValue);
    }

    [Fact]
    public void PanelsPlaybackService_ShouldBeConstructibleWithFakedDeviceServerClientAndFrameTransport()
    {
        var client = new FakeDeviceServerClient();
        var frameTransport = new FakeDeviceFrameTransport();
        using var coordinator = new DeviceOperationsCoordinator(
            client,
            settingsRepository: null,
            settingsDomainService: null);
        using var panelsDeviceSessionService = new PanelsDeviceSessionService(coordinator);
        using var visualizerSessionService = new Hub75VisualizerSessionService(coordinator);
        using var service = new PanelsPlaybackService(
            client,
            frameTransport,
            new PanelsFrameComposer(),
            panelsDeviceSessionService,
            visualizerSessionService,
            enableMatrixTransport: false);

        Assert.False(service.IsRunning);
    }

    [Fact]
    public void BuildServiceProvider_ShouldRegisterShellAndPages()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(MainPage)));
        Assert.True(isService.IsService(typeof(DevicesPage)));
        Assert.True(isService.IsService(typeof(PanelsPage)));
        Assert.True(isService.IsService(typeof(SettingsPage)));
        Assert.True(isService.IsService(typeof(ShellPage)));
        Assert.True(isService.IsService(typeof(ShellPageContentFactory)));
    }

    [Fact]
    public void BuildServiceProvider_ShouldNotRegisterSerialMonitorServices()
    {
        var serialMonitorType = Type.GetType("App.WinUI.Infrastructure.Serial.ISerialMonitorService, App.WinUI");
        var serialCatalogType = Type.GetType("App.WinUI.Infrastructure.Serial.ISerialPortCatalogService, App.WinUI");

        Assert.Null(serialMonitorType);
        Assert.Null(serialCatalogType);
    }

    [Fact]
    public void BuildServiceProvider_ShouldRegisterDependenciesRequiredByStartupPages()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(MainPageViewModel)));
        Assert.True(isService.IsService(typeof(DevicesPageViewModel)));
        Assert.True(isService.IsService(typeof(PanelsPageViewModel)));
        Assert.True(isService.IsService(typeof(ShellPageViewModel)));
        Assert.True(isService.IsService(typeof(PresetRepository)));
        Assert.True(isService.IsService(typeof(SettingsRepository)));
        Assert.True(isService.IsService(typeof(AppSettingsDomainService)));
    }

    [Fact]
    public void StartupPages_ShouldNotExposeServiceLocatorConstructors()
    {
        AssertNoServiceProviderConstructor(typeof(ShellPage));
        AssertNoServiceProviderConstructor(typeof(MainPage));
        AssertNoServiceProviderConstructor(typeof(DevicesPage));
        AssertNoServiceProviderConstructor(typeof(PanelsPage));
    }

    [Fact]
    public void BuildServiceProvider_ShouldPopulateMicaAudioOptions()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MicaAudioOptions>>().Value;

        Assert.False(string.IsNullOrWhiteSpace(options.AppDataRoot));
        Assert.False(string.IsNullOrWhiteSpace(options.DevicesFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.SettingsFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.PresetsDirectory));
        Assert.False(string.IsNullOrWhiteSpace(options.AppsCatalogPath));
        Assert.False(string.IsNullOrWhiteSpace(options.AppsModifierStatePath));
        Assert.False(string.IsNullOrWhiteSpace(options.PanelsFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.CrashLogPath));
    }

    private static void AssertNoServiceProviderConstructor(Type pageType)
    {
        var constructors = pageType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var hasServiceProviderCtor = constructors.Any(ctor =>
        {
            var parameters = ctor.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(IServiceProvider);
        });

        Assert.False(hasServiceProviderCtor, $"Service locator constructor not allowed: {pageType.FullName}");
    }

    private static MicaAudioOptions CreateTestOptions(string root)
    {
        var local = Path.Combine(root, "local");
        var roaming = Path.Combine(root, "roaming");
        return new MicaAudioOptions
        {
            AppDataRoot = roaming,
            DevicesFilePath = Path.Combine(roaming, "devices.json"),
            SettingsFilePath = Path.Combine(roaming, "settings.json"),
            PresetsDirectory = Path.Combine(roaming, "presets"),
            AppsCatalogPath = Path.Combine(roaming, "apps", "catalog.json"),
            AppsModifierStatePath = Path.Combine(roaming, "apps", "modifiers.json"),
            PanelsFilePath = Path.Combine(roaming, "panels", "panels.json"),
            RemoteDeviceServerSecretsFilePath = Path.Combine(roaming, "remote-server-secrets.json"),
            CrashLogPath = Path.Combine(local, "crash.log"),
            PrecompiledFirmwareDirectory = Path.Combine(AppContext.BaseDirectory, "AppData", "Firmware"),
            WorkspaceRoot = string.Empty,
        };
    }

    private sealed class FakeDeviceServerClient : IDeviceServerClient
    {
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

        public IReadOnlyList<DeviceSnapshot> GetDevices() => Array.Empty<DeviceSnapshot>();

        public bool RemoveDevice(string deviceId)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

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
}
