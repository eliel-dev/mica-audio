using App.WinUI.Services.Devices;
using App.WinUI.Services.Visualizer;
using App.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
public sealed partial class ShellPage : Page
{
    private const string VisualizerTag = "visualizer";
    private const string DevicesTag = "devices";
    private const string PanelsTag = "panels";
    private const string MonitoringTag = "monitoring";
    private const string SettingsTag = "settings";
    private const string VisualizerStudioTag = "visualizer-studio";

    private readonly ShellPageViewModel viewModel;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly ShellPageContentFactory contentFactory;
    private readonly VisualizerEditorNavigationCoordinator visualizerEditorNavigation;

    private string currentNavigationTag = VisualizerTag;
    private string currentRouteTag = string.Empty;

    internal ShellPage(
        ShellPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        ShellPageContentFactory contentFactory,
        VisualizerEditorNavigationCoordinator visualizerEditorNavigation)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.contentFactory = contentFactory;
        this.visualizerEditorNavigation = visualizerEditorNavigation;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (RootNavigation.MenuItems.Count == 0)
        {
            return;
        }

        if (RootNavigation.SelectedItem is null)
        {
            RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        }

        if (string.IsNullOrWhiteSpace(currentRouteTag))
        {
            await ShowPageAsync(VisualizerTag).ConfigureAwait(true);
        }

        deviceOps.StateChanged += OnDeviceOpsStateChanged;
        visualizerEditorNavigation.OpenVisualizerPresetEditorRequested += OnOpenVisualizerPresetEditorRequested;
        visualizerEditorNavigation.ReturnToVisualizerRequested += OnReturnToVisualizerRequested;
        UpdateServerFooter();

        App.ShellChromeVisibilityChanged += OnShellChromeVisibilityChanged;
        ApplyShellChromeVisibility(App.IsShellChromeHidden);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.ShellChromeVisibilityChanged -= OnShellChromeVisibilityChanged;
        deviceOps.StateChanged -= OnDeviceOpsStateChanged;
        visualizerEditorNavigation.OpenVisualizerPresetEditorRequested -= OnOpenVisualizerPresetEditorRequested;
        visualizerEditorNavigation.ReturnToVisualizerRequested -= OnReturnToVisualizerRequested;
    }

    private async void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.SelectedItemContainer?.Tag as string;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (!await ShowPageAsync(tag).ConfigureAwait(true))
        {
            EnsureNavigationSelection(currentNavigationTag);
        }
    }

    private async Task<bool> ShowPageAsync(string tag, string? selectedNavigationTag = null, bool force = false)
    {
        // DOCS: docs/wiki/architecture/02-runtime-lifecycle.md#navegacao
        var normalizedRouteTag = NormalizeTag(tag);
        var normalizedNavigationTag = NormalizeTag(selectedNavigationTag ?? normalizedRouteTag);

        if (!force && string.Equals(currentRouteTag, normalizedRouteTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!await CanLeaveCurrentContentAsync(normalizedRouteTag).ConfigureAwait(true))
        {
            return false;
        }

        viewModel.CurrentTag = normalizedNavigationTag;
        App.RecordStartupBreadcrumb($"ShellPage.ShowPage({normalizedRouteTag})");

        if (!contentFactory.TryResolve(normalizedRouteTag, out var page, out var exception))
        {
            App.ReportStartupFailure($"ShellPage.ShowPage({normalizedRouteTag}) failed", exception!);
            ContentFrame.Content = AppFailureViewFactory.Build(
                "Falha ao carregar a pagina.",
                "A shell permaneceu ativa. Voce pode navegar para outra aba enquanto verifica o log local.",
                exception!,
                App.CurrentCrashLogPath);
            return false;
        }

        currentNavigationTag = normalizedNavigationTag;
        currentRouteTag = normalizedRouteTag;
        viewModel.CurrentTag = normalizedNavigationTag;
        ContentFrame.Content = page;
        return true;
    }

    private async void OnOpenVisualizerPresetEditorRequested(object? sender, OpenVisualizerPresetEditorRequest request)
    {
        if (!contentFactory.TryResolve(VisualizerStudioTag, out var page, out var exception))
        {
            App.ReportStartupFailure("ShellPage.ShowPage(visualizer-studio) failed", exception!);
            return;
        }

        if (page is VisualizerStudioPage studioPage)
        {
            studioPage.PrepareSession(request.PresetId);
        }

        if (await ShowPageAsync(VisualizerStudioTag, VisualizerTag, force: true).ConfigureAwait(true))
        {
            EnsureNavigationSelection(VisualizerTag);
        }
    }

    private async void OnReturnToVisualizerRequested(object? sender, ReturnToVisualizerRequest request)
    {
        if (contentFactory.Resolve(VisualizerTag) is MainPage mainPage)
        {
            mainPage.PrepareReturnFromEditor(request.PresetId, request.ReloadPresets);
        }

        if (await ShowPageAsync(VisualizerTag, VisualizerTag, force: true).ConfigureAwait(true))
        {
            EnsureNavigationSelection(VisualizerTag);
        }
    }

    private void EnsureNavigationSelection(string tag)
    {
        var item = RootNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Tag as string, tag, StringComparison.OrdinalIgnoreCase));

        if (item is not null && !ReferenceEquals(RootNavigation.SelectedItem, item))
        {
            RootNavigation.SelectedItem = item;
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

    private static string NormalizeTag(string tag)
        => string.IsNullOrWhiteSpace(tag) ? VisualizerTag : tag.Trim().ToLowerInvariant();

    private async Task<bool> CanLeaveCurrentContentAsync(string nextRouteTag)
    {
        if (string.IsNullOrWhiteSpace(currentRouteTag)
            || string.Equals(currentRouteTag, nextRouteTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ContentFrame.Content is not IShellNavigationGuard guard)
        {
            return true;
        }

        return await guard.CanLeaveAsync().ConfigureAwait(true);
    }
}
