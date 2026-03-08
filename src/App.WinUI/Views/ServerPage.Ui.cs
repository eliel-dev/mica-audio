using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class ServerPage
{
    private TextBlock ServerInfoText = null!;
    private TextBlock DownloadStatusText = null!;
    private ProgressRing DownloadProgressRing = null!;
    private TextBlock DownloadPercentText = null!;
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

        var downloadDma = new AppBarButton { Label = "Baixar firmware", Icon = new SymbolIcon(Symbol.Download) };
        downloadDma.Click += OnDownloadDmaClicked;
        var copyHost = new AppBarButton { Label = "Copiar host", Icon = new SymbolIcon(Symbol.Copy) };
        copyHost.Click += OnCopyHostClicked;
        var refresh = new AppBarButton { Label = "Atualizar", Icon = new SymbolIcon(Symbol.Refresh) };
        refresh.Click += OnRefreshClicked;

        topBar.PrimaryCommands.Add(downloadDma);
        topBar.PrimaryCommands.Add(new AppBarSeparator());
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
        DownloadStatusText = new TextBlock { Text = "Download: pronto", TextWrapping = TextWrapping.Wrap };
        statusStack.Children.Add(ServerInfoText);
        statusStack.Children.Add(DownloadStatusText);

        var progressStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        DownloadProgressRing = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = false,
            Visibility = Visibility.Collapsed,
        };
        DownloadPercentText = new TextBlock { Text = "0%" };
        progressStack.Children.Add(DownloadProgressRing);
        progressStack.Children.Add(DownloadPercentText);

        Grid.SetColumn(progressStack, 1);
        statusGrid.Children.Add(statusStack);
        statusGrid.Children.Add(progressStack);

        var statusCard = CreateCard(statusGrid);
        Grid.SetRow(statusCard, 1);
        root.Children.Add(statusCard);

        LogsTextBox = new TextBox
        {
            Header = "Logs de servidor e download",
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
