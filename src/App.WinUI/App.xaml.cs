using System.Text;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Firmware;
using App.WinUI.Views;
using Device.Server.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace App.WinUI;

// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    internal static DeviceIntegrationService? DeviceIntegration { get; private set; }

    internal static DeviceOperationsCoordinator? DeviceOps { get; private set; }

    internal static AppCatalogService? AppCatalog { get; private set; }

    internal static AppDeploymentService? AppDeployment { get; private set; }

    internal static AppModifierStateStore? AppModifierStore { get; private set; }

    internal static CityAutocompleteService? CityAutocomplete { get; private set; }

    internal static PrecompiledFirmwareService? FirmwareService { get; private set; }

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

        EnsureDeviceIntegrationInitialized();
        _ = StartDeviceIntegrationAsync();

        try
        {
            if (rootFrame.Content is null)
            {
                rootFrame.Content = new ShellPage();
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnLaunched failed", ex);
            MainWindow.Content = BuildStartupFallbackView(ex);
        }

        MainWindow.Activate();
    }

    private static void EnsureDeviceIntegrationInitialized()
    {
        if (DeviceIntegration is not null)
        {
            return;
        }

        var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MicaAudio");
        var registryStore = new JsonDeviceRegistryStore(appDataRoot);
        DeviceIntegration = new DeviceIntegrationService(new DeviceServerHost(), registryStore);
        FirmwareService = new PrecompiledFirmwareService();
        DeviceOps = new DeviceOperationsCoordinator(DeviceIntegration);
        AppCatalog = new AppCatalogService(appDataRoot);
        AppModifierStore = new AppModifierStateStore(appDataRoot);
        CityAutocomplete = new CityAutocompleteService();
        AppDeployment = new AppDeploymentService(DeviceOps);
    }

    private static async Task StartDeviceIntegrationAsync()
    {
        if (DeviceIntegration is null)
        {
            return;
        }

        try
        {
            await DeviceIntegration.StartAsync().ConfigureAwait(false);
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
            DeviceOps = null;
            AppDeployment = null;
            AppCatalog = null;
            AppModifierStore = null;
            CityAutocomplete = null;
            FirmwareService = null;

            if (DeviceIntegration is not null)
            {
                await DeviceIntegration.DisposeAsync().ConfigureAwait(false);
                DeviceIntegration = null;
            }
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









