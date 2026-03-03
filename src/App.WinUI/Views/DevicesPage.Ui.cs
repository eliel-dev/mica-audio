using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class DevicesPage
{
    // DOCS: docs/wiki/guides/setup-new-device.md#passos
    private TextBox SearchBox = null!;
    private ListView DevicesList = null!;
    private TextBlock SelectedDeviceTitleText = null!;
    private TextBlock SelectedDeviceSubtitleText = null!;
    private TextBlock SelectedDeviceRegistrationText = null!;
    private TextBlock SelectedDeviceAppText = null!;
    private TextBlock ServerInfoText = null!;
    private AppBarButton TestLedButton = null!;
    private AppBarButton RemoveDeviceButton = null!;
    private ProgressRing CommandProgressRing = null!;
    private TextBlock CommandStatusText = null!;
    private TextBlock CommandPercentText = null!;
    // DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#consumo-na-devicespage-entrega-3
    private TextBlock DashboardStatusText = null!;
    private TextBlock DashboardPlaceholderText = null!;
    private Grid DashboardMetricsGrid = null!;
    private Border DashboardConnectionChipBorder = null!;
    private SymbolIcon DashboardConnectionChipIcon = null!;
    private TextBlock DashboardConnectionChipText = null!;
    private Border DashboardWifiChipBorder = null!;
    private SymbolIcon DashboardWifiChipIcon = null!;
    private TextBlock DashboardWifiChipText = null!;
    private Border DashboardRssiChipBorder = null!;
    private SymbolIcon DashboardRssiChipIcon = null!;
    private TextBlock DashboardRssiChipText = null!;
    private Border DashboardSnapshotChipBorder = null!;
    private SymbolIcon DashboardSnapshotChipIcon = null!;
    private TextBlock DashboardSnapshotChipText = null!;
    private Border DashboardLoopTile = null!;
    private Border DashboardHeapTile = null!;
    private Border DashboardPsramTile = null!;
    private Border DashboardNetworkTile = null!;
    private TextBlock DashboardLoopLoadText = null!;
    private ProgressBar DashboardLoopLoadBar = null!;
    private TextBlock DashboardUptimeText = null!;
    private TextBlock DashboardHeapText = null!;
    private TextBlock DashboardHeapFragmentationText = null!;
    private ProgressBar DashboardHeapFragmentationBar = null!;
    private TextBlock DashboardPsramText = null!;
    private TextBlock DashboardPsramFragmentationText = null!;
    private ProgressBar DashboardPsramFragmentationBar = null!;
    private TextBlock DashboardNetworkText = null!;
    private TextBlock DashboardLoopTrendCaptionText = null!;
    private Grid DashboardLoopTrendGrid = null!;
    private readonly List<Border> DashboardLoopTrendBars = new();
    private TextBox DeviceLogsTextBox = null!;
    private InfoBar PairingCodeText = null!;

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
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var topCommandBar = new CommandBar
        {
            Background = null,
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
        };

        var setupButton = new AppBarButton { Label = "Baixar firmware", Icon = new SymbolIcon(Symbol.Download) };
        setupButton.Click += OnDownloadFirmwareClicked;
        var pairButton = new AppBarButton { Label = "Parear", Icon = new SymbolIcon(Symbol.Add) };
        pairButton.Click += OnGeneratePairingCodeClicked;
        var refreshButton = new AppBarButton { Label = "Atualizar", Icon = new SymbolIcon(Symbol.Refresh) };
        refreshButton.Click += OnRefreshClicked;
        topCommandBar.PrimaryCommands.Add(setupButton);
        topCommandBar.PrimaryCommands.Add(pairButton);
        topCommandBar.PrimaryCommands.Add(refreshButton);

        root.Children.Add(CreateCard(topCommandBar, elevated: true, padding: 4));

        var middle = new Grid { ColumnSpacing = 12 };
        middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        Grid.SetRow(middle, 1);

        var leftGrid = new Grid { RowSpacing = 10 };
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        SearchBox = new TextBox
        {
            Header = "Buscar",
            PlaceholderText = "Nome, id, app ou status",
        };
        SearchBox.TextChanged += OnSearchTextChanged;

        DevicesList = new ListView();
        DevicesList.SelectionChanged += OnDeviceSelectionChanged;
        Grid.SetRow(DevicesList, 1);

        leftGrid.Children.Add(SearchBox);
        leftGrid.Children.Add(DevicesList);
        middle.Children.Add(CreateCard(leftGrid));

        var rightGrid = new Grid { RowSpacing = 10 };
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rightGrid, 1);

        var summaryLayout = new Grid { ColumnSpacing = 12 };
        summaryLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var summary = new StackPanel { Spacing = 4 };
        SelectedDeviceTitleText = new TextBlock
        {
            Text = "Nenhum dispositivo selecionado",
            Style = Application.Current.Resources["TitleTextBlockStyle"] as Style,
        };
        SelectedDeviceSubtitleText = new TextBlock { Text = "-", Opacity = 0.82 };
        SelectedDeviceRegistrationText = new TextBlock { Text = "-", Opacity = 0.78, TextWrapping = TextWrapping.Wrap };
        SelectedDeviceAppText = new TextBlock { Text = "App ativo: -", Opacity = 0.78 };
        ServerInfoText = new TextBlock { Text = "Servidor: iniciando...", Opacity = 0.78, TextWrapping = TextWrapping.Wrap };

        summary.Children.Add(SelectedDeviceTitleText);
        summary.Children.Add(SelectedDeviceSubtitleText);
        summary.Children.Add(SelectedDeviceRegistrationText);
        summary.Children.Add(SelectedDeviceAppText);
        summary.Children.Add(ServerInfoText);

        var summaryActionsBar = new CommandBar
        {
            Background = null,
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };

        TestLedButton = new AppBarButton { Label = "Testar LED", Icon = new SymbolIcon(Symbol.TouchPointer) };
        TestLedButton.Click += OnTestLedClicked;

        RemoveDeviceButton = new AppBarButton { Label = "Remover", Icon = new SymbolIcon(Symbol.Delete) };
        RemoveDeviceButton.Click += OnRemoveDeviceClicked;

        summaryActionsBar.PrimaryCommands.Add(TestLedButton);
        summaryActionsBar.PrimaryCommands.Add(RemoveDeviceButton);

        summaryLayout.Children.Add(summary);
        Grid.SetColumn(summaryActionsBar, 1);
        summaryLayout.Children.Add(summaryActionsBar);

        rightGrid.Children.Add(CreateCard(summaryLayout));

        var statusGrid = new Grid { ColumnSpacing = 10 };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        CommandProgressRing = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };

        CommandStatusText = new TextBlock
        {
            Text = "Comandos: pronto",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        CommandPercentText = new TextBlock
        {
            Text = "0%",
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.82,
        };

        Grid.SetColumn(CommandStatusText, 1);
        Grid.SetColumn(CommandPercentText, 2);

        statusGrid.Children.Add(CommandProgressRing);
        statusGrid.Children.Add(CommandStatusText);
        statusGrid.Children.Add(CommandPercentText);

        var statusCard = CreateCard(statusGrid);
        Grid.SetRow(statusCard, 1);
        rightGrid.Children.Add(statusCard);

        var dashboardStack = new StackPanel { Spacing = 10 };

        var dashboardHeader = new Grid { ColumnSpacing = 8 };
        dashboardHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dashboardHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        DashboardStatusText = new TextBlock
        {
            Text = "Sem metricas",
            Opacity = 0.82,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };

        Grid.SetColumn(DashboardStatusText, 1);
        dashboardHeader.Children.Add(DashboardStatusText);
        dashboardStack.Children.Add(dashboardHeader);

        var dashboardChipsGrid = new Grid
        {
            ColumnSpacing = 6,
            RowSpacing = 6,
        };
        dashboardChipsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dashboardChipsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dashboardChipsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dashboardChipsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        DashboardConnectionChipBorder = CreateDashboardChip(
            text: "Conexao indisponivel",
            iconSymbol: Symbol.Help,
            out var dashboardConnectionChipIcon,
            out var dashboardConnectionChipText);
        DashboardConnectionChipIcon = dashboardConnectionChipIcon;
        DashboardConnectionChipText = dashboardConnectionChipText;

        DashboardWifiChipBorder = CreateDashboardChip(
            text: "Wi-Fi indisponivel",
            iconSymbol: Symbol.Help,
            out var dashboardWifiChipIcon,
            out var dashboardWifiChipText);
        DashboardWifiChipIcon = dashboardWifiChipIcon;
        DashboardWifiChipText = dashboardWifiChipText;

        DashboardRssiChipBorder = CreateDashboardChip(
            text: "Sinal indisponivel",
            iconSymbol: Symbol.Help,
            out var dashboardRssiChipIcon,
            out var dashboardRssiChipText);
        DashboardRssiChipIcon = dashboardRssiChipIcon;
        DashboardRssiChipText = dashboardRssiChipText;

        DashboardSnapshotChipBorder = CreateDashboardChip(
            text: "Snapshot indisponivel",
            iconSymbol: Symbol.Help,
            out var dashboardSnapshotChipIcon,
            out var dashboardSnapshotChipText);
        DashboardSnapshotChipIcon = dashboardSnapshotChipIcon;
        DashboardSnapshotChipText = dashboardSnapshotChipText;

        Grid.SetRow(DashboardConnectionChipBorder, 0);
        Grid.SetColumn(DashboardConnectionChipBorder, 0);
        Grid.SetRow(DashboardWifiChipBorder, 0);
        Grid.SetColumn(DashboardWifiChipBorder, 1);
        Grid.SetRow(DashboardRssiChipBorder, 1);
        Grid.SetColumn(DashboardRssiChipBorder, 0);
        Grid.SetRow(DashboardSnapshotChipBorder, 1);
        Grid.SetColumn(DashboardSnapshotChipBorder, 1);

        dashboardChipsGrid.Children.Add(DashboardConnectionChipBorder);
        dashboardChipsGrid.Children.Add(DashboardWifiChipBorder);
        dashboardChipsGrid.Children.Add(DashboardRssiChipBorder);
        dashboardChipsGrid.Children.Add(DashboardSnapshotChipBorder);
        dashboardStack.Children.Add(dashboardChipsGrid);

        DashboardPlaceholderText = new TextBlock
        {
            Text = "Selecione um dispositivo para ver metricas",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.74,
        };
        dashboardStack.Children.Add(DashboardPlaceholderText);

        DashboardMetricsGrid = new Grid
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            Visibility = Visibility.Collapsed,
        };

        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        DashboardMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var loopTileStack = new StackPanel { Spacing = 4 };
        loopTileStack.Children.Add(new TextBlock
        {
            Text = "Carga do loop",
            Opacity = 0.74,
        });

        DashboardLoopLoadText = new TextBlock
        {
            Text = "-",
            FontSize = 25,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };

        DashboardLoopLoadBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 8,
        };

        loopTileStack.Children.Add(DashboardLoopLoadText);
        loopTileStack.Children.Add(DashboardLoopLoadBar);

        DashboardLoopTile = CreateDashboardTile(loopTileStack);
        Grid.SetRow(DashboardLoopTile, 0);
        Grid.SetColumn(DashboardLoopTile, 0);

        var heapTileStack = new StackPanel { Spacing = 4 };
        heapTileStack.Children.Add(new TextBlock
        {
            Text = "Heap",
            Opacity = 0.74,
        });

        DashboardHeapText = new TextBlock
        {
            Text = "Heap livre: - | Maior bloco: -",
            Opacity = 0.9,
            TextWrapping = TextWrapping.Wrap,
        };

        DashboardHeapFragmentationText = new TextBlock
        {
            Text = "Heap (maior bloco / livre): -",
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap,
        };

        DashboardHeapFragmentationBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 8,
            Visibility = Visibility.Collapsed,
        };

        heapTileStack.Children.Add(DashboardHeapText);
        heapTileStack.Children.Add(DashboardHeapFragmentationText);
        heapTileStack.Children.Add(DashboardHeapFragmentationBar);

        DashboardHeapTile = CreateDashboardTile(heapTileStack);
        Grid.SetRow(DashboardHeapTile, 0);
        Grid.SetColumn(DashboardHeapTile, 1);

        var psramTileStack = new StackPanel { Spacing = 4 };
        psramTileStack.Children.Add(new TextBlock
        {
            Text = "PSRAM",
            Opacity = 0.74,
        });

        DashboardPsramText = new TextBlock
        {
            Text = "PSRAM: desconhecida",
            Opacity = 0.9,
            TextWrapping = TextWrapping.Wrap,
        };

        DashboardPsramFragmentationText = new TextBlock
        {
            Text = "PSRAM (maior bloco / livre): -",
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap,
        };

        DashboardPsramFragmentationBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 8,
            Visibility = Visibility.Collapsed,
        };

        psramTileStack.Children.Add(DashboardPsramText);
        psramTileStack.Children.Add(DashboardPsramFragmentationText);
        psramTileStack.Children.Add(DashboardPsramFragmentationBar);

        DashboardPsramTile = CreateDashboardTile(psramTileStack);
        Grid.SetRow(DashboardPsramTile, 1);
        Grid.SetColumn(DashboardPsramTile, 0);

        var networkTileStack = new StackPanel { Spacing = 4 };
        networkTileStack.Children.Add(new TextBlock
        {
            Text = "Rede e uptime",
            Opacity = 0.74,
        });

        DashboardNetworkText = new TextBlock
        {
            Text = "Wi-Fi: -",
            Opacity = 0.9,
            TextWrapping = TextWrapping.Wrap,
        };

        DashboardUptimeText = new TextBlock
        {
            Text = "Uptime: -",
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap,
        };

        networkTileStack.Children.Add(DashboardNetworkText);
        networkTileStack.Children.Add(DashboardUptimeText);

        DashboardNetworkTile = CreateDashboardTile(networkTileStack);
        Grid.SetRow(DashboardNetworkTile, 1);
        Grid.SetColumn(DashboardNetworkTile, 1);

        DashboardMetricsGrid.Children.Add(DashboardLoopTile);
        DashboardMetricsGrid.Children.Add(DashboardHeapTile);
        DashboardMetricsGrid.Children.Add(DashboardPsramTile);
        DashboardMetricsGrid.Children.Add(DashboardNetworkTile);
        dashboardStack.Children.Add(DashboardMetricsGrid);

        var trendStack = new StackPanel { Spacing = 6 };
        DashboardLoopTrendCaptionText = new TextBlock
        {
            Text = "Tendencia carga do loop: aguardando amostras",
            Opacity = 0.76,
        };
        trendStack.Children.Add(DashboardLoopTrendCaptionText);

        DashboardLoopTrendGrid = new Grid
        {
            Height = 72,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        for (var index = 0; index < 24; index++)
        {
            DashboardLoopTrendGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var trendBar = new Border
            {
                Height = 4,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1, 0, 1, 0),
                CornerRadius = new CornerRadius(2),
                Opacity = 0.28,
                Background = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 65, 82, 104)),
            };
            Grid.SetColumn(trendBar, index);
            DashboardLoopTrendGrid.Children.Add(trendBar);
            DashboardLoopTrendBars.Add(trendBar);
        }

        trendStack.Children.Add(DashboardLoopTrendGrid);
        dashboardStack.Children.Add(CreateDashboardTile(trendStack));

        var dashboardCard = CreateCard(dashboardStack);
        Grid.SetRow(dashboardCard, 2);
        rightGrid.Children.Add(dashboardCard);

        DeviceLogsTextBox = new TextBox
        {
            Header = "Logs do dispositivo",
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono"),
            FontSize = 12,
            MinHeight = 220,
            Text = "Selecione um dispositivo para ver logs do dispositivo.",
        };
        ScrollViewer.SetVerticalScrollBarVisibility(DeviceLogsTextBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(DeviceLogsTextBox, ScrollBarVisibility.Auto);

        var logsCard = CreateCard(DeviceLogsTextBox);
        Grid.SetRow(logsCard, 3);
        rightGrid.Children.Add(logsCard);

        middle.Children.Add(rightGrid);
        root.Children.Add(middle);

        PairingCodeText = new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            IsClosable = false,
            IsOpen = true,
            Message = "Pareamento: -",
        };
        Grid.SetRow(PairingCodeText, 2);
        root.Children.Add(PairingCodeText);

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

    private static Border CreateDashboardTile(UIElement content)
    {
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            Background = ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42)),
            Child = content,
        };
    }

    private static Border CreateDashboardChip(string text, Symbol iconSymbol, out SymbolIcon icon, out TextBlock label)
    {
        icon = new SymbolIcon(iconSymbol)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };

        label = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.92,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        content.Children.Add(icon);
        content.Children.Add(label);

        return new Border
        {
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0),
            BorderBrush = null,
            Background = null,
            Child = content,
        };
    }

    private static Brush ResolveBrush(string key, Color fallback)
    {
        return UiResourceResolver.ResolveBrush(key, fallback);
    }
}

