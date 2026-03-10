using App.WinUI.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MicaAudio.Core.Led;
using Windows.UI;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/paineis.md#galeria-de-paineis
public sealed partial class PanelsPage
{
    private Grid RootLayout = null!;
    private Grid GalleryHeaderGrid = null!;
    private Grid EditorHeaderGrid = null!;
    private StackPanel GalleryHeaderActionsPanel = null!;
    private StackPanel EditorHeaderActionsPanel = null!;
    private FrameworkElement GalleryHeader = null!;
    private FrameworkElement EditorHeader = null!;
    private FrameworkElement GalleryView = null!;
    private FrameworkElement EditorView = null!;
    private Grid EditorContentLayout = null!;
    private FrameworkElement WidgetLibraryPane = null!;
    private FrameworkElement CanvasPane = null!;
    private FrameworkElement InspectorPane = null!;
    private GridView PanelsGalleryGrid = null!;
    private ListView WidgetLibraryList = null!;
    private Hub75PanelEditorControl EditorCanvas = null!;
    private ComboBox TargetDeviceCombo = null!;
    private TextBlock StatusTextBlock = null!;
    private Button NewPanelButton = null!;
    private Button EditorBackButton = null!;
    private Button EditorSaveButton = null!;
    private Button EditorDuplicateButton = null!;
    private Button EditorDeleteButton = null!;
    private TextBox EditorNameText = null!;
    private TextBlock WidgetInspectorTitle = null!;
    private TextBlock WidgetModifiersHintText = null!;
    private StackPanel WidgetModifiersPanel = null!;
    private Button DeleteWidgetButton = null!;
    private Button GifSourceButton = null!;
    private TextBox GifSourcePathText = null!;
    private Border WidgetSourceCard = null!;

