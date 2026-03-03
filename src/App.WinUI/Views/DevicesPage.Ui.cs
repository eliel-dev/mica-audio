using App.WinUI.Views.Controls;
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
    private AppPreviewThumbnailControl SelectedDevicePreview = null!;
    private TextBlock SelectedDevicePreviewPlaceholderText = null!;
    private AppBarButton EnterProvisioningButton = null!;
    private AppBarButton RevokeButton = null!;
    private AppBarButton TestLedButton = null!;
    private AppBarButton RemoveDeviceButton = null!;
    private ProgressRing CommandProgressRing = null!;
    private TextBlock CommandStatusText = null!;
    private TextBlock CommandPercentText = null!;
    // DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#consumo-na-devicespage-entrega-3
    private TextBlock DashboardStatusText = null!;
    private TextBlock DashboardPlaceholderText = null!;
    private Grid DashboardMetricsGrid = null!;
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
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rightGrid, 1);

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
        rightGrid.Children.Add(CreateCard(summary));

        var previewStack = new StackPanel { Spacing = 8 };
        previewStack.Children.Add(new TextBlock
        {
            Text = "Preview do app",
            Opacity = 0.82,
        });

        var previewHost = new Grid
        {
            MinHeight = 120,
        };

        SelectedDevicePreview = new AppPreviewThumbnailControl
        {
            Width = 232,
            Height = 116,
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        SelectedDevicePreviewPlaceholderText = new TextBlock
        {
            Text = "Selecione um dispositivo para ver o app ativo",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 260,
        };

        previewHost.Children.Add(SelectedDevicePreview);
        previewHost.Children.Add(SelectedDevicePreviewPlaceholderText);
        previewStack.Children.Add(previewHost);

        var previewCard = CreateCard(previewStack);
        Grid.SetRow(previewCard, 1);
        rightGrid.Children.Add(previewCard);

        var actionsCommandBar = new CommandBar
        {
            Background = null,
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
        };

        EnterProvisioningButton = new AppBarButton { Label = "Provisioning", Icon = new SymbolIcon(Symbol.Setting) };
        EnterProvisioningButton.Click += OnEnterProvisioningClicked;
        RevokeButton = new AppBarButton { Label = "Revogar", Icon = new SymbolIcon(Symbol.Undo) };
        RevokeButton.Click += OnRevokeClicked;
        TestLedButton = new AppBarButton { Label = "Testar LED", Icon = new SymbolIcon(Symbol.TouchPointer) };
        TestLedButton.Click += OnTestLedClicked;
        RemoveDeviceButton = new AppBarButton { Label = "Remover", Icon = new SymbolIcon(Symbol.Delete) };
        RemoveDeviceButton.Click += OnRemoveDeviceClicked;

        actionsCommandBar.PrimaryCommands.Add(EnterProvisioningButton);
        actionsCommandBar.PrimaryCommands.Add(RevokeButton);
        actionsCommandBar.PrimaryCommands.Add(TestLedButton);
        actionsCommandBar.PrimaryCommands.Add(RemoveDeviceButton);

        var actionsCard = CreateCard(actionsCommandBar, padding: 4);
        Grid.SetRow(actionsCard, 2);
        rightGrid.Children.Add(actionsCard);

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
        Grid.SetRow(statusCard, 3);
        rightGrid.Children.Add(statusCard);

        var dashboardStack = new StackPanel { Spacing = 8 };

        var dashboardHeader = new Grid();
        dashboardHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dashboardHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dashboardTitle = new TextBlock
        {
            Text = "Dashboard ESP",
            Opacity = 0.84,
        };

        DashboardStatusText = new TextBlock
        {
            Text = "Sem metricas",
            Opacity = 0.72,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
        };

        Grid.SetColumn(DashboardStatusText, 1);
        dashboardHeader.Children.Add(dashboardTitle);
        dashboardHeader.Children.Add(DashboardStatusText);
        dashboardStack.Children.Add(dashboardHeader);

        DashboardPlaceholderText = new TextBlock
        {
            Text = "Selecione um dispositivo para ver metricas",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        };
        dashboardStack.Children.Add(DashboardPlaceholderText);

        DashboardMetricsGrid = new Grid
        {
            RowSpacing = 6,
            Visibility = Visibility.Collapsed,
        };

        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        DashboardMetricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        DashboardLoopLoadText = new TextBlock
        {
            Text = "Carga do loop: -",
            Opacity = 0.84,
        };

        DashboardLoopLoadBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 6,
        };
        Grid.SetRow(DashboardLoopLoadBar, 1);

        DashboardUptimeText = new TextBlock
        {
            Text = "Uptime: -",
            Opacity = 0.84,
        };
        Grid.SetRow(DashboardUptimeText, 2);

        DashboardHeapText = new TextBlock
        {
            Text = "Heap livre: - | Maior bloco: -",
            Opacity = 0.84,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(DashboardHeapText, 3);

        DashboardHeapFragmentationText = new TextBlock
        {
            Text = "Heap (maior bloco / livre): -",
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(DashboardHeapFragmentationText, 4);

        DashboardHeapFragmentationBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 6,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetRow(DashboardHeapFragmentationBar, 5);

        DashboardPsramText = new TextBlock
        {
            Text = "PSRAM: desconhecida",
            Opacity = 0.84,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(DashboardPsramText, 6);

        DashboardPsramFragmentationText = new TextBlock
        {
            Text = "PSRAM (maior bloco / livre): -",
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(DashboardPsramFragmentationText, 7);

        DashboardPsramFragmentationBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 6,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetRow(DashboardPsramFragmentationBar, 8);

        DashboardNetworkText = new TextBlock
        {
            Text = "Wi-Fi: -",
            Opacity = 0.84,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(DashboardNetworkText, 9);

        DashboardMetricsGrid.Children.Add(DashboardLoopLoadText);
        DashboardMetricsGrid.Children.Add(DashboardLoopLoadBar);
        DashboardMetricsGrid.Children.Add(DashboardUptimeText);
        DashboardMetricsGrid.Children.Add(DashboardHeapText);
        DashboardMetricsGrid.Children.Add(DashboardHeapFragmentationText);
        DashboardMetricsGrid.Children.Add(DashboardHeapFragmentationBar);
        DashboardMetricsGrid.Children.Add(DashboardPsramText);
        DashboardMetricsGrid.Children.Add(DashboardPsramFragmentationText);
        DashboardMetricsGrid.Children.Add(DashboardPsramFragmentationBar);
        DashboardMetricsGrid.Children.Add(DashboardNetworkText);
        dashboardStack.Children.Add(DashboardMetricsGrid);

        var dashboardCard = CreateCard(dashboardStack);
        Grid.SetRow(dashboardCard, 4);
        rightGrid.Children.Add(dashboardCard);

        DeviceLogsTextBox = new TextBox
        {
            Header = "Logs do dispositivo",
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 200,
            Text = "Selecione um dispositivo para ver logs do dispositivo.",
        };
        ScrollViewer.SetVerticalScrollBarVisibility(DeviceLogsTextBox, ScrollBarVisibility.Auto);

        var logsCard = CreateCard(DeviceLogsTextBox);
        Grid.SetRow(logsCard, 5);
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

    private static Brush ResolveBrush(string key, Color fallback)
    {
        return UiResourceResolver.ResolveBrush(key, fallback);
    }
}

