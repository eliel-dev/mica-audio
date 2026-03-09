using App.WinUI.Services.Devices;
using App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
public sealed partial class ShellPage : Page
{
    private const string VisualizerTag = "visualizer";
    private const string DevicesTag = "devices";
    private const string AppsTag = "apps";
    private const string MonitoringTag = "monitoring";
    private const string SettingsTag = "settings";

    private readonly ShellPageViewModel viewModel;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly ShellPageContentFactory contentFactory;

    private string currentTag = string.Empty;

    internal ShellPage(
        ShellPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        ShellPageContentFactory contentFactory)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.contentFactory = contentFactory;

        InitializeComponent();
        DataContext = viewModel;
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

        viewModel.CurrentTag = tag;
        App.RecordStartupBreadcrumb($"ShellPage.ShowPage({tag})");

        if (!contentFactory.TryResolve(tag, out var page, out var exception))
        {
            App.ReportStartupFailure($"ShellPage.ShowPage({tag}) failed", exception!);
            ContentFrame.Content = AppFailureViewFactory.Build(
                "Falha ao carregar a pagina.",
                "A shell permaneceu ativa. Voce pode navegar para outra aba enquanto verifica o log local.",
                exception!,
                App.CurrentCrashLogPath);
            return;
        }

        currentTag = tag;
        viewModel.CurrentTag = currentTag;
        ContentFrame.Content = page;

        if (string.Equals(tag, AppsTag, StringComparison.OrdinalIgnoreCase) && page is AppsPage appsPage)
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
        viewModel.ServerFooterText = $"Servidor: {baseAddress}";
        ServerFooterText.Text = viewModel.ServerFooterText;
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