    private void InitializeComponent()
    {
        RootLayout = new Grid
        {
            Padding = new Thickness(16),
            RowSpacing = 10,
            Background = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 11, 15, 20)),
        };
        RootLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RootLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RootLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        GalleryHeader = BuildGalleryHeader();
        EditorHeader = BuildEditorHeader();
        Grid.SetRow(GalleryHeader, 0);
        Grid.SetRow(EditorHeader, 0);
        RootLayout.Children.Add(GalleryHeader);
        RootLayout.Children.Add(EditorHeader);

        StatusTextBlock = new TextBlock
        {
            Text = "Paineis: pronto",
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2),
        };
        Grid.SetRow(StatusTextBlock, 1);
        RootLayout.Children.Add(StatusTextBlock);

        GalleryView = BuildGalleryView();
        EditorView = BuildEditorView();
        Grid.SetRow(GalleryView, 2);
        Grid.SetRow(EditorView, 2);
        RootLayout.Children.Add(GalleryView);
        RootLayout.Children.Add(EditorView);

        Content = RootLayout;
    }

    private Grid BuildGalleryHeader()
    {
        GalleryHeaderGrid = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 12,
        };
        GalleryHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        GalleryHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        GalleryHeaderGrid.Children.Add(new TextBlock
        {
            Text = "Paineis",
            Style = Application.Current.Resources["TitleTextBlockStyle"] as Style,
            VerticalAlignment = VerticalAlignment.Center,
        });

        GalleryHeaderActionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        TargetDeviceCombo = new ComboBox
        {
            PlaceholderText = "Dispositivo online",
            MinWidth = 240,
        };
        TargetDeviceCombo.SelectionChanged += OnTargetDeviceSelectionChanged;
        GalleryHeaderActionsPanel.Children.Add(TargetDeviceCombo);

        NewPanelButton = CreatePageButton("Novo painel", OnNewPanelClicked, isPrimary: true);
        GalleryHeaderActionsPanel.Children.Add(NewPanelButton);

        Grid.SetColumn(GalleryHeaderActionsPanel, 1);
        GalleryHeaderGrid.Children.Add(GalleryHeaderActionsPanel);
        return GalleryHeaderGrid;
    }

    private Grid BuildEditorHeader()
    {
        EditorHeaderGrid = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 12,
            Visibility = Visibility.Collapsed,
        };
        EditorHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        EditorHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titlePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };

        EditorBackButton = CreatePageButton("Voltar", OnEditorBackClicked);
        titlePanel.Children.Add(EditorBackButton);

        EditorNameText = new TextBox
        {
            Text = "Editor",
            MinWidth = 260,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Background = null,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };
        EditorNameText.TextChanged += OnPanelNameChanged;
        titlePanel.Children.Add(EditorNameText);
        EditorHeaderGrid.Children.Add(titlePanel);

        EditorHeaderActionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        EditorSaveButton = CreatePageButton("Salvar", OnSavePanelClicked, isPrimary: true);
        EditorDuplicateButton = CreatePageButton("Duplicar", OnDuplicatePanelClicked);
        EditorDeleteButton = CreatePageButton("Excluir", OnDeletePanelClicked);
        EditorHeaderActionsPanel.Children.Add(EditorSaveButton);
        EditorHeaderActionsPanel.Children.Add(EditorDuplicateButton);
        EditorHeaderActionsPanel.Children.Add(EditorDeleteButton);

        Grid.SetColumn(EditorHeaderActionsPanel, 1);
        EditorHeaderGrid.Children.Add(EditorHeaderActionsPanel);
        return EditorHeaderGrid;
    }

    private GridView BuildGalleryView()
    {
        PanelsGalleryGrid = new GridView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0, 4, 0, 20),
            Margin = new Thickness(-6, 0, -6, 0),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(PanelsGalleryGrid, ScrollBarVisibility.Disabled);
        ScrollViewer.SetHorizontalScrollMode(PanelsGalleryGrid, ScrollMode.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(PanelsGalleryGrid, ScrollBarVisibility.Auto);
        return PanelsGalleryGrid;
    }

    private Grid BuildEditorView()
    {
        EditorContentLayout = new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 12,
            Visibility = Visibility.Collapsed,
        };

        WidgetLibraryPane = BuildWidgetLibraryPane();
        CanvasPane = BuildCanvasPane();
        InspectorPane = BuildInspectorPane();

        EditorContentLayout.Children.Add(WidgetLibraryPane);
        EditorContentLayout.Children.Add(CanvasPane);
        EditorContentLayout.Children.Add(InspectorPane);
        return EditorContentLayout;
    }

    private Border BuildWidgetLibraryPane()
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "Widgets",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Arraste um app do catalogo atual para o canvas HUB75.",
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        });

        WidgetLibraryList = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            CanDragItems = true,
            AllowDrop = false,
            MinHeight = 260,
        };
        WidgetLibraryList.DragItemsStarting += OnWidgetLibraryDragItemsStarting;
        stack.Children.Add(WidgetLibraryList);
        return CreateCard(stack);
    }

    private Border BuildCanvasPane()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Canvas HUB75",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        });

        EditorCanvas = new Hub75PanelEditorControl
        {
            AllowDrop = true,
            MinHeight = 420,
        };
        EditorCanvas.DragOver += OnCanvasDragOver;
        EditorCanvas.Drop += OnCanvasDrop;
        EditorCanvas.WidgetSelected += OnEditorWidgetSelected;
        EditorCanvas.WidgetBoundsChanged += OnEditorWidgetBoundsChanged;
        stack.Children.Add(EditorCanvas);
        return CreateCard(stack, padding: 12, elevated: true);
    }

    private StackPanel BuildInspectorPane()
    {
        var host = new StackPanel { Spacing = 12 };
        host.Children.Add(BuildWidgetInspectorCard());
        return host;
    }

    private Border BuildWidgetInspectorCard()
    {
        var stack = new StackPanel { Spacing = 8 };
        WidgetInspectorTitle = new TextBlock
        {
            Text = "Widget",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        };
        stack.Children.Add(WidgetInspectorTitle);

        DeleteWidgetButton = new Button
        {
            Content = "Remover widget",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        DeleteWidgetButton.Click += OnDeleteWidgetClicked;
        stack.Children.Add(DeleteWidgetButton);

        WidgetSourceCard = CreateCard(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Fonte do GIF",
                    Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
                },
                (GifSourcePathText = new TextBox
                {
                    IsReadOnly = true,
                    PlaceholderText = "Nenhum arquivo ou pasta selecionado.",
                }),
                (GifSourceButton = new Button
                {
                    Content = "Selecionar arquivo",
                    HorizontalAlignment = HorizontalAlignment.Left,
                }),
            },
        });
        GifSourceButton.Click += OnGifSourceButtonClicked;
        WidgetSourceCard.Visibility = Visibility.Collapsed;
        stack.Children.Add(WidgetSourceCard);

        WidgetModifiersHintText = new TextBlock
        {
            Text = "Selecione um widget para editar a configuracao.",
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        };
        stack.Children.Add(WidgetModifiersHintText);

        WidgetModifiersPanel = new StackPanel { Spacing = 10 };
        stack.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = WidgetModifiersPanel,
            MinHeight = 240,
            MaxHeight = 420,
        });

        return CreateCard(stack, padding: 12, elevated: true);
    }
    private static Button CreatePageButton(string label, RoutedEventHandler handler, bool isPrimary = false)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 36,
            Padding = new Thickness(16, 8, 16, 8),
        };
        if (isPrimary)
        {
            button.Style = Application.Current.Resources["AccentButtonStyle"] as Style;
        }

        button.Click += handler;
        return button;
    }

    private static Border CreateCard(UIElement content, double padding = 12, bool elevated = false)
    {
        return new Border
        {
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 55, 68, 86)),
            Background = elevated
                ? ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 18, 24, 32))
                : ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 16, 22, 30)),
            Child = content,
        };
    }

    private static Brush ResolveBrush(string key, Color fallback)
    {
        return UiResourceResolver.ResolveBrush(key, fallback);
    }
}
