using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class DevicesPage
{
    // DOCS: docs/wiki/guides/setup-new-device.md#passos
    private ListView DevicesList = null!;
    private Button NewDeviceButton = null!;
    private ColumnDefinition DevicesDetailsColumn = null!;
    private Grid DeviceDetailsGrid = null!;
    private TextBlock SelectedDeviceTitleText = null!;
    private TextBlock SelectedDeviceSubtitleText = null!;
    private TextBlock SelectedDeviceRegistrationText = null!;
    private TextBlock SelectedDeviceAppText = null!;
    private TextBlock ServerInfoText = null!;
    private TextBlock SelectedDeviceSignalText = null!;
    private AppBarButton TestLedButton = null!;
    private AppBarButton RemoveDeviceButton = null!;
    private TextBlock DashboardPlaceholderText = null!;
    private Grid DashboardMetricsGrid = null!;
    private Slider DashboardBrightnessSlider = null!;
    private TextBlock DashboardBrightnessValueText = null!;
    private TextBlock DashboardBrightnessStatusText = null!;
    private TextBlock DashboardTelemetryHeartbeatText = null!;
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

        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var middle = new Grid { ColumnSpacing = 12 };
        middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        DevicesDetailsColumn = new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) };
        middle.ColumnDefinitions.Add(DevicesDetailsColumn);
        Grid.SetRow(middle, 0);

        var leftGrid = new Grid { RowSpacing = 10 };
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        DevicesList = new ListView();
        DevicesList.SelectionChanged += OnDeviceSelectionChanged;
        Grid.SetRow(DevicesList, 0);

        leftGrid.Children.Add(DevicesList);

        NewDeviceButton = new Button
        {
            Content = "Novo dispositivo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 40,
        };
        NewDeviceButton.Click += OnNewDeviceClicked;
        Grid.SetRow(NewDeviceButton, 1);
        leftGrid.Children.Add(NewDeviceButton);

        middle.Children.Add(CreateCard(leftGrid));

        DeviceDetailsGrid = new Grid { RowSpacing = 10 };
        DeviceDetailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DeviceDetailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DeviceDetailsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(DeviceDetailsGrid, 1);

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

        var summaryActionsStack = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };

        TestLedButton = new AppBarButton { Label = "Testar LED", Icon = new SymbolIcon(Symbol.TouchPointer) };
        TestLedButton.Click += OnTestLedClicked;

        RemoveDeviceButton = new AppBarButton { Label = "Remover", Icon = new SymbolIcon(Symbol.Delete) };
        RemoveDeviceButton.Click += OnRemoveDeviceClicked;

        summaryActionsStack.Children.Add(TestLedButton);
        summaryActionsStack.Children.Add(RemoveDeviceButton);

        // DOCS: docs/wiki/guides/setup-new-device.md#tela-dispositivos
        SelectedDeviceSignalText = new TextBlock
        {
            Text = "Sinal -",
            Opacity = 0.82,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Cascadia Mono"),
        };

        var summaryActionsPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        summaryActionsPanel.Children.Add(SelectedDeviceSignalText);
        summaryActionsPanel.Children.Add(summaryActionsStack);

        summaryLayout.Children.Add(summary);
        Grid.SetColumn(summaryActionsPanel, 1);
        summaryLayout.Children.Add(summaryActionsPanel);

        DeviceDetailsGrid.Children.Add(CreateCard(summaryLayout));

        var dashboardStack = new StackPanel { Spacing = 10 };

        var brightnessStack = new StackPanel { Spacing = 6 };
        var brightnessHeader = new Grid { ColumnSpacing = 8 };
        brightnessHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        brightnessHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        brightnessHeader.Children.Add(new TextBlock
        {
            Text = "Brilho do painel",
            Opacity = 0.82,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        DashboardBrightnessValueText = new TextBlock
        {
            Text = "160/255",
            Opacity = 0.82,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            FontFamily = new FontFamily("Cascadia Mono"),
        };
        Grid.SetColumn(DashboardBrightnessValueText, 1);
        brightnessHeader.Children.Add(DashboardBrightnessValueText);

        DashboardBrightnessSlider = new Slider
        {
            Minimum = 30,
            Maximum = 160,
            SmallChange = 1,
            StepFrequency = 1,
            Value = 160,
            IsEnabled = false,
        };
        DashboardBrightnessSlider.ValueChanged += OnBrightnessSliderValueChanged;
        DashboardBrightnessSlider.PointerCaptureLost += OnBrightnessSliderPointerCaptureLost;
        DashboardBrightnessSlider.LostFocus += OnBrightnessSliderLostFocus;

        DashboardBrightnessStatusText = new TextBlock
        {
            Text = "Brilho aplicado: - | Limite: -",
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap,
        };

        DashboardTelemetryHeartbeatText = new TextBlock
        {
            Text = "Heartbeat: -",
            Opacity = 0.74,
            FontFamily = new FontFamily("Cascadia Mono"),
        };

        brightnessStack.Children.Add(brightnessHeader);
        brightnessStack.Children.Add(DashboardBrightnessSlider);
        brightnessStack.Children.Add(DashboardBrightnessStatusText);
        brightnessStack.Children.Add(DashboardTelemetryHeartbeatText);
        dashboardStack.Children.Add(CreateDashboardTile(brightnessStack));

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
        Grid.SetRow(dashboardCard, 1);
        DeviceDetailsGrid.Children.Add(dashboardCard);

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
        logsCard.MinHeight = 280;
        Grid.SetRow(logsCard, 2);
        DeviceDetailsGrid.Children.Add(logsCard);

        middle.Children.Add(DeviceDetailsGrid);
        root.Children.Add(middle);

        PairingCodeText = new InfoBar
        {
            Severity = InfoBarSeverity.Informational,
            IsClosable = false,
            IsOpen = true,
            Message = "Pareamento: -",
        };
        Grid.SetRow(PairingCodeText, 1);
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

    private static Brush ResolveBrush(string key, Color fallback)
    {
        return UiResourceResolver.ResolveBrush(key, fallback);
    }
}

