using App.WinUI.Services.Apps;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace App.WinUI.Views.Dialogs;

/// <summary>
/// ContentDialog that lets the user search GIPHY and pick a GIF.
/// Constructed entirely in code — no XAML backing file.
/// </summary>
internal sealed class GiphySearchDialog : ContentDialog
{
    private readonly GiphySearchService service;
    private readonly TextBox queryBox;
    private readonly GridView resultsGrid;
    private readonly ProgressRing progressRing;
    private readonly TextBlock statusText;

    private List<GiphyItem> currentResults = [];

    public GiphyItem? SelectedItem { get; private set; }

    public GiphySearchDialog(GiphySearchService service, XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(service);
        this.service = service;

        XamlRoot = xamlRoot;
        Title = "Buscar no GIPHY";
        PrimaryButtonText = string.Empty;
        CloseButtonText = "Fechar";
        DefaultButton = ContentDialogButton.Close;

        queryBox = new TextBox
        {
            PlaceholderText = "Ex: fire, space, rain…",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        queryBox.KeyDown += OnQueryBoxKeyDown;

        var searchButton = new Button { Content = "Buscar", Margin = new Thickness(8, 0, 0, 0) };
        searchButton.Click += OnSearchClicked;

        var searchRow = new Grid { ColumnSpacing = 0 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(queryBox, 0);
        Grid.SetColumn(searchButton, 1);
        searchRow.Children.Add(queryBox);
        searchRow.Children.Add(searchButton);

        progressRing = new ProgressRing
        {
            IsActive = false,
            Width = 32,
            Height = 32,
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        statusText = new TextBlock
        {
            Text = string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.75,
            Visibility = Visibility.Collapsed,
        };

        resultsGrid = new GridView
        {
            Margin = new Thickness(0, 12, 0, 0),
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = true,
            Height = 340,
        };
        resultsGrid.ItemClick += OnResultItemClick;
        resultsGrid.ItemTemplate = CreateItemTemplate();
        resultsGrid.ItemContainerStyle = BuildItemContainerStyle();

        var root = new StackPanel { Spacing = 4, Width = 480 };
        root.Children.Add(searchRow);
        root.Children.Add(progressRing);
        root.Children.Add(statusText);
        root.Children.Add(resultsGrid);

        Content = root;
    }

    private async void OnSearchClicked(object sender, RoutedEventArgs e) => await RunSearchAsync();
    private async void OnQueryBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await RunSearchAsync();
        }
    }

    private async Task RunSearchAsync()
    {
        var q = queryBox.Text.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return;
        }

        SetSearching(true);
        currentResults = [];
        resultsGrid.ItemsSource = null;
        statusText.Visibility = Visibility.Collapsed;

        var response = await service.SearchAsync(q, cancellationToken: default).ConfigureAwait(true);
        SetSearching(false);

        if (response is null)
        {
            ShowStatus("Busca falhou — verifique se o servidor está acessível e a chave GIPHY está configurada.");
            return;
        }

        currentResults = response.Items ?? [];

        if (currentResults.Count == 0)
        {
            ShowStatus($"Nenhum resultado para \"{q}\".");
            return;
        }

        resultsGrid.ItemsSource = currentResults;
    }

    private void OnResultItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GiphyItem item)
        {
            SelectedItem = item;
            Hide();
        }
    }

    private void SetSearching(bool searching)
    {
        progressRing.IsActive = searching;
        progressRing.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowStatus(string message)
    {
        statusText.Text = message;
        statusText.Visibility = Visibility.Visible;
    }

    private static DataTemplate CreateItemTemplate()
    {
        // Programmatic DataTemplate: a 120x90 Image + title label below it.
        // The GridView binds GiphyItem via x:Bind emulation — we build the UI in code.
        // We use a FrameworkTemplate loader workaround: build in ItemClick instead.
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <StackPanel Width="110" Margin="4">
                    <Border Width="110" Height="80" CornerRadius="6" Background="#1A1A2E">
                        <Image Source="{Binding PreviewUrl}" Stretch="UniformToFill"/>
                    </Border>
                    <TextBlock Text="{Binding Title}" TextTrimming="CharacterEllipsis"
                               MaxLines="2" TextWrapping="Wrap" FontSize="11"
                               Margin="0,4,0,0" Opacity="0.85"/>
                </StackPanel>
            </DataTemplate>
            """);
    }

    private static Style BuildItemContainerStyle()
    {
        var style = new Style(typeof(GridViewItem));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0)));
        return style;
    }
}
