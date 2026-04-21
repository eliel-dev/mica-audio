using App.WinUI.Views.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-regra-global-de-scroll-canvas-first
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
    private ScrollViewer EditorBodyScrollViewer = null!;
    private Border EditorBodyContentHost = null!;
    private Grid EditorContentLayout = null!;
    private FrameworkElement WidgetLibraryPane = null!;
    private FrameworkElement CanvasPane = null!;
    private FrameworkElement InspectorPane = null!;
    private GridView PanelsGalleryGrid = null!;
    private ListView WidgetLibraryList = null!;
    private TextBox WidgetLibrarySearchBox = null!;
    private Hub75PanelEditorControl EditorCanvas = null!;
    private ComboBox TargetDeviceCombo = null!;
    private TextBlock StatusTextBlock = null!;
    private Button NewPanelButton = null!;
    private Button EditorBackButton = null!;
    private Button EditorSaveButton = null!;
    private Button EditorDuplicateButton = null!;
    private Button EditorDeleteButton = null!;
    private ToggleSwitch EditorPreviewToggle = null!;
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
        EditorPreviewToggle = new ToggleSwitch
        {
            Header = "Preview",
            IsOn = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        EditorPreviewToggle.Toggled += OnEditorPreviewToggleToggled;
        EditorSaveButton = CreatePageButton("Salvar", OnSavePanelClicked, isPrimary: true);
        EditorDuplicateButton = CreatePageButton("Duplicar", OnDuplicatePanelClicked);
        EditorDeleteButton = CreatePageButton("Excluir", OnDeletePanelClicked);
        EditorHeaderActionsPanel.Children.Add(EditorPreviewToggle);
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
        PanelsGalleryGrid.ItemsSource = panelGalleryItems;
        PanelsGalleryGrid.ItemTemplate = CreateGalleryItemTemplate();
        PanelsGalleryGrid.ItemContainerStyle = BuildGalleryItemContainerStyle();
        ScrollViewer.SetHorizontalScrollBarVisibility(PanelsGalleryGrid, ScrollBarVisibility.Disabled);
        ScrollViewer.SetHorizontalScrollMode(PanelsGalleryGrid, ScrollMode.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(PanelsGalleryGrid, ScrollBarVisibility.Auto);
        return PanelsGalleryGrid;
    }

    private ScrollViewer BuildEditorView()
    {
        EditorContentLayout = new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 12,
        };

        WidgetLibraryPane = BuildWidgetLibraryPane();
        CanvasPane = BuildCanvasPane();
        InspectorPane = BuildInspectorPane();

        EditorContentLayout.Children.Add(WidgetLibraryPane);
        EditorContentLayout.Children.Add(CanvasPane);
        EditorContentLayout.Children.Add(InspectorPane);

        EditorBodyContentHost = new Border
        {
            Padding = new Thickness(0, 0, 16, 0),
            Child = EditorContentLayout,
        };

        EditorBodyScrollViewer = new ScrollViewer
        {
            Visibility = Visibility.Collapsed,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Enabled,
            Content = EditorBodyContentHost,
        };

        return EditorBodyScrollViewer;
    }

    private Border BuildWidgetLibraryPane()
    {
        var layout = new Grid
        {
            RowSpacing = 10,
        };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "Widgets",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        };
        Grid.SetRow(title, 0);
        layout.Children.Add(title);

        WidgetLibraryList = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            CanDragItems = true,
            AllowDrop = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            MinHeight = 74,
            MaxHeight = 84,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemsPanel = (ItemsPanelTemplate)XamlReader.Load(
                """
                <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <ItemsStackPanel Orientation="Horizontal"/>
                </ItemsPanelTemplate>
                """),
            ItemContainerStyle = BuildWidgetRailItemContainerStyle(),
        };
        WidgetLibraryList.DragItemsStarting += OnWidgetLibraryDragItemsStarting;
        ScrollViewer.SetHorizontalScrollBarVisibility(WidgetLibraryList, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollMode(WidgetLibraryList, ScrollMode.Enabled);
        ScrollViewer.SetVerticalScrollBarVisibility(WidgetLibraryList, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollMode(WidgetLibraryList, ScrollMode.Disabled);
        Grid.SetRow(WidgetLibraryList, 1);
        layout.Children.Add(WidgetLibraryList);

        var card = CreateCard(layout, padding: 12, elevated: true);
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    private Border BuildCanvasPane()
    {
        var layout = new Grid
        {
            RowSpacing = 10,
        };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var title = new TextBlock
        {
            Text = "Canvas HUB75",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        };
        Grid.SetRow(title, 0);
        layout.Children.Add(title);

        EditorCanvas = new Hub75PanelEditorControl
        {
            AllowDrop = true,
            MinHeight = 320,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        EditorCanvas.DragOver += OnCanvasDragOver;
        EditorCanvas.Drop += OnCanvasDrop;
        EditorCanvas.WidgetSelected += OnEditorWidgetSelected;
        EditorCanvas.WidgetBoundsChanged += OnEditorWidgetBoundsChanged;
        EditorCanvas.MoveSelectedWidgetBackwardRequested += OnEditorMoveSelectedWidgetBackwardRequested;
        EditorCanvas.MoveSelectedWidgetForwardRequested += OnEditorMoveSelectedWidgetForwardRequested;
        EditorCanvas.CycleOverlappingWidgetRequested += OnEditorCycleOverlappingWidgetRequested;
        Grid.SetRow(EditorCanvas, 1);
        layout.Children.Add(EditorCanvas);

        var card = CreateCard(layout, padding: 8, elevated: true);
        card.VerticalAlignment = VerticalAlignment.Stretch;
        return card;
    }

    private Border BuildInspectorPane()
    {
        var card = BuildWidgetInspectorCard();
        card.VerticalAlignment = VerticalAlignment.Stretch;
        return card;
    }

    private Border BuildWidgetInspectorCard()
    {
        var layout = new Grid
        {
            RowSpacing = 8,
        };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        WidgetInspectorTitle = new TextBlock
        {
            Text = "Widget",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        };
        Grid.SetRow(WidgetInspectorTitle, 0);
        layout.Children.Add(WidgetInspectorTitle);

        var widgetActionsPanel = new StackPanel
        {
            Spacing = 8,
        };
        DeleteWidgetButton = new Button
        {
            Content = "Remover widget",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        DeleteWidgetButton.Click += OnDeleteWidgetClicked;
        widgetActionsPanel.Children.Add(DeleteWidgetButton);
        Grid.SetRow(DeleteWidgetButton, 1);
        Grid.SetRow(widgetActionsPanel, 1);
        layout.Children.Add(widgetActionsPanel);

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
        Grid.SetRow(WidgetSourceCard, 2);
        layout.Children.Add(WidgetSourceCard);

        WidgetModifiersHintText = new TextBlock
        {
            Text = "Selecione um widget para editar a configuracao.",
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(WidgetModifiersHintText, 3);
        layout.Children.Add(WidgetModifiersHintText);

        WidgetModifiersPanel = new StackPanel { Spacing = 10 };
        var modifiersScrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = WidgetModifiersPanel,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Grid.SetRow(modifiersScrollViewer, 4);
        layout.Children.Add(modifiersScrollViewer);

        return CreateCard(layout, padding: 12, elevated: true);
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

    private static Style BuildGalleryItemContainerStyle()
    {
        var style = new Style(typeof(GridViewItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0)));
        return style;
    }

    private static Style BuildWidgetRailItemContainerStyle()
    {
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 8, 0)));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 52d));
        return style;
    }

    private static DataTemplate CreateGalleryItemTemplate()
    {
        if (Application.Current.Resources.TryGetValue("PanelGalleryCardTemplate", out var resource)
            && resource is DataTemplate template)
        {
            return template;
        }

        throw new InvalidOperationException("PanelGalleryCardTemplate nao foi encontrado em Application.Resources.");
    }
}
