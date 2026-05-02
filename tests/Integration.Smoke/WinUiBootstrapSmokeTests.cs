using System.Reflection;
using System.Text.Json;
using App.WinUI;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Panels;
using App.WinUI.ViewModels;
using App.WinUI.Views;
using Device.Client;
using Device.Client.Remote;
using Device.Protocol.Models;
using Device.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Integration.Smoke;

public sealed class WinUiBootstrapSmokeTests
{
    [Fact]
    public void BuildServiceProvider_ShouldResolveRemoteOnlyCoreAppServices()
    {
        var provider = App.WinUI.App.BuildServiceProvider();

        Assert.IsType<RemoteDeviceServerClient>(provider.GetRequiredService<IDeviceServerClient>());
        Assert.IsType<RemoteDeviceFrameTransport>(provider.GetRequiredService<IDeviceFrameTransport>());
        Assert.IsType<RemoteDeviceServerRuntime>(provider.GetRequiredService<IDeviceServerClientRuntime>());
        Assert.Null(provider.GetService<IDeviceServerHost>());
        Assert.Null(Type.GetType("Device.Client.Embedded.EmbeddedDeviceServerClient, Device.Client.Embedded"));
        Assert.Null(Type.GetType("Device.Client.Embedded.IEmbeddedDeviceServerClientRuntime, Device.Client.Embedded"));
        Assert.Null(Type.GetType("App.WinUI.Services.Devices.PanelsDeviceSessionService, App.WinUI"));

        Assert.NotNull(provider.GetService<IDeviceClientSessionManager>());
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
    }

    [Fact]
    public async Task BuildServiceProvider_WithLegacyEmbeddedSettings_ShouldStillResolveRemoteClientAndTransport()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-winui-bootstrap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var options = CreateTestOptions(root);
        Directory.CreateDirectory(Path.GetDirectoryName(options.SettingsFilePath)!);
        await File.WriteAllTextAsync(
            options.SettingsFilePath,
            """
            {
              "deviceServerMode": "Embedded",
              "remoteServerBaseAddress": "http://127.0.0.1:5272"
            }
            """);
        var secretStore = new RemoteDeviceServerSecretStore(Options.Create(options));
        await secretStore.SaveAdminTokenAsync("dev-token");

        var provider = App.WinUI.App.BuildServiceProvider(options);

        Assert.IsType<RemoteDeviceServerClient>(provider.GetRequiredService<IDeviceServerClient>());
        Assert.IsType<RemoteDeviceFrameTransport>(provider.GetRequiredService<IDeviceFrameTransport>());
        Assert.IsType<RemoteDeviceServerRuntime>(provider.GetRequiredService<IDeviceServerClientRuntime>());
        Assert.Null(provider.GetService<IDeviceServerHost>());
    }

    [Fact]
    public void PanelsPlaybackService_ShouldDependOnlyOnDeviceServerClientAndComposer()
    {
        var constructor = Assert.Single(typeof(PanelsPlaybackService)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

        var parameters = constructor.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(IDeviceServerClient), parameters[0].ParameterType);
        Assert.Equal(typeof(PanelsFrameComposer), parameters[1].ParameterType);
    }

    [Fact]
    public async Task PanelsPlaybackService_ShouldSaveRemoteRuntimeStateWithoutFrameTransport()
    {
        var client = new FakeDeviceServerClient();
        using var service = new PanelsPlaybackService(client, new PanelsFrameComposer(client));
        var panel = new App.WinUI.Models.Panels.PanelDefinition
        {
            PanelId = "panel-remote",
            Name = "Remoto",
            Widgets =
            [
                new App.WinUI.Models.Panels.PanelWidgetDefinition
                {
                    WidgetId = "clock",
                    AppId = "analogclock",
                    X = 0,
                    Y = 0,
                    Width = 64,
                    Height = 32,
                },
            ],
        };

        await service.StartAsync(panel, "device-remote");

        Assert.True(client.LastRuntimeState?.Enabled);
        Assert.Equal("panel-remote", client.LastRuntimeState?.PanelId);
        Assert.Equal("device-remote", client.LastRuntimeState?.TargetDeviceId);
        Assert.True(service.IsRunning);
        Assert.Equal("device-remote", service.TargetDeviceId);
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
    public void BuildServiceProvider_ShouldPopulateMicaAudioOptions()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MicaAudioOptions>>().Value;

        Assert.False(string.IsNullOrWhiteSpace(options.AppDataRoot));
        Assert.False(string.IsNullOrWhiteSpace(options.SettingsFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.PresetsDirectory));
        Assert.False(string.IsNullOrWhiteSpace(options.AppsCatalogPath));
        Assert.False(string.IsNullOrWhiteSpace(options.AppsModifierStatePath));
        Assert.False(string.IsNullOrWhiteSpace(options.PanelsFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.CrashLogPath));
    }

    [Fact]
    public void StartupPages_ShouldNotExposeServiceLocatorConstructors()
    {
        AssertNoServiceProviderConstructor(typeof(ShellPage));
        AssertNoServiceProviderConstructor(typeof(MainPage));
        AssertNoServiceProviderConstructor(typeof(DevicesPage));
        AssertNoServiceProviderConstructor(typeof(PanelsPage));
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

        public PanelRuntimeStateDocument? LastRuntimeState { get; private set; }

        public string GetServerBaseAddress() => "http://127.0.0.1:5272";

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
            => new() { Code = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public IReadOnlyList<DeviceSnapshot> GetDevices() => Array.Empty<DeviceSnapshot>();

        public bool RemoveDevice(string deviceId)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public Task SavePanelRuntimeStateAsync(PanelRuntimeStateDocument document, CancellationToken cancellationToken = default)
        {
            LastRuntimeState = document;
            return Task.CompletedTask;
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
    }
}
