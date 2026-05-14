using Device.Protocol.Contracts;
using Device.Server.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicaAudio.Server;

// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#fase-1---server-standalone-cloud-ready-sem-mudar-wire
// DOCS: docs/handoffs/2026-04-22-micaudio-server-standalone.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-22-micaudio-server-docker-advertised-endpoints.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public static class MicaAudioServerBootstrap
{
    public static MicaAudioServerOptions LoadOptions(IConfiguration configuration, string? renderPort = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new MicaAudioServerOptions();
        configuration.Bind(options);

        var portOverride = renderPort ?? Environment.GetEnvironmentVariable("PORT");
        if (TryParsePort(portOverride, out var port))
        {
            options.Port = port;
        }

        NormalizeOptions(options);
        return options;
    }

    public static ServerConfig CreateServerConfig(MicaAudioServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new ServerConfig
        {
            ListenHost = options.ListenHost,
            Port = options.Port,
            MqttPort = options.MqttPort,
            MaxDevices = options.MaxDevices,
            MdnsServiceName = options.MdnsServiceName,
            PublicHost = options.PublicHost,
            MqttRootTopic = options.MqttRootTopic,
            AdminToken = options.AdminToken,
            RestrictToPrivateNetworks = options.RestrictToPrivateNetworks,
            VisualUdpPort = options.VisualUdpPort,
            PreferLanUdpVisualTransport = options.PreferLanUdpVisualTransport,
            PublicHttpBaseAddress = options.PublicHttpBaseAddress,
            AllowedCidrs = options.AllowedCidrs,
            PairRequestsPerMinute = options.PairRequestsPerMinute,
            CommandAckRequestsPerSecond = options.CommandAckRequestsPerSecond,
            WebSocketHandshakesPerMinute = options.WebSocketHandshakesPerMinute,
            PairingAttemptsPerWindow = options.PairingAttemptsPerWindow,
            PairingAttemptWindowSeconds = options.PairingAttemptWindowSeconds,
            DeviceFreshThresholdSeconds = options.DeviceFreshThresholdSeconds,
            AllowLegacyWebSocketQueryToken = options.AllowLegacyWebSocketQueryToken,
            MaxJsonBodyBytes = options.MaxJsonBodyBytes,
            MaxWebSocketMessageBytes = options.MaxWebSocketMessageBytes,
        };
    }

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(LoadOptions(configuration));
        services.AddSingleton<IPanelsBatchStore, InMemoryPanelsBatchStore>();
        services.AddSingleton<IDevicePairingStore, InMemoryDevicePairingStore>();
        services.AddSingleton<ICommandStateStore, InMemoryCommandStateStore>();
        services.AddSingleton<ISessionStateStore, InMemorySessionStateStore>();
        services.AddSingleton<IServerPanelStore>(sp =>
            new FileServerPanelStore(sp.GetRequiredService<MicaAudioServerOptions>().StorageRoot));
        services.AddSingleton<IServerPanelCatalogStore>(sp =>
            new FileServerPanelCatalogStore(sp.GetRequiredService<MicaAudioServerOptions>().StorageRoot));
        services.AddSingleton<IServerMediaStore>(sp =>
            new FileServerMediaStore(sp.GetRequiredService<MicaAudioServerOptions>().StorageRoot));
        services.AddSingleton<DeviceServerHost>(sp => new DeviceServerHost(
            TimeProvider.System,
            firmwareCatalog: null,
            sp.GetRequiredService<IPanelsBatchStore>(),
            sp.GetRequiredService<IDevicePairingStore>(),
            sp.GetRequiredService<ICommandStateStore>(),
            sp.GetRequiredService<ISessionStateStore>()));
        services.AddSingleton<IDeviceServerHost>(sp => sp.GetRequiredService<DeviceServerHost>());
        services.AddSingleton(sp => new StandaloneDeviceRegistryStore(sp.GetRequiredService<MicaAudioServerOptions>().StorageRoot));
        services.AddHostedService<MicaAudioServerRuntime>();
        services.AddHostedService<PanelCompositorHostedService>();
    }

    private static void NormalizeOptions(MicaAudioServerOptions options)
    {
        options.ListenHost = NormalizeString(options.ListenHost, "0.0.0.0");
        options.Port = NormalizePort(options.Port, 5272);
        options.MqttPort = NormalizePort(options.MqttPort, 5273);
        options.MaxDevices = Math.Max(1, options.MaxDevices);
        options.MdnsServiceName = NormalizeString(options.MdnsServiceName, "_micaaudio._tcp");
        options.PublicHost = options.PublicHost?.Trim() ?? string.Empty;
        options.PublicHttpBaseAddress = NormalizePublicHttpBaseAddress(options.PublicHttpBaseAddress);
        options.MqttRootTopic = NormalizeString(options.MqttRootTopic, "mica/v1/devices").Trim('/');
        options.AdminToken = options.AdminToken?.Trim() ?? string.Empty;
        options.VisualUdpPort = NormalizePort(options.VisualUdpPort, 5274);
        options.StorageRoot = NormalizeString(options.StorageRoot, Path.Combine(AppContext.BaseDirectory, "data"));
        options.StartupPairCodeTtlSeconds = Math.Max(0, options.StartupPairCodeTtlSeconds);
        options.PairRequestsPerMinute = Math.Max(1, options.PairRequestsPerMinute);
        options.CommandAckRequestsPerSecond = Math.Max(1, options.CommandAckRequestsPerSecond);
        options.WebSocketHandshakesPerMinute = Math.Max(1, options.WebSocketHandshakesPerMinute);
        options.PairingAttemptsPerWindow = Math.Max(1, options.PairingAttemptsPerWindow);
        options.PairingAttemptWindowSeconds = Math.Max(10, options.PairingAttemptWindowSeconds);
        options.DeviceFreshThresholdSeconds = Math.Clamp(options.DeviceFreshThresholdSeconds, 5, 120);
        options.MaxJsonBodyBytes = Math.Clamp(options.MaxJsonBodyBytes, 1024L, 1024L * 1024L);
        options.MaxWebSocketMessageBytes = Math.Clamp(options.MaxWebSocketMessageBytes, 1024, 1024 * 1024);
        options.AllowedCidrs ??= Array.Empty<string>();
    }

    private static string NormalizeString(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static int NormalizePort(int port, int fallback)
        => port is >= 1 and <= 65535 ? port : fallback;

    private static string NormalizePublicHttpBaseAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return string.Empty;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static bool TryParsePort(string? value, out int port)
    {
        port = 0;
        return !string.IsNullOrWhiteSpace(value)
               && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out port)
               && port is >= 1 and <= 65535;
    }
}
