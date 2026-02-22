using System.Text;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Firmware;
using App.WinUI.Views;
using Device.Server.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace App.WinUI;

// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    internal static IServiceProvider? Services { get; private set; }

    internal static DeviceIntegrationService? DeviceIntegration => Services?.GetService<DeviceIntegrationService>();

    internal static DeviceOperationsCoordinator? DeviceOps => Services?.GetService<DeviceOperationsCoordinator>();

    internal static IAppCatalogService? AppCatalog => Services?.GetService<IAppCatalogService>();

    internal static IAppDeploymentService? AppDeployment => Services?.GetService<IAppDeploymentService>();

    internal static IAppModifierStateStore? AppModifierStore => Services?.GetService<IAppModifierStateStore>();

    internal static CityAutocompleteService? CityAutocomplete => Services?.GetService<CityAutocompleteService>();

    internal static PrecompiledFirmwareService? FirmwareService => Services?.GetService<PrecompiledFirmwareService>();

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
        _ = StartDeviceIntegrationAsync();

        try
        {
            if (rootFrame.Content is null)
            {
                rootFrame.Content = Resolve<ShellPage>();
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
        var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MicaAudio");
        var services = new ServiceCollection();

        services.AddSingleton(new JsonDeviceRegistryStore(appDataRoot));
        services.AddSingleton<DeviceServerHost>();
        services.AddSingleton(sp => new DeviceIntegrationService(sp.GetRequiredService<DeviceServerHost>(), sp.GetRequiredService<JsonDeviceRegistryStore>()));
        services.AddSingleton<DeviceOperationsCoordinator>();

        services.AddSingleton<IAppCatalogService>(new AppCatalogService(appDataRoot));
        services.AddSingleton<IAppModifierStateStore>(new AppModifierStateStore(appDataRoot));
        services.AddSingleton<CityAutocompleteService>();
        services.AddSingleton<IAppDeploymentService, AppDeploymentService>();
        services.AddSingleton<PrecompiledFirmwareService>();

        services.AddTransient<MainPage>();
        services.AddTransient<DevicesPage>();
        services.AddTransient<AppsPage>();
        services.AddTransient<ServerPage>();
        services.AddTransient<ShellPage>();

        return services.BuildServiceProvider();
    }

    internal static void EnsureServicesInitialized()
    {
        Services ??= BuildServiceProvider();
    }

    internal static T Resolve<T>() where T : notnull
    {
        EnsureServicesInitialized();
        return Services!.GetRequiredService<T>();
    }

    private static async Task StartDeviceIntegrationAsync()
    {
        var deviceIntegration = DeviceIntegration;
        if (deviceIntegration is null)
        {
            return;
        }

        try
        {
            await deviceIntegration.StartAsync().ConfigureAwait(false);
            DeviceOps?.RequestRefresh();
            if (AppCatalog is not null)
            {
                _ = await AppCatalog.LoadCatalogAsync().ConfigureAwait(false);
            }

            if (AppModifierStore is not null)
            {
                await AppModifierStore.LoadAsync().ConfigureAwait(false);
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
            DeviceOps?.Dispose();

            if (DeviceIntegration is not null)
            {
                await DeviceIntegration.DisposeAsync().ConfigureAwait(false);
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
        var path = GetCrashLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var log = new StringBuilder()
            .AppendLine("=== " + header + " ===")
            .AppendLine(DateTimeOffset.Now.ToString("O"))
            .AppendLine(ex.ToString())
            .AppendLine();

        File.AppendAllText(path, log.ToString());
    }

    private static string GetCrashLogPath()
    {
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
