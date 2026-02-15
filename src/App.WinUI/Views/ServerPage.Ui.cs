using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class ServerPage
{
    private TextBlock ServerInfoText = null!;
    private TextBlock BuildStatusText = null!;
    private ProgressRing BuildProgressRing = null!;
    private TextBlock BuildPercentText = null!;
    private TextBox LogsTextBox = null!;

    private void InitializeComponent()
    {
        var root = new Grid
        {
            Padding = new Thickness(16),
            RowSpacing = 12,
            Background = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 11, 15, 20)),
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topBar = new CommandBar
        {
            Background = null,
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
        };

        var buildStable = new AppBarButton { Label = "Build stable", Icon = new SymbolIcon(Symbol.Play) };
        buildStable.Click += OnBuildStableClicked;
        var buildDma = new AppBarButton { Label = "Build dma_exp", Icon = new SymbolIcon(Symbol.Play) };
        buildDma.Click += OnBuildDmaClicked;
        var openFolder = new AppBarButton { Label = "Abrir pasta", Icon = new SymbolIcon(Symbol.OpenFile) };
        openFolder.Click += OnOpenFirmwareFolderClicked;
        var copyHost = new AppBarButton { Label = "Copiar host", Icon = new SymbolIcon(Symbol.Copy) };
        copyHost.Click += OnCopyHostClicked;
        var refresh = new AppBarButton { Label = "Atualizar", Icon = new SymbolIcon(Symbol.Refresh) };
        refresh.Click += OnRefreshClicked;

        topBar.PrimaryCommands.Add(buildStable);
        topBar.PrimaryCommands.Add(buildDma);
        topBar.PrimaryCommands.Add(new AppBarSeparator());
        topBar.PrimaryCommands.Add(openFolder);
        topBar.PrimaryCommands.Add(copyHost);
        topBar.PrimaryCommands.Add(refresh);

        root.Children.Add(CreateCard(topBar, elevated: true, padding: 4));

        var statusGrid = new Grid { ColumnSpacing = 12 };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusStack = new StackPanel { Spacing = 4 };
        ServerInfoText = new TextBlock
        {
            Text = "Servidor: inicializando...",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
            TextWrapping = TextWrapping.Wrap,
        };
        BuildStatusText = new TextBlock { Text = "Build: pronto", TextWrapping = TextWrapping.Wrap };
        statusStack.Children.Add(ServerInfoText);
        statusStack.Children.Add(BuildStatusText);

        var progressStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        BuildProgressRing = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = false,
            Visibility = Visibility.Collapsed,
        };
        BuildPercentText = new TextBlock { Text = "0%" };
        progressStack.Children.Add(BuildProgressRing);
        progressStack.Children.Add(BuildPercentText);

        Grid.SetColumn(progressStack, 1);
        statusGrid.Children.Add(statusStack);
        statusGrid.Children.Add(progressStack);

        var statusCard = CreateCard(statusGrid);
        Grid.SetRow(statusCard, 1);
        root.Children.Add(statusCard);

        LogsTextBox = new TextBox
        {
            Header = "Logs de servidor e build",
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 260,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(LogsTextBox, ScrollBarVisibility.Auto);

        var logsCard = CreateCard(LogsTextBox);
        Grid.SetRow(logsCard, 2);
        root.Children.Add(logsCard);

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

