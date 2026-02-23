using App.WinUI.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Graphics.Canvas.UI.Xaml;
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
    private Border GifRuntimePanel = null!;
    private Button GifOpenFileButton = null!;
    private TextBlock GifRuntimeStatusText = null!;
    private TextBlock GifFirmwareWarningText = null!;
    private CanvasControl GifRuntimeCanvas = null!;
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

        var topCommandBar = new CommandBar
        {
            Background = null,
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
        };

        var reload = new AppBarButton { Label = "Recarregar", Icon = new SymbolIcon(Symbol.Refresh) };
        reload.Click += OnReloadCatalogClicked;
        topCommandBar.PrimaryCommands.Add(reload);
        topCommandBar.PrimaryCommands.Add(new AppBarSeparator());

        SearchBox = new TextBox
        {
            PlaceholderText = "Buscar por nome ou categoria",
            MinWidth = 320,
        };
        SearchBox.TextChanged += OnSearchTextChanged;

        topCommandBar.PrimaryCommands.Add(new AppBarElementContainer { Content = SearchBox });
        root.Children.Add(CreateCard(topCommandBar, elevated: true, padding: 4));

        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        Grid.SetRow(content, 1);

        var catalogHost = new Grid { RowSpacing = 10 };
        catalogHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        catalogHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        catalogHost.Children.Add(new TextBlock
        {
            Text = "Catálogo de apps",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        });

        CatalogGrid = new GridView
        {
            IsItemClickEnabled = true,
            SelectionMode = ListViewSelectionMode.Single,
        };
        CatalogGrid.ItemClick += OnCatalogItemClick;
        CatalogGrid.SelectionChanged += OnCatalogSelectionChanged;

        Grid.SetRow(CatalogGrid, 1);
        catalogHost.Children.Add(CatalogGrid);

        content.Children.Add(CreateCard(catalogHost));

        var right = new Grid { RowSpacing = 10 };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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

        var deviceBar = new CommandBar
        {
            Background = null,
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
        };

        TargetDeviceCombo = new ComboBox
        {
            PlaceholderText = "Dispositivo online",
            MinWidth = 240,
        };
        TargetDeviceCombo.SelectionChanged += OnTargetDeviceSelectionChanged;

        deviceBar.PrimaryCommands.Add(new AppBarElementContainer { Content = TargetDeviceCombo });
        var deviceCard = CreateCard(deviceBar, padding: 4);
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

        var gifRuntimeHost = new StackPanel { Spacing = 8 };
        gifRuntimeHost.Children.Add(new TextBlock
        {
            Text = "Runtime GIF HUB75",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        });

        GifRuntimeStatusText = new TextBlock
        {
            Text = "Selecione o app GIF para iniciar.",
            Opacity = 0.9,
            TextWrapping = TextWrapping.Wrap,
        };

        GifOpenFileButton = new Button
        {
            Content = "Abrir arquivo GIF",
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        GifFirmwareWarningText = new TextBlock
        {
            Text = "Aviso: firmware antigo ignora frame RGB565 (tipo 2). O runtime local continua ativo.",
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap,
        };

        GifRuntimeCanvas = new CanvasControl
        {
            Height = 110,
        };
        GifRuntimeCanvas.Draw += OnGifRuntimeCanvasDraw;

        gifRuntimeHost.Children.Add(GifRuntimeStatusText);
        gifRuntimeHost.Children.Add(GifOpenFileButton);
        gifRuntimeHost.Children.Add(GifFirmwareWarningText);
        gifRuntimeHost.Children.Add(GifRuntimeCanvas);

        GifRuntimePanel = CreateCard(gifRuntimeHost);
        GifRuntimePanel.Visibility = Visibility.Collapsed;
        Grid.SetRow(GifRuntimePanel, 3);
        right.Children.Add(GifRuntimePanel);

        var actionsGrid = new Grid { ColumnSpacing = 8 };
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        InstallButton = new Button { Content = "Instalar" };
        InstallButton.Click += OnInstallClicked;

        SaveModifiersButton = new Button { Content = "Salvar" };
        SaveModifiersButton.Click += OnSaveModifiersClicked;

        Grid.SetColumn(SaveModifiersButton, 1);

        actionsGrid.Children.Add(InstallButton);
        actionsGrid.Children.Add(SaveModifiersButton);

        var actionsCard = CreateCard(actionsGrid);
        Grid.SetRow(actionsCard, 4);
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
        Grid.SetRow(statusCard, 5);
        right.Children.Add(statusCard);

        content.Children.Add(right);
        root.Children.Add(content);

        Content = root;
    }

    private static Border CreateCard(UIElement content, bool elevated = false, double padding = 12)
    {
        return new Border
        {
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            Background = elevated
                ? ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42))
                : ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 18, 24, 32)),
            Child = content,
        };
    }

    private static Brush ResolveBrush(string key, Color fallback)
    {
        return UiResourceResolver.ResolveBrush(key, fallback);
    }
}
