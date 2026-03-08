using App.WinUI.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
public sealed partial class AppsPage
{
    private TextBox SearchBox = null!;
    private GridView CatalogGrid = null!;
    private TextBlock SelectedAppNameText = null!;
    private TextBlock SelectedAppMetaText = null!;
    private TextBlock SelectedAppDescriptionText = null!;
    private ComboBox TargetDeviceCombo = null!;
    private ProgressRing OperationProgressRing = null!;
    private TextBlock OperationStatusText = null!;
    private TextBlock OperationPercentText = null!;
    private TextBlock ModifiersHintText = null!;
    private StackPanel ModifiersPanel = null!;
    private Button GifOpenFileButton = null!;
    private Button InstallButton = null!;
    private Button SaveModifiersButton = null!;

    private void InitializeComponent()
    {
        var root = new Grid
        {
            Padding = new Thickness(16),
            RowSpacing = 12,
            Background = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 11, 15, 20)),
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topHost = new StackPanel { Spacing = 10 };
        topHost.Children.Add(new TextBlock
        {
            Text = "Apps do sistema",
            Style = Application.Current.Resources["TitleTextBlockStyle"] as Style,
        });

        var searchGrid = new Grid { ColumnSpacing = 8 };
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        SearchBox = new TextBox
        {
            PlaceholderText = "Buscar apps por nome ou categoria",
            MinWidth = 320,
        };
        SearchBox.TextChanged += OnSearchTextChanged;

        var reloadButton = new Button
        {
            Content = "Recarregar",
            MinWidth = 110,
        };
        reloadButton.Click += OnReloadCatalogClicked;

        Grid.SetColumn(reloadButton, 1);
        searchGrid.Children.Add(SearchBox);
        searchGrid.Children.Add(reloadButton);

        topHost.Children.Add(searchGrid);
        root.Children.Add(topHost);

        var content = new Grid { ColumnSpacing = 14 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        Grid.SetRow(content, 1);

        var catalogHost = new Grid { RowSpacing = 8 };
        catalogHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        catalogHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        catalogHost.Children.Add(new TextBlock
        {
            Text = "Grade de apps",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
            Opacity = 0.9,
        });

        CatalogGrid = new GridView
        {
            IsItemClickEnabled = true,
            SelectionMode = ListViewSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        CatalogGrid.ItemClick += OnCatalogItemClick;
        CatalogGrid.SelectionChanged += OnCatalogSelectionChanged;

        Grid.SetRow(CatalogGrid, 1);
        catalogHost.Children.Add(CatalogGrid);

        content.Children.Add(catalogHost);

        var right = new Grid { RowSpacing = 10 };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(right, 1);

        var details = new StackPanel { Spacing = 4 };
        SelectedAppNameText = new TextBlock
        {
            Text = "Selecione um app",
            Style = Application.Current.Resources["TitleTextBlockStyle"] as Style,
        };
        SelectedAppMetaText = new TextBlock { Text = "-", Opacity = 0.82, TextWrapping = TextWrapping.Wrap };
        SelectedAppDescriptionText = new TextBlock { Text = "Nenhum app selecionado.", TextWrapping = TextWrapping.Wrap };
        details.Children.Add(SelectedAppNameText);
        details.Children.Add(SelectedAppMetaText);
        details.Children.Add(SelectedAppDescriptionText);
        right.Children.Add(CreateCard(details));

        TargetDeviceCombo = new ComboBox
        {
            PlaceholderText = "Dispositivo online",
            MinWidth = 220,
        };
        TargetDeviceCombo.SelectionChanged += OnTargetDeviceSelectionChanged;

        var deviceCard = CreateCard(TargetDeviceCombo);
        Grid.SetRow(deviceCard, 1);
        right.Children.Add(deviceCard);

        var modifiersHost = new StackPanel { Spacing = 8 };
        ModifiersHintText = new TextBlock
        {
            Text = "Modificadores do app selecionado.",
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        };

        ModifiersPanel = new StackPanel { Spacing = 10 };
        var modifierScroll = new ScrollViewer
        {
            MinHeight = 170,
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = ModifiersPanel,
        };

        modifiersHost.Children.Add(new TextBlock
        {
            Text = "Modificadores",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        });
        modifiersHost.Children.Add(ModifiersHintText);
        modifiersHost.Children.Add(modifierScroll);

        var modifiersCard = CreateCard(modifiersHost);
        Grid.SetRow(modifiersCard, 2);
        right.Children.Add(modifiersCard);

        var actionsGrid = new Grid { ColumnSpacing = 8 };
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        InstallButton = new Button { Content = "Instalar" };
        InstallButton.Click += OnInstallClicked;

        SaveModifiersButton = new Button { Content = "Salvar" };
        SaveModifiersButton.Click += OnSaveModifiersClicked;

        GifOpenFileButton = new Button
        {
            Content = "Selecionar GIF",
            Visibility = Visibility.Collapsed,
        };

        Grid.SetColumn(SaveModifiersButton, 1);
        Grid.SetColumn(GifOpenFileButton, 2);

        actionsGrid.Children.Add(InstallButton);
        actionsGrid.Children.Add(SaveModifiersButton);
        actionsGrid.Children.Add(GifOpenFileButton);

        var actionsCard = CreateCard(actionsGrid);
        Grid.SetRow(actionsCard, 3);
        right.Children.Add(actionsCard);

        var statusGrid = new Grid { ColumnSpacing = 10 };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        OperationProgressRing = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };

        OperationStatusText = new TextBlock
        {
            Text = "Operações: pronto",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        OperationPercentText = new TextBlock
        {
            Text = "0%",
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.82,
        };

        Grid.SetColumn(OperationStatusText, 1);
        Grid.SetColumn(OperationPercentText, 2);
        statusGrid.Children.Add(OperationProgressRing);
        statusGrid.Children.Add(OperationStatusText);
        statusGrid.Children.Add(OperationPercentText);

        var statusCard = CreateCard(statusGrid);
        Grid.SetRow(statusCard, 4);
        right.Children.Add(statusCard);

        content.Children.Add(right);
        root.Children.Add(content);

        Content = root;
    }

    private static Border CreateCard(UIElement content, double padding = 12)
    {
        return new Border
        {
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 55, 68, 86)),
            Background = ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 16, 22, 30)),
            Child = content,
        };
    }

    private static Brush ResolveBrush(string key, Color fallback)
    {
        return UiResourceResolver.ResolveBrush(key, fallback);
    }
}
