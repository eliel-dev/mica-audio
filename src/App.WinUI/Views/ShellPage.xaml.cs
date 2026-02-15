using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

public sealed class ShellPage : Page
{
    private const string VisualizerTag = "visualizer";
    private const string DevicesTag = "devices";

    private readonly NavigationView rootNavigation;
    private readonly Frame contentFrame;
    private readonly DevicesPage devicesPage;

    private string currentTag = string.Empty;

    public ShellPage()
    {
        rootNavigation = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = false,
            PaneDisplayMode = NavigationViewPaneDisplayMode.LeftCompact,
        };

        rootNavigation.MenuItems.Add(new NavigationViewItem
        {
            Tag = VisualizerTag,
            Content = "Visualizador",
            Icon = new SymbolIcon(Symbol.Library),
        });

        rootNavigation.MenuItems.Add(new NavigationViewItem
        {
            Tag = DevicesTag,
            Content = "Dispositivos",
            Icon = new SymbolIcon(Symbol.AllApps),
        });

        devicesPage = new DevicesPage();

        contentFrame = new Frame();
        rootNavigation.Content = contentFrame;
        rootNavigation.SelectionChanged += OnNavigationSelectionChanged;

        Content = rootNavigation;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (rootNavigation.MenuItems.Count == 0)
        {
            return;
        }

        if (rootNavigation.SelectedItem is null)
        {
            rootNavigation.SelectedItem = rootNavigation.MenuItems[0];
        }

        if (string.IsNullOrWhiteSpace(currentTag))
        {
            ShowPage(VisualizerTag);
        }
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
        if (string.Equals(currentTag, tag, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentTag = tag;

        contentFrame.Content = string.Equals(tag, DevicesTag, StringComparison.OrdinalIgnoreCase)
            ? devicesPage
            : new MainPage();
    }
}
