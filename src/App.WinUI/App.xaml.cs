using App.WinUI.Infrastructure;
using App.WinUI.Infrastructure.Http;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Firmware;
using App.WinUI.Services.Logging;
using App.WinUI.Services.Monitoring;
using App.WinUI.Services.Panels;
using App.WinUI.ViewModels;
using Audio.Loopback.Capture;
using Device.Client;
using Device.Client.Embedded;
using Device.Server.Hosting;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Output.Led;

namespace App.WinUI;

// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
// DOCS: docs/handoffs/2026-04-20-remove-usb-flash-flow.md
// DOCS: docs/handoffs/2026-04-21-remove-settings-serial-monitor.md
// DOCS: docs/handoffs/2026-04-22-device-server-client-boundary.md
// DOCS: docs/handoffs/2026-04-22-device-client-abstractions.md
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
// DOCS: docs/handoffs/2026-04-22-device-server-panels-batch-storage.md
// DOCS: docs/handoffs/2026-04-22-device-server-pairing-store.md
// DOCS: docs/handoffs/2026-04-22-device-server-command-state-store.md
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    internal static IServiceProvider? Services { get; private set; }

    internal static bool IsShellChromeHidden { get; private set; }

    internal static event Action<bool>? ShellChromeVisibilityChanged;

    public App()
    {
        UnhandledException += OnUnhandledException;
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            WriteCrashLog("InitializeComponent(App) failed", ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // DOCS: docs/wiki/architecture/02-runtime-lifecycle.md#startup
        RecordStartupBreadcrumb("OnLaunched");
        MainWindow ??= new Window();

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            MainWindow.Content = rootFrame;
        }

        MainWindow.Closed -= OnMainWindowClosed;
        MainWindow.Closed += OnMainWindowClosed;

        try
        {
            RecordStartupBreadcrumb("BuildServiceProvider");
            EnsureServicesInitialized();
            var startupSettings = LoadStartupSettings(Services!);
            ApplyBackdropPreference(rootFrame, startupSettings.UseMicaBackdrop);
            _ = StartDeviceIntegrationAsync(Services!);

            if (rootFrame.Content is null)
            {
                RecordStartupBreadcrumb("Resolve ShellPage");
                rootFrame.Content = Services!.GetRequiredService<ShellPage>();
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnLaunched failed", ex);
            MainWindow.Content = BuildStartupFallbackView(ex);
        }

        MainWindow.Activate();
    }

    internal static IServiceProvider BuildServiceProvider()
    {
        RecordStartupBreadcrumb("BuildServiceProvider.ConfigureServices");
        var roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataRoot = Path.Combine(roamingRoot, "MicaAudio");
        var localAppDataRoot = Path.Combine(localRoot, "MicaAudio");
        var workspaceRoot = ResolveWorkspaceRoot();

        var options = new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            DevicesFilePath = Path.Combine(appDataRoot, "devices.json"),
            SettingsFilePath = Path.Combine(appDataRoot, "settings.json"),
            PresetsDirectory = Path.Combine(appDataRoot, "presets"),
            AppsCatalogPath = Path.Combine(appDataRoot, "apps", "catalog.json"),
            AppsModifierStatePath = Path.Combine(appDataRoot, "apps", "modifiers.json"),
            PanelsFilePath = Path.Combine(appDataRoot, "panels", "panels.json"),
            CrashLogPath = Path.Combine(localAppDataRoot, "crash.log"),
            PrecompiledFirmwareDirectory = Path.Combine(AppContext.BaseDirectory, "AppData", "Firmware"),
            WorkspaceRoot = workspaceRoot,
        };

        var observability = AppObservability.ConfigureGlobalLogger(options);
        var services = new ServiceCollection();

        services.AddLogging(AppObservability.ConfigureLogging);
        // DOCS: docs/wiki/modules/app-winui.md#observabilidade-tecnica
        AppObservability.ConfigureOpenTelemetry(services, observability);
        // DOCS: docs/wiki/modules/app-winui.md#cache-compartilhado
        services.AddHybridCache();
        // DOCS: docs/wiki/modules/app-winui.md#integracoes-http-externas
        services.AddExternalHttpClients();

        services.Configure<MicaAudioOptions>(configured =>
        {
            configured.AppDataRoot = options.AppDataRoot;
            configured.DevicesFilePath = options.DevicesFilePath;
            configured.SettingsFilePath = options.SettingsFilePath;
            configured.PresetsDirectory = options.PresetsDirectory;
            configured.AppsCatalogPath = options.AppsCatalogPath;
            configured.AppsModifierStatePath = options.AppsModifierStatePath;
            configured.PanelsFilePath = options.PanelsFilePath;
            configured.CrashLogPath = options.CrashLogPath;
            configured.PrecompiledFirmwareDirectory = options.PrecompiledFirmwareDirectory;
            configured.WorkspaceRoot = options.WorkspaceRoot;
        });

        services.AddSingleton<PrecompiledFirmwareService>();
        services.AddSingleton<IDeviceOfficialFirmwareCatalog, PrecompiledFirmwareCatalogAdapter>();
        services.AddSingleton<IEmbeddedDeviceRegistryStore, JsonDeviceRegistryStore>();
        services.AddSingleton<IEmbeddedDeviceServerSettingsProvider, AppEmbeddedDeviceServerSettingsProvider>();
        services.AddSingleton<IEmbeddedDevicePublicHostResolver, NetworkInterfaceEmbeddedDevicePublicHostResolver>();
        services.AddSingleton<IPanelsBatchStore, InMemoryPanelsBatchStore>();
        services.AddSingleton<IDevicePairingStore, InMemoryDevicePairingStore>();
        services.AddSingleton<ICommandStateStore, InMemoryCommandStateStore>();
        services.AddSingleton<ISessionStateStore, InMemorySessionStateStore>();
        services.AddSingleton<DeviceServerHost>(sp => new DeviceServerHost(
            TimeProvider.System,
            sp.GetService<IDeviceOfficialFirmwareCatalog>(),
            sp.GetRequiredService<IPanelsBatchStore>(),
            sp.GetRequiredService<IDevicePairingStore>(),
            sp.GetRequiredService<ICommandStateStore>(),
            sp.GetRequiredService<ISessionStateStore>()));
        services.AddSingleton<IDeviceServerHost>(sp => sp.GetRequiredService<DeviceServerHost>());
        services.AddSingleton<IDeviceFrameTransport>(sp => sp.GetRequiredService<IDeviceServerHost>());
        services.AddSingleton(sp => new EmbeddedDeviceServerClient(
            sp.GetRequiredService<IDeviceServerHost>(),
            sp.GetRequiredService<IEmbeddedDeviceRegistryStore>(),
            sp.GetRequiredService<IEmbeddedDeviceServerSettingsProvider>(),
            sp.GetRequiredService<IEmbeddedDevicePublicHostResolver>(),
            sp.GetRequiredService<ILogger<EmbeddedDeviceServerClient>>()));
        services.AddSingleton<IDeviceServerClient>(sp => sp.GetRequiredService<EmbeddedDeviceServerClient>());
        services.AddSingleton<IEmbeddedDeviceServerClientRuntime>(sp => sp.GetRequiredService<EmbeddedDeviceServerClient>());
        services.AddSingleton<DeviceOperationsCoordinator>(sp =>
        {
            var coordinator = new DeviceOperationsCoordinator(
                sp.GetRequiredService<IDeviceServerClient>(),
                sp.GetRequiredService<SettingsRepository>(),
                sp.GetRequiredService<AppSettingsDomainService>(),
                sp.GetRequiredService<ILogger<DeviceOperationsCoordinator>>());
            coordinator.CentralLogStore = sp.GetRequiredService<AppLogStore>();
            return coordinator;
        });
        services.AddSingleton<Hub75VisualizerSessionService>();

        services.AddSingleton<IAppCatalogService, AppCatalogService>();
        services.AddSingleton<IAppModifierStateStore, AppModifierStateStore>();
        services.AddSingleton<PanelsStore>();
        services.AddSingleton<PanelsFrameComposer>();
        services.AddSingleton(sp => new CityAutocompleteService(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<CityAutocompleteService>>()));
        services.AddSingleton<WeatherPreviewDataService.IWeatherForecastClient, OpenMeteoForecastClient>();
        services.AddSingleton<WeatherPreviewDataService>();
        services.AddSingleton<AppLogStore>();
        services.AddSingleton<WindowsMemoryFallbackProvider>();
        services.AddSingleton<HwinfoSharedMemorySource>();

        services.AddSingleton<PresetRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<AppSettingsDomainService>();
        services.AddSingleton<ILoopbackCapture, WasapiLoopbackCaptureService>();
        services.AddSingleton<SimulatorLedOutput>();
        services.AddSingleton<NullLedOutput>();
        services.AddSingleton(sp => new Esp32S3LedOutput(sp.GetRequiredService<IDeviceFrameTransport>()));
        services.AddSingleton<PanelsDeviceSessionService>();
        services.AddSingleton(sp => new PanelsPlaybackService(
            sp.GetRequiredService<IDeviceServerClient>(),
            sp.GetRequiredService<IDeviceFrameTransport>(),
            sp.GetRequiredService<PanelsFrameComposer>(),
            sp.GetRequiredService<PanelsDeviceSessionService>(),
            sp.GetRequiredService<Hub75VisualizerSessionService>(),
            enableMatrixTransport: true));

        services.AddTransient<MainPageViewModel>();
        services.AddTransient<DevicesPageViewModel>();
        services.AddTransient<PanelsPageViewModel>();
        services.AddTransient<MonitoringPageViewModel>();
        services.AddTransient<ShellPageViewModel>();
        services.AddTransient<MainPage>(sp => new MainPage(
            sp.GetRequiredService<MainPageViewModel>(),
            sp.GetRequiredService<PresetRepository>(),
            sp.GetRequiredService<SettingsRepository>(),
            sp.GetRequiredService<AppSettingsDomainService>(),
            sp.GetRequiredService<ILoopbackCapture>(),
            sp.GetRequiredService<SimulatorLedOutput>(),
            sp.GetRequiredService<NullLedOutput>(),
            sp.GetRequiredService<Esp32S3LedOutput>(),
            sp.GetRequiredService<Hub75VisualizerSessionService>(),
            sp.GetRequiredService<PanelsPlaybackService>()));

        services.AddTransient<DevicesPage>(sp => new DevicesPage(
            sp.GetRequiredService<DevicesPageViewModel>(),
            sp.GetRequiredService<DeviceOperationsCoordinator>(),
            sp.GetRequiredService<PrecompiledFirmwareService>(),
            sp.GetRequiredService<IAppCatalogService>(),
            sp.GetRequiredService<IAppModifierStateStore>(),
            sp.GetRequiredService<SettingsRepository>(),
            sp.GetRequiredService<AppSettingsDomainService>(),
            sp.GetRequiredService<SimulatorLedOutput>()));

        services.AddTransient<MonitoringPage>(sp => new MonitoringPage(
            sp.GetRequiredService<MonitoringPageViewModel>(),
            sp.GetRequiredService<HwinfoSharedMemorySource>()));

        services.AddTransient<PanelsPage>(sp => new PanelsPage(
            sp.GetRequiredService<PanelsPageViewModel>(),
            sp.GetRequiredService<DeviceOperationsCoordinator>(),
            sp.GetRequiredService<IAppCatalogService>(),
            sp.GetRequiredService<IAppModifierStateStore>(),
            sp.GetRequiredService<PanelsStore>(),
            sp.GetRequiredService<PanelsFrameComposer>(),
            sp.GetRequiredService<PanelsPlaybackService>(),
            sp.GetRequiredService<Hub75VisualizerSessionService>(),
            sp.GetRequiredService<CityAutocompleteService>()));

        services.AddTransient<SettingsPage>(sp => new SettingsPage(
            sp.GetRequiredService<SettingsRepository>(),
            sp.GetRequiredService<AppSettingsDomainService>()));

        services.AddTransient(sp => new ShellPageContentFactory(
            () =>
            {
                RecordStartupBreadcrumb("Resolve MainPage");
                return sp.GetRequiredService<MainPage>();
            },
            () => sp.GetRequiredService<DevicesPage>(),
            () => sp.GetRequiredService<PanelsPage>(),
            () => sp.GetRequiredService<MonitoringPage>(),
            () => sp.GetRequiredService<SettingsPage>()));

        services.AddTransient<ShellPage>(sp => new ShellPage(
            sp.GetRequiredService<ShellPageViewModel>(),
            sp.GetRequiredService<DeviceOperationsCoordinator>(),
            sp.GetRequiredService<ShellPageContentFactory>()));

        return services.BuildServiceProvider();
    }

    internal static void EnsureServicesInitialized()
    {
        Services ??= BuildServiceProvider();
    }

    private static async Task StartDeviceIntegrationAsync(IServiceProvider services)
    {
        var deviceRuntime = services.GetService<IEmbeddedDeviceServerClientRuntime>();
        if (deviceRuntime is null)
        {
            return;
        }

        try
        {
            await deviceRuntime.StartAsync().ConfigureAwait(false);

            _ = WarmUpOfficialFirmwareAsync(services);

            var deviceOps = services.GetService<DeviceOperationsCoordinator>();
            deviceOps?.RequestRefresh();

            var appCatalog = services.GetService<IAppCatalogService>();
            if (appCatalog is not null)
            {
                _ = await appCatalog.LoadCatalogAsync().ConfigureAwait(false);
            }

            var appModifierStore = services.GetService<IAppModifierStateStore>();
            if (appModifierStore is not null)
            {
                await appModifierStore.LoadAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("DeviceIntegration.StartAsync failed", ex);
        }
    }

    private static async void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        try
        {
            if (Services is not null)
            {
                var deviceOps = Services.GetService<DeviceOperationsCoordinator>();
                deviceOps?.Dispose();

                var deviceRuntime = Services.GetService<IEmbeddedDeviceServerClientRuntime>();
                if (deviceRuntime is not null)
                {
                    await deviceRuntime.DisposeAsync().ConfigureAwait(false);
                }
            }

            if (Services is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (Services is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Serilog.Log.CloseAndFlush();
            Services = null;
        }
        catch (Exception ex)
        {
            WriteCrashLog("DeviceIntegration.DisposeAsync failed", ex);
        }
    }

    internal static void SetShellChromeHidden(bool hidden)
    {
        if (IsShellChromeHidden == hidden)
        {
            return;
        }

        IsShellChromeHidden = hidden;
        ShellChromeVisibilityChanged?.Invoke(hidden);
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to navigate to {e.SourcePageType.FullName}.");
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            WriteCrashLog("UnhandledException", e.Exception);
        }
        catch
        {
            // Ignore secondary failures while logging crashes.
        }
    }

    internal static BackdropPreferenceResult ApplyBackdropPreference(bool useMica)
    {
        if (MainWindow?.Content is not Frame rootFrame)
        {
            return new BackdropPreferenceResult(useMica, MicaApplied: false, "Janela principal indisponivel para aplicar backdrop.");
        }

        return ApplyBackdropPreference(rootFrame, useMica);
    }

    internal static BackdropPreferenceResult ApplyBackdropPreference(Frame rootFrame, bool useMica)
    {
        ArgumentNullException.ThrowIfNull(rootFrame);

        if (MainWindow is null)
        {
            return new BackdropPreferenceResult(useMica, MicaApplied: false, "Janela principal indisponivel para aplicar backdrop.");
        }

        if (!useMica)
        {
            MainWindow.SystemBackdrop = null;
            ApplyFallbackBackground(rootFrame);
            return new BackdropPreferenceResult(useMica, MicaApplied: false, "Mica desativado. Usando superficie solida.");
        }

        try
        {
            MainWindow.SystemBackdrop = new MicaBackdrop();
            rootFrame.Background = null;
            return new BackdropPreferenceResult(useMica, MicaApplied: true, "Mica ativo.");
        }
        catch (Exception ex)
        {
            MainWindow.SystemBackdrop = null;
            ApplyFallbackBackground(rootFrame);
            LogBackdropPreferenceIssue("Mica backdrop unavailable. Using solid fallback.", ex);
            return new BackdropPreferenceResult(useMica, MicaApplied: false, "Mica indisponivel neste ambiente. Usando superficie solida.");
        }
    }

    private static async Task WarmUpOfficialFirmwareAsync(IServiceProvider services)
    {
        var firmwareService = services.GetService<PrecompiledFirmwareService>();
        if (firmwareService is null)
        {
            return;
        }

        try
        {
            var requestRefresh = false;
            foreach (var option in firmwareService.GetOptions())
            {
                var result = await firmwareService.EnsureOfficialFirmwareFreshAsync(option.Id).ConfigureAwait(false);
                requestRefresh |= result.WasRegenerated;
            }

            if (requestRefresh)
            {
                services.GetService<DeviceOperationsCoordinator>()?.RequestRefresh();
            }
        }
        catch (Exception ex)
        {
            services.GetService<AppLogStore>()?.Append(LogCategory.System, LogSeverity.Warning, $"Official firmware warm-up failed: {ex.Message}");
        }
    }

    private static AppSettings LoadStartupSettings(IServiceProvider services)
    {
        try
        {
            var repository = services.GetRequiredService<SettingsRepository>();
            var settingsDomainService = services.GetRequiredService<AppSettingsDomainService>();
            var settings = repository.LoadAsync().GetAwaiter().GetResult();
            return settingsDomainService.Migrate(settings);
        }
        catch (Exception ex)
        {
            WriteCrashLog("Load startup settings failed. Using defaults.", ex);
            return new AppSettings();
        }
    }

    private static string ResolveWorkspaceRoot()
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
        };

        foreach (var candidate in candidates)
        {
            var resolved = TryResolveWorkspaceRootFrom(candidate);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return string.Empty;
    }

    private static string? TryResolveWorkspaceRootFrom(string? startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        if (!current.Exists && current.Parent is not null)
        {
            current = current.Parent;
        }

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "MicaAudio.sln");
            var scriptPath = Path.Combine(current.FullName, "scripts", "build-precompiled-firmware.ps1");
            var firmwareRoot = Path.Combine(current.FullName, "firmware", "esp32s3-devkitc1");
            if (File.Exists(solutionPath)
                && File.Exists(scriptPath)
                && Directory.Exists(firmwareRoot))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static void ApplyFallbackBackground(Frame rootFrame)
    {
        if (TryResolveFallbackBrush(out var brush))
        {
            rootFrame.Background = brush;
            return;
        }

        rootFrame.Background = null;
    }

    private static bool TryResolveFallbackBrush(out Brush? brush)
    {
        brush = null;

        if (Current?.Resources.TryGetValue("AppSurfaceBaseBrush", out var primary) == true && primary is Brush primaryBrush)
        {
            brush = primaryBrush;
            return true;
        }

        if (Current?.Resources.TryGetValue("AppFallbackSurfaceBaseBrush", out var fallback) == true && fallback is Brush fallbackBrush)
        {
            brush = fallbackBrush;
            return true;
        }

        return false;
    }

    private static void WriteCrashLog(string header, Exception ex)
    {
        var activity = AppObservability.StartActivity("ui.error", AppObservability.AppComponent);
        AppObservability.RecordUiError(header);
        if (activity is not null)
        {
            activity.SetTag("error.header", header);
            AppObservability.SetException(activity, ex);
        }

        var logger = Services?.GetService<ILogger<App>>();
        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "ui.error"),
            ("error.header", header));
        AppStartupDiagnostics.WriteCrashLog(
            GetCrashLogPath(),
            header,
            ex,
            logger is null ? null : (capturedHeader, capturedException) => LogUnhandledAppFailure(logger, capturedException, capturedHeader));

        var centralLog = Services?.GetService<AppLogStore>();
        centralLog?.Append(LogCategory.System, LogSeverity.Error, $"{header}: {ex.Message}");
        activity?.Dispose();
    }

    private static string GetCrashLogPath()
    {
        var configured = Services?.GetService<IOptions<MicaAudioOptions>>()?.Value?.CrashLogPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicaAudio");
        return Path.Combine(root, "crash.log");
    }

    private static ScrollViewer BuildStartupFallbackView(Exception ex)
        => AppFailureViewFactory.Build(
            "Falha ao iniciar a interface principal.",
            "Abra o log local para ver breadcrumbs de startup e a stack trace completa.",
            ex,
            GetCrashLogPath());

    internal static void RecordStartupBreadcrumb(string stage)
    {
        AppStartupDiagnostics.RecordBreadcrumb(stage);
        var centralLog = Services?.GetService<AppLogStore>();
        centralLog?.Append(LogCategory.System, LogSeverity.Info, $"Startup: {stage}");
    }

    internal static void ReportStartupFailure(string header, Exception ex)
        => WriteCrashLog(header, ex);

    internal static void ReportError(string header, Exception ex)
        => WriteCrashLog(header, ex);

    internal static string CurrentCrashLogPath => GetCrashLogPath();

    private static void LogBackdropPreferenceIssue(string message, Exception ex)
    {
        var logger = Services?.GetService<ILogger<App>>();
        if (logger is not null)
        {
            LogBackdropPreferenceWarning(logger, ex, message);
        }

        Services?.GetService<AppLogStore>()?.Append(LogCategory.System, LogSeverity.Warning, $"{message}: {ex.Message}");
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Critical, Message = "Unhandled app failure: {Header}")]
    private static partial void LogUnhandledAppFailure(ILogger logger, Exception exception, string header);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "{Message}")]
    private static partial void LogBackdropPreferenceWarning(ILogger logger, Exception exception, string message);

    internal readonly record struct BackdropPreferenceResult(bool UseMicaRequested, bool MicaApplied, string StatusMessage);
}



