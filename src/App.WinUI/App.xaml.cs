using System.Text;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Apps.UseCases;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Firmware;
using App.WinUI.ViewModels;
using App.WinUI.Views;
using Audio.Loopback.Capture;
using Device.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using MicaAudio.Core.Config;
using Output.Led;

namespace App.WinUI;

// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    internal static IServiceProvider? Services { get; private set; }

    internal static bool IsShellChromeHidden { get; private set; }

    internal static event Action<bool>? ShellChromeVisibilityChanged;

    private static readonly object CrashLogGate = new();

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
        MainWindow ??= new Window();

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            MainWindow.Content = rootFrame;
        }

        MainWindow.Closed -= OnMainWindowClosed;
        MainWindow.Closed += OnMainWindowClosed;

        ApplySystemBackdrop(rootFrame);

        EnsureServicesInitialized();
        _ = StartDeviceIntegrationAsync(Services!);

        try
        {
            if (rootFrame.Content is null)
            {
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
        var roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataRoot = Path.Combine(roamingRoot, "MicaAudio");
        var localAppDataRoot = Path.Combine(localRoot, "MicaAudio");

        var options = new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            DevicesFilePath = Path.Combine(appDataRoot, "devices.json"),
            SettingsFilePath = Path.Combine(appDataRoot, "settings.json"),
            PresetsDirectory = Path.Combine(appDataRoot, "presets"),
            AppsCatalogPath = Path.Combine(appDataRoot, "apps", "catalog.json"),
            AppsModifierStatePath = Path.Combine(appDataRoot, "apps", "modifiers.json"),
            CrashLogPath = Path.Combine(localAppDataRoot, "crash.log"),
            PrecompiledFirmwareDirectory = Path.Combine(AppContext.BaseDirectory, "AppData", "Firmware"),
        };

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddDebug();
        });

        services.Configure<MicaAudioOptions>(configured =>
        {
            configured.AppDataRoot = options.AppDataRoot;
            configured.DevicesFilePath = options.DevicesFilePath;
            configured.SettingsFilePath = options.SettingsFilePath;
            configured.PresetsDirectory = options.PresetsDirectory;
            configured.AppsCatalogPath = options.AppsCatalogPath;
            configured.AppsModifierStatePath = options.AppsModifierStatePath;
            configured.CrashLogPath = options.CrashLogPath;
            configured.PrecompiledFirmwareDirectory = options.PrecompiledFirmwareDirectory;
        });

        services.AddSingleton<IDeviceRegistryStore, JsonDeviceRegistryStore>();
        services.AddSingleton<DeviceServerHost>();
        services.AddSingleton<IDeviceServerHost>(sp => sp.GetRequiredService<DeviceServerHost>());
        services.AddSingleton(sp => new DeviceIntegrationService(
            sp.GetRequiredService<IDeviceServerHost>(),
            sp.GetRequiredService<IDeviceRegistryStore>(),
            sp.GetRequiredService<SettingsRepository>(),
            sp.GetRequiredService<AppSettingsDomainService>(),
            sp.GetRequiredService<ILogger<DeviceIntegrationService>>()));
        services.AddSingleton<DeviceOperationsCoordinator>();
        services.AddSingleton<Hub75VisualizerSessionService>();

        services.AddSingleton<IAppCatalogService, AppCatalogService>();
        services.AddSingleton<IAppModifierStateStore, AppModifierStateStore>();
        services.AddSingleton<CityAutocompleteService>();
        services.AddSingleton<IAppDeploymentService, AppDeploymentService>();
        services.AddSingleton<AppConfigValidationUseCase>();
        services.AddSingleton<SaveAppConfigUseCase>();
        services.AddSingleton<DeployAppUseCase>();
        services.AddSingleton<PrecompiledFirmwareService>();

        services.AddSingleton<PresetRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<AppSettingsDomainService>();
        services.AddSingleton<ILoopbackCapture, WasapiLoopbackCaptureService>();
        services.AddSingleton<SimulatorLedOutput>();
        services.AddSingleton<NullLedOutput>();
        services.AddSingleton(sp => new Esp32S3LedOutput(sp.GetRequiredService<DeviceServerHost>()));

        services.AddTransient<MainPageViewModel>();
        services.AddTransient<DevicesPageViewModel>();
        services.AddTransient<AppsPageViewModel>();
        services.AddTransient<ShellPageViewModel>();
        services.AddTransient<MainPage>(sp => new MainPage(
            sp.GetRequiredService<MainPageViewModel>(),
            sp.GetRequiredService<PresetRepository>(),
            sp.GetRequiredService<SettingsRepository>(),
            sp.GetRequiredService<AppSettingsDomainService>(),
            sp.GetRequiredService<ILoopbackCapture>(),
            sp.GetRequiredService<SimulatorLedOutput>(),
            sp.GetRequiredService<NullLedOutput>(),
            sp.GetRequiredService<Esp32S3LedOutput>()));

        services.AddTransient<DevicesPage>(sp => new DevicesPage(
            sp.GetRequiredService<DevicesPageViewModel>(),
            sp.GetRequiredService<DeviceOperationsCoordinator>(),
            sp.GetRequiredService<PrecompiledFirmwareService>(),
            sp.GetRequiredService<IAppCatalogService>(),
            sp.GetRequiredService<SettingsRepository>(),
            sp.GetRequiredService<AppSettingsDomainService>(),
            sp.GetRequiredService<SimulatorLedOutput>()));

        services.AddTransient<AppsPage>(sp => new AppsPage(
            sp.GetRequiredService<AppsPageViewModel>(),
            sp.GetRequiredService<DeviceOperationsCoordinator>(),
            sp.GetRequiredService<IAppCatalogService>(),
            sp.GetRequiredService<IAppModifierStateStore>(),
            sp.GetRequiredService<CityAutocompleteService>(),
            sp.GetRequiredService<SaveAppConfigUseCase>(),
            sp.GetRequiredService<DeployAppUseCase>(),
            sp.GetRequiredService<AppConfigValidationUseCase>(),
            sp.GetRequiredService<DeviceIntegrationService>()));

        services.AddTransient<ShellPage>(sp => new ShellPage(
            sp.GetRequiredService<ShellPageViewModel>(),
            sp.GetRequiredService<DeviceOperationsCoordinator>(),
            sp.GetRequiredService<MainPage>(),
            sp.GetRequiredService<DevicesPage>(),
            sp.GetRequiredService<AppsPage>()));

        return services.BuildServiceProvider();
    }

    internal static void EnsureServicesInitialized()
    {
        Services ??= BuildServiceProvider();
    }

    private static async Task StartDeviceIntegrationAsync(IServiceProvider services)
    {
        var deviceIntegration = services.GetService<DeviceIntegrationService>();
        if (deviceIntegration is null)
        {
            return;
        }

        try
        {
            await deviceIntegration.StartAsync().ConfigureAwait(false);

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

                var deviceIntegration = Services.GetService<DeviceIntegrationService>();
                if (deviceIntegration is not null)
                {
                    await deviceIntegration.DisposeAsync().ConfigureAwait(false);
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

    private void ApplySystemBackdrop(Frame rootFrame)
    {
        if (MainWindow is null)
        {
            return;
        }

        try
        {
            MainWindow.SystemBackdrop = new MicaBackdrop();
        }
        catch (Exception ex)
        {
            WriteCrashLog("Mica backdrop unavailable. Using solid fallback.", ex);
            MainWindow.SystemBackdrop = null;
            if (TryResolveFallbackBrush(out var brush))
            {
                rootFrame.Background = brush;
            }
        }
    }

    private bool TryResolveFallbackBrush(out Brush? brush)
    {
        brush = null;

        if (Resources.TryGetValue("AppSurfaceBaseBrush", out var primary) && primary is Brush primaryBrush)
        {
            brush = primaryBrush;
            return true;
        }

        if (Resources.TryGetValue("AppFallbackSurfaceBaseBrush", out var fallback) && fallback is Brush fallbackBrush)
        {
            brush = fallbackBrush;
            return true;
        }

        return false;
    }

    private static void WriteCrashLog(string header, Exception ex)
    {
        var logger = Services?.GetService<ILogger<App>>();
        if (logger is not null)
        {
            logger.LogCritical(ex, header);
            return;
        }

        var path = GetCrashLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var log = new StringBuilder()
            .AppendLine("=== " + header + " ===")
            .AppendLine(DateTimeOffset.Now.ToString("O"))
            .AppendLine(ex.ToString())
            .AppendLine();

        lock (CrashLogGate)
        {
            File.AppendAllText(path, log.ToString());
        }
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

    private static UIElement BuildStartupFallbackView(Exception ex)
    {
        var panel = new StackPanel
        {
            Padding = new Thickness(20),
            Spacing = 10,
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Falha ao iniciar a interface principal.",
            FontSize = 20,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"Log de erro: {GetCrashLogPath()}",
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"{ex.GetType().Name}: {ex.Message}",
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        return new ScrollViewer
        {
            Content = panel,
        };
    }
}



