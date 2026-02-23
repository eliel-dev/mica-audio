using App.WinUI.Services.Devices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
public sealed partial class ShellPage : Page
{
    private const string VisualizerTag = "visualizer";
    private const string DevicesTag = "devices";
    private const string AppsTag = "apps";
    private const string ServerTag = "server";

    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly MainPage mainPage;
    private readonly DevicesPage devicesPage;
    private readonly AppsPage appsPage;
    private readonly ServerPage serverPage;

    private string currentTag = string.Empty;

    public ShellPage(IServiceProvider services)
        : this(
            services.GetRequiredService<DeviceOperationsCoordinator>(),
            services.GetRequiredService<MainPage>(),
            services.GetRequiredService<DevicesPage>(),
            services.GetRequiredService<AppsPage>(),
            services.GetRequiredService<ServerPage>())
    {
    }

    internal ShellPage(
        DeviceOperationsCoordinator deviceOps,
        MainPage mainPage,
        DevicesPage devicesPage,
        AppsPage appsPage,
        ServerPage serverPage)
    {
        this.deviceOps = deviceOps;
        this.mainPage = mainPage;
        this.devicesPage = devicesPage;
        this.appsPage = appsPage;
        this.serverPage = serverPage;

        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (RootNavigation.MenuItems.Count == 0)
        {
            return;
        }

        if (RootNavigation.SelectedItem is null)
        {
            RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        }

        if (string.IsNullOrWhiteSpace(currentTag))
        {
            ShowPage(VisualizerTag);
        }

        deviceOps.StateChanged += OnDeviceOpsStateChanged;
        UpdateServerFooter();

        App.ShellChromeVisibilityChanged += OnShellChromeVisibilityChanged;
        ApplyShellChromeVisibility(App.IsShellChromeHidden);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.ShellChromeVisibilityChanged -= OnShellChromeVisibilityChanged;
        deviceOps.StateChanged -= OnDeviceOpsStateChanged;
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.SelectedItemContainer?.Tag as string;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        // DOCS: docs/wiki/architecture/02-runtime-lifecycle.md#navegacao
        if (string.Equals(currentTag, tag, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentTag = tag;
        ContentFrame.Content = tag.ToLowerInvariant() switch
        {
            DevicesTag => devicesPage,
            AppsTag => appsPage,
            ServerTag => serverPage,
            _ => mainPage,
        };

        if (string.Equals(tag, AppsTag, StringComparison.OrdinalIgnoreCase))
        {
            _ = appsPage.ReloadCatalogFromDiskAsync();
        }
    }

    private void OnDeviceOpsStateChanged(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(UpdateServerFooter);
    }

    private void UpdateServerFooter()
    {
        var baseAddress = deviceOps.GetServerBaseAddress();
        ServerFooterText.Text = $"Servidor: {baseAddress}";
    }

    private void OnShellChromeVisibilityChanged(bool hideChrome)
    {
        _ = DispatcherQueue.TryEnqueue(() => ApplyShellChromeVisibility(hideChrome));
    }

    private void ApplyShellChromeVisibility(bool hideChrome)
    {
        RootNavigation.IsPaneVisible = !hideChrome;
        RootNavigation.IsPaneToggleButtonVisible = !hideChrome;

        if (hideChrome)
        {
            RootNavigation.IsPaneOpen = false;
        }

        ServerFooterText.Visibility = hideChrome ? Visibility.Collapsed : Visibility.Visible;
    }
}
