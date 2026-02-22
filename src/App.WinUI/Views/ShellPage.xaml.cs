using App.WinUI.Services.Devices;
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

    private readonly MainPage mainPage = new();
    private readonly DevicesPage devicesPage = new();
    private readonly AppsPage appsPage = new();
    private readonly ServerPage serverPage = new();

    private string currentTag = string.Empty;

    public ShellPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => App.DeviceOps;

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

        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged += OnDeviceOpsStateChanged;
            UpdateServerFooter();
        }

        App.ShellChromeVisibilityChanged += OnShellChromeVisibilityChanged;
        ApplyShellChromeVisibility(App.IsShellChromeHidden);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.ShellChromeVisibilityChanged -= OnShellChromeVisibilityChanged;

        if (DeviceOps is null)
        {
            return;
        }

        DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
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
        var baseAddress = DeviceOps?.GetServerBaseAddress() ?? "http://127.0.0.1:5272";
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


