using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class DevicesPage
{
    private const double PanelCornerRadius = 8d;
    private const double SectionCornerRadius = 8d;
    private const double WizardCardWidth = 560d;
    private const double WizardHorizontalMargin = 14d;
    private const double WizardCornerRadius = 10d;
    private const double WizardHeaderVerticalPadding = 14d;
    private const double WizardHeaderHorizontalPadding = 16d;
    private const double WizardBodyVerticalPadding = 14d;
    private const double WizardBodyHorizontalPadding = 16d;
    private const double WizardFooterVerticalPadding = 10d;
    private const double WizardFooterHorizontalPadding = 16d;
    private const double WizardStepHeight = 4d;
    private const double WizardStepGap = 8d;
    private const double WizardControlHeight = 34d;
    private const double DetailHeaderPadding = 24d;
    private const double MetricsGridGap = 16d;

    // DOCS: docs/wiki/guides/setup-new-device.md#passos
    private ListView DevicesList = null!;
    private Button NewDeviceButton = null!;
    private ColumnDefinition DevicesDetailsColumn = null!;
    private Grid DeviceDetailsGrid = null!;
    private WebView2 DeviceDashboardWebView = null!;
    private TextBlock SelectedDeviceTitleText = null!;
    private TextBlock SelectedDeviceSubtitleText = null!;
    private TextBlock SelectedDeviceRegistrationText = null!;
    private TextBlock SelectedDeviceAppText = null!;
    private TextBlock ServerInfoText = null!;
    private TextBlock SelectedDeviceSignalText = null!;
    private AppBarButton CopyDashboardLinkButton = null!;
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
    private TextBlock DashboardLoopLoadText = null!;
    private ProgressBar DashboardLoopLoadBar = null!;
    private TextBlock DashboardUptimeText = null!;
    private TextBlock DashboardHeapText = null!;
    private TextBlock DashboardHeapFragmentationText = null!;
    private ProgressBar DashboardHeapFragmentationBar = null!;
    private TextBlock DashboardPsramText = null!;
    private TextBlock DashboardPsramFragmentationText = null!;
    private ProgressBar DashboardPsramFragmentationBar = null!;
    private Border DashboardStatusSectionBorder = null!;
    private TextBlock DashboardNetworkText = null!;
    private TextBlock DashboardPortalStateText = null!;
    private TextBlock DashboardLastEventText = null!;
    private TextBlock DashboardAuxLedText = null!;
    private TextBlock StreamFramesReceivedText = null!;
    private TextBlock StreamFramesAppliedText = null!;
    private TextBlock StreamSuccessRateText = null!;
    private TextBlock StreamGapCountText = null!;
    private TextBlock StreamInvalidCountText = null!;
    private TextBlock StreamLastSequenceText = null!;
    private TextBox DeviceLogsTextBox = null!;
    private ScrollViewer? DeviceLogsScrollViewer;

    private InfoBar PairingCodeText = null!;
    private Button PairingCopyCodeButton = null!;
    private TextBlock WizardPairCodeText = null!;

    private Grid WizardOverlay = null!;
    private Border WizardCardBorder = null!;
    private StackPanel WizardPortPanel = null!;
    private ComboBox WizardPortComboBox = null!;
    private Button WizardRefreshPortsButton = null!;
    // DOCS: docs/wiki/guides/setup-new-device.md#logs-seriais-no-wizard
    private Button WizardDetailsToggleButton = null!;
    private StackPanel WizardDiagnosticsPanel = null!;
    private TextBlock WizardSerialStatusText = null!;
    private Button WizardRecaptureBootButton = null!;
    private Button WizardCopySerialLogsButton = null!;
    private Button WizardClearSerialLogsButton = null!;
    private ListView WizardSerialListView = null!;
    private TextBlock WizardSerialPlaceholderText = null!;
    private TextBlock WizardServerBaseAddressText = null!;
    private TextBlock WizardSummaryNoteText = null!;
    private TextBlock WizardStatusText = null!;
    private Grid WizardFlashProgressHost = null!;
    private ProgressBar WizardFlashProgressBar = null!;
    private TextBlock WizardFlashPercentText = null!;
    private Button WizardCloseButton = null!;
    private Button WizardFinishButton = null!;

    private void InitializeComponent()
    {
        var root = new Grid
        {
            Padding = new Thickness(8, 0, 8, 8),
            RowSpacing = 8,
            Background = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 11, 15, 20)),
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(BuildGlobalActionsBar());
        root.Children.Add(BuildInlineStatusBanner());

        var shell = new Grid { ColumnSpacing = 8 };
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        DevicesDetailsColumn = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        shell.ColumnDefinitions.Add(DevicesDetailsColumn);
        Grid.SetRow(shell, 2);

        shell.Children.Add(BuildDeviceListPanel());
        shell.Children.Add(BuildDetailsPanel());

        root.Children.Add(shell);
        root.Children.Add(BuildWizardOverlay());

        Content = root;
    }

    // DOCS: docs/wiki/reference/device-observability-dashboard.md
    private Border BuildGlobalActionsBar()
    {
        var host = CreatePanelBorder(new Thickness(4), cornerRadius: PanelCornerRadius, elevated: true);

        var commandBar = new CommandBar
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
            OverflowButtonVisibility = CommandBarOverflowButtonVisibility.Auto,
        };

        var downloadFirmwareButton = new AppBarButton
        {
            Label = "Baixar firmware",
            Icon = new SymbolIcon(Symbol.Download),
        };
        downloadFirmwareButton.Click += OnDownloadFirmwareClicked;

        var pairingButton = new AppBarButton
        {
            Label = "Parear",
            Icon = new SymbolIcon(Symbol.Add),
        };
        pairingButton.Click += OnGeneratePairingCodeClicked;

        var refreshButton = new AppBarButton
        {
            Label = "Atualizar",
            Icon = new SymbolIcon(Symbol.Refresh),
        };
        refreshButton.Click += OnRefreshClicked;

        CopyDashboardLinkButton = new AppBarButton
        {
            Label = "Copiar link do dashboard",
            Icon = new SymbolIcon(Symbol.Copy),
            IsEnabled = false,
        };
        CopyDashboardLinkButton.Click += OnCopyDashboardLinkClicked;

        var copyHostButton = new AppBarButton
        {
            Label = "Copiar host",
            Icon = new SymbolIcon(Symbol.Copy),
        };
        copyHostButton.Click += OnCopyHostClicked;

        commandBar.PrimaryCommands.Add(downloadFirmwareButton);
        commandBar.PrimaryCommands.Add(pairingButton);
        commandBar.PrimaryCommands.Add(refreshButton);
        commandBar.PrimaryCommands.Add(CopyDashboardLinkButton);
        commandBar.PrimaryCommands.Add(copyHostButton);

        host.Child = commandBar;
        return host;
    }

    // DOCS: docs/wiki/modules/app-winui.md
    // DOCS: docs/wiki/guides/setup-new-device.md#passos
    private InfoBar BuildInlineStatusBanner()
    {
        PairingCopyCodeButton = new Button
        {
            Content = "Copiar codigo",
            MinWidth = 132,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        PairingCopyCodeButton.Click += OnCopyPairingCodeClicked;

        PairingCodeText = new InfoBar
        {
            IsOpen = false,
            Visibility = Visibility.Collapsed,
            IsClosable = true,
            Severity = InfoBarSeverity.Informational,
            Message = "Pareamento: -",
            Margin = new Thickness(0, 0, 0, 8),
        };
        PairingCodeText.RegisterPropertyChangedCallback(InfoBar.IsOpenProperty, (_, _) =>
        {
            PairingCodeText.Visibility = PairingCodeText.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        });

        Grid.SetRow(PairingCodeText, 1);
        return PairingCodeText;
    }

    private Border BuildDeviceListPanel()
    {
        var leftCard = CreatePanelBorder(new Thickness(0), cornerRadius: PanelCornerRadius, elevated: false);

        var leftGrid = new Grid();
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        DevicesList = new ListView
        {
            Margin = new Thickness(8, 14, 8, 8),
        };
        DevicesList.SelectionChanged += OnDeviceSelectionChanged;
        Grid.SetRow(DevicesList, 0);
        leftGrid.Children.Add(DevicesList);

        var actionsHost = new Grid
        {
            Padding = new Thickness(10),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };

        NewDeviceButton = new Button
        {
            Content = BuildButtonWithGlyph("\uE710", "Novo dispositivo"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 36,
            Padding = new Thickness(12, 0, 12, 0),
        };
        NewDeviceButton.Click += OnNewDeviceClicked;
        actionsHost.Children.Add(NewDeviceButton);

        Grid.SetRow(actionsHost, 1);
        leftGrid.Children.Add(actionsHost);

        leftCard.Child = leftGrid;
        return leftCard;
    }
    private Grid BuildDetailsPanel()
    {
        DeviceDetailsGrid = new Grid
        {
            RowSpacing = 0,
            Visibility = Visibility.Collapsed,
        };
        Grid.SetColumn(DeviceDetailsGrid, 1);

        DeviceDashboardWebView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0),
        };

        var detailsHost = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
        };
        detailsHost.Children.Add(DeviceDashboardWebView);

        DeviceDetailsGrid.Children.Add(detailsHost);

        return DeviceDetailsGrid;
    }

    private ScrollViewer BuildDashboardDetailsHost()
    {
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var contentStack = new StackPanel
        {
            Padding = new Thickness(0, 0, 0, 16),
        };
        contentStack.Children.Add(BuildMetricsSection());
        contentStack.Children.Add(BuildStatusSection());

        scrollViewer.Content = contentStack;
        return scrollViewer;
    }

    private Border BuildDetailsHeader()
    {
        var header = new Border
        {
            Padding = new Thickness(DetailHeaderPadding),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 18, 24, 32)),
        };

        var titleRow = new Grid { ColumnSpacing = 16 };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var summary = new StackPanel { Spacing = 4 };
        SelectedDeviceTitleText = new TextBlock
        {
            Text = "Nenhum dispositivo selecionado",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        SelectedDeviceSubtitleText = new TextBlock { Text = "-", Opacity = 0.86, TextWrapping = TextWrapping.Wrap };
        SelectedDeviceRegistrationText = new TextBlock { Text = "-", Opacity = 0.82, TextWrapping = TextWrapping.Wrap };
        SelectedDeviceAppText = new TextBlock { Text = "App ativo: -", Opacity = 0.82, TextWrapping = TextWrapping.Wrap };
        ServerInfoText = new TextBlock { Text = "Servidor: iniciando...", Opacity = 0.72, TextWrapping = TextWrapping.Wrap };

        summary.Children.Add(SelectedDeviceTitleText);
        summary.Children.Add(SelectedDeviceSubtitleText);
        summary.Children.Add(SelectedDeviceRegistrationText);
        summary.Children.Add(SelectedDeviceAppText);
        summary.Children.Add(ServerInfoText);

        titleRow.Children.Add(summary);

        var actionStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Spacing = 8,
        };

        SelectedDeviceSignalText = new TextBlock
        {
            Text = "Sinal -",
            Opacity = 0.88,
            FontSize = 12,
        };

        var buttonsStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
        };

        TestLedButton = new AppBarButton
        {
            Label = "Testar LED",
            Icon = new SymbolIcon(Symbol.TouchPointer),
            MinWidth = 132,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        TestLedButton.Click += OnTestLedClicked;

        RemoveDeviceButton = new AppBarButton
        {
            Label = "Remover",
            Icon = new SymbolIcon(Symbol.Delete),
            MinWidth = 132,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        RemoveDeviceButton.Click += OnRemoveDeviceClicked;

        buttonsStack.Children.Add(TestLedButton);
        buttonsStack.Children.Add(RemoveDeviceButton);

        actionStack.Children.Add(SelectedDeviceSignalText);
        actionStack.Children.Add(buttonsStack);

        Grid.SetColumn(actionStack, 1);
        titleRow.Children.Add(actionStack);

        header.Child = titleRow;
        return header;
    }

    private Border BuildBrightnessSection()
    {
        var section = CreateSectionBorder();

        var stack = new StackPanel { Spacing = 10 };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = "Brilho do painel LED",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        DashboardBrightnessValueText = new TextBlock
        {
            Text = "120/160",
            Opacity = 0.84,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(DashboardBrightnessValueText, 1);
        header.Children.Add(DashboardBrightnessValueText);

        var sliderRow = new Grid { ColumnSpacing = 12 };
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        DashboardBrightnessSlider = new Slider
        {
            Minimum = 30,
            Maximum = 160,
            SmallChange = 1,
            StepFrequency = 1,
            Value = 160,
            IsEnabled = false,
            Height = WizardControlHeight,
        };
        DashboardBrightnessSlider.ValueChanged += OnBrightnessSliderValueChanged;
        DashboardBrightnessSlider.PointerCaptureLost += OnBrightnessSliderPointerCaptureLost;
        DashboardBrightnessSlider.LostFocus += OnBrightnessSliderLostFocus;
        sliderRow.Children.Add(DashboardBrightnessSlider);

        var sliderIcon = new FontIcon
        {
            Glyph = "\uE793",
            Opacity = 0.7,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(sliderIcon, 1);
        sliderRow.Children.Add(sliderIcon);

        DashboardBrightnessStatusText = new TextBlock
        {
            Text = "Brilho atual no painel: -",
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        };

        DashboardTelemetryHeartbeatText = new TextBlock
        {
            Text = "Sinal de vida: - | LED de teste: - | Intensidade do LED: -",
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };

        stack.Children.Add(header);
        stack.Children.Add(sliderRow);
        stack.Children.Add(DashboardBrightnessStatusText);
        stack.Children.Add(DashboardTelemetryHeartbeatText);

        section.Child = stack;
        return section;
    }

    private StackPanel BuildMetricsSection()
    {
        var wrapper = new StackPanel { Spacing = 0 };

        DashboardPlaceholderText = new TextBlock
        {
            Text = "Selecione um dispositivo para ver o status",
            Opacity = 0.74,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            Margin = new Thickness(24, 16, 24, 16),
        };
        wrapper.Children.Add(DashboardPlaceholderText);

        DashboardMetricsGrid = new Grid
        {
            ColumnSpacing = MetricsGridGap,
            Padding = new Thickness(24, 16, 24, 16),
            Visibility = Visibility.Collapsed,
        };

        DashboardMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        DashboardMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        DashboardMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        DashboardLoopTile = CreateDashboardTile(BuildMetricCard(
            "Uso do processador",
            out DashboardLoopLoadText,
            out DashboardLoopLoadBar,
            out var loopSub));
        DashboardLoopLoadBar.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
        loopSub.Text = string.Empty;
        Grid.SetColumn(DashboardLoopTile, 0);

        DashboardHeapTile = CreateDashboardTile(BuildMetricCard(
            "Memoria RAM livre",
            out DashboardHeapText,
            out DashboardHeapFragmentationBar,
            out DashboardHeapFragmentationText));
        DashboardHeapFragmentationBar.Foreground = new SolidColorBrush(Color.FromArgb(255, 78, 201, 78));
        Grid.SetColumn(DashboardHeapTile, 1);

        DashboardPsramTile = CreateDashboardTile(BuildMetricCard(
            "Memoria extra livre",
            out DashboardPsramText,
            out DashboardPsramFragmentationBar,
            out DashboardPsramFragmentationText));
        DashboardPsramFragmentationBar.Foreground = new SolidColorBrush(Color.FromArgb(255, 155, 89, 182));
        Grid.SetColumn(DashboardPsramTile, 2);

        DashboardMetricsGrid.Children.Add(DashboardLoopTile);
        DashboardMetricsGrid.Children.Add(DashboardHeapTile);
        DashboardMetricsGrid.Children.Add(DashboardPsramTile);

        wrapper.Children.Add(DashboardMetricsGrid);
        return wrapper;
    }

    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-dashboard-online-seguro
    private Border BuildStatusSection()
    {
        DashboardStatusSectionBorder = CreateSectionBorder();

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(new TextBlock
        {
            Text = "Status do dispositivo",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        var summary = new StackPanel { Spacing = 4 };
        DashboardNetworkText = BuildConnectivityItem("Wi-Fi:", "-");
        DashboardUptimeText = BuildConnectivityItem("Uptime:", "-");
        DashboardPortalStateText = BuildConnectivityItem("Modo configuracao:", "-");
        DashboardLastEventText = BuildConnectivityItem("Ultimo evento de rede:", "-");
        DashboardAuxLedText = BuildConnectivityItem("LED auxiliar:", "-");

        summary.Children.Add(DashboardNetworkText);
        summary.Children.Add(DashboardUptimeText);
        summary.Children.Add(DashboardPortalStateText);
        summary.Children.Add(DashboardLastEventText);
        summary.Children.Add(DashboardAuxLedText);

        stack.Children.Add(summary);
        stack.Children.Add(CreateDashboardTile(BuildStreamStatsTable()));

        DashboardStatusSectionBorder.Child = stack;
        DashboardStatusSectionBorder.Visibility = Visibility.Collapsed;
        return DashboardStatusSectionBorder;
    }

    private void ScrollDeviceLogsToOffset(bool scrollToEnd)
    {
        if (DeviceLogsTextBox is null)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            DeviceLogsScrollViewer ??= FindDescendant<ScrollViewer>(DeviceLogsTextBox);
            if (DeviceLogsScrollViewer is null)
            {
                return;
            }

            DeviceLogsScrollViewer.UpdateLayout();
            var targetOffset = scrollToEnd ? DeviceLogsScrollViewer.ScrollableHeight : 0d;
            DeviceLogsScrollViewer.ChangeView(null, targetOffset, null, true);
        });
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private Grid BuildWizardOverlay()
    {
        WizardOverlay = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(115, 0, 0, 0)),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false,
        };
        Grid.SetRowSpan(WizardOverlay, 3);

        WizardCardBorder = new Border
        {
            Width = WizardCardWidth,
            MaxWidth = WizardCardWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(WizardHorizontalMargin),
            CornerRadius = new CornerRadius(WizardCornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            Background = new SolidColorBrush(Color.FromArgb(255, 35, 38, 43)),
        };

        var cardGrid = new Grid();
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid
        {
            Padding = new Thickness(WizardHeaderHorizontalPadding, WizardHeaderVerticalPadding, WizardHeaderHorizontalPadding, WizardHeaderVerticalPadding),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "Novo dispositivo",
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(title);

        WizardCloseButton = CreateToolbarButton(BuildButtonWithGlyph("\uE8BB", string.Empty));
        WizardCloseButton.Width = 46;
        WizardCloseButton.Height = WizardControlHeight;
        WizardCloseButton.VerticalAlignment = VerticalAlignment.Center;
        WizardCloseButton.Padding = new Thickness(0);
        Grid.SetColumn(WizardCloseButton, 1);
        header.Children.Add(WizardCloseButton);

        cardGrid.Children.Add(header);

        var body = new StackPanel
        {
            Spacing = 10,
            Padding = new Thickness(WizardBodyHorizontalPadding, WizardBodyVerticalPadding, WizardBodyHorizontalPadding, WizardBodyVerticalPadding),
        };
        Grid.SetRow(body, 1);

        WizardPortPanel = new StackPanel
        {
            Spacing = 10,
        };
        WizardPortPanel.Children.Add(new TextBlock
        {
            Text = "Dispositivo USB (Porta COM)",
            FontSize = 12,
            Opacity = 0.86,
        });

        var comRow = new Grid { ColumnSpacing = 8 };
        comRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        WizardPortComboBox = new ComboBox
        {
            Height = WizardControlHeight,
            MinWidth = 300,
            DisplayMemberPath = "Label",
            PlaceholderText = "Selecione uma porta COM",
        };
        WizardPortComboBox.BorderThickness = new Thickness(1);
        WizardPortComboBox.BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81));
        WizardPortComboBox.Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
        comRow.Children.Add(WizardPortComboBox);

        WizardRefreshPortsButton = CreateToolbarButton(BuildButtonWithGlyph("\uE72C", "Atualizar portas"));
        WizardRefreshPortsButton.Height = WizardControlHeight;
        WizardRefreshPortsButton.Padding = new Thickness(12, 0, 12, 0);
        Grid.SetColumn(WizardRefreshPortsButton, 1);
        comRow.Children.Add(WizardRefreshPortsButton);

        WizardPortPanel.Children.Add(comRow);

        WizardSummaryNoteText = new TextBlock
        {
            Text = "Selecione a porta COM e clique em Concluir para gravar o firmware. O flash usa a COM primeiro; depois disso, o wizard reabre a mesma porta a 115200 para acompanhar o boot, permite copiar a sessao inteira e ajuda a diagnosticar quando o AP nao aparece.",
            Opacity = 0.72,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        WizardPortPanel.Children.Add(WizardSummaryNoteText);

        body.Children.Add(WizardPortPanel);

        var serverPanel = new StackPanel
        {
            Spacing = 10,
        };

        serverPanel.Children.Add(new TextBlock
        {
            Text = "Servidor local para preencher no portal AP",
            FontSize = 12,
            Opacity = 0.86,
        });

        var serverInfoPanel = new StackPanel
        {
            Spacing = 4,
        };
        serverInfoPanel.Children.Add(new TextBlock
        {
            Text = "Copie este valor para o campo Servidor do portal do ESP32",
            FontSize = 12,
            Opacity = 0.86,
        });

        WizardServerBaseAddressText = new TextBlock
        {
            Text = "-",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        };
        serverInfoPanel.Children.Add(WizardServerBaseAddressText);
        serverPanel.Children.Add(serverInfoPanel);

        body.Children.Add(serverPanel);

        var pairCodePanel = new StackPanel
        {
            Spacing = 4,
        };
        pairCodePanel.Children.Add(new TextBlock
        {
            Text = "Codigo de pareamento (pair code)",
            FontSize = 12,
            Opacity = 0.86,
        });

        WizardPairCodeText = new TextBlock
        {
            Text = "-",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Opacity = 0.9,
        };
        pairCodePanel.Children.Add(WizardPairCodeText);

        body.Children.Add(pairCodePanel);

        WizardStatusText = new TextBlock
        {
            Text = string.Empty,
            Opacity = 0.85,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        body.Children.Add(WizardStatusText);

        var diagnosticsHeader = new Grid { ColumnSpacing = 8 };
        diagnosticsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        diagnosticsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        diagnosticsHeader.Children.Add(new TextBlock
        {
            Text = "Diagnostico serial de boot",
            FontSize = 12,
            Opacity = 0.86,
            VerticalAlignment = VerticalAlignment.Center,
        });

        WizardDetailsToggleButton = CreateToolbarButton(BuildButtonWithGlyph("\uE712", "Ver mais"));
        WizardDetailsToggleButton.Height = WizardControlHeight;
        WizardDetailsToggleButton.Padding = new Thickness(10, 0, 10, 0);
        WizardDetailsToggleButton.MinWidth = 94;
        Grid.SetColumn(WizardDetailsToggleButton, 1);
        diagnosticsHeader.Children.Add(WizardDetailsToggleButton);

        body.Children.Add(diagnosticsHeader);

        WizardDiagnosticsPanel = new StackPanel
        {
            Spacing = 10,
            Visibility = Visibility.Collapsed,
        };

        WizardSerialStatusText = new TextBlock
        {
            Text = "O wizard vai assumir a porta serial depois do flash para mostrar o boot do ESP32-S3 e permitir copiar a sessao inteira de diagnostico.",
            Opacity = 0.82,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        WizardDiagnosticsPanel.Children.Add(WizardSerialStatusText);

        var diagnosticsActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };

        WizardRecaptureBootButton = CreateToolbarButton(BuildButtonWithGlyph("\uE777", "Recapturar boot"));
        WizardRecaptureBootButton.Height = WizardControlHeight;
        WizardRecaptureBootButton.Padding = new Thickness(10, 0, 10, 0);
        WizardRecaptureBootButton.MinWidth = 132;
        diagnosticsActions.Children.Add(WizardRecaptureBootButton);

        WizardCopySerialLogsButton = CreateToolbarButton(BuildButtonWithGlyph("\uE8C8", "Copiar logs"));
        WizardCopySerialLogsButton.Height = WizardControlHeight;
        WizardCopySerialLogsButton.Padding = new Thickness(10, 0, 10, 0);
        WizardCopySerialLogsButton.MinWidth = 118;
        diagnosticsActions.Children.Add(WizardCopySerialLogsButton);

        WizardClearSerialLogsButton = CreateToolbarButton(BuildButtonWithGlyph("\uE894", "Limpar"));
        WizardClearSerialLogsButton.Height = WizardControlHeight;
        WizardClearSerialLogsButton.Padding = new Thickness(10, 0, 10, 0);
        WizardClearSerialLogsButton.MinWidth = 92;
        diagnosticsActions.Children.Add(WizardClearSerialLogsButton);

        WizardDiagnosticsPanel.Children.Add(diagnosticsActions);

        var serialSurface = new Border
        {
            MinHeight = 180,
            MaxHeight = 240,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            Background = new SolidColorBrush(Color.FromArgb(255, 15, 18, 24)),
        };

        var serialHost = new Grid();
        WizardSerialListView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0),
            MinHeight = 180,
            MaxHeight = 240,
        };
        serialHost.Children.Add(WizardSerialListView);

        WizardSerialPlaceholderText = new TextBlock
        {
            Text = "Os logs seriais de boot vao aparecer aqui depois do flash. Use 'Copiar logs' para exportar a sessao inteira ou 'Recapturar boot' se nada chegar.",
            Margin = new Thickness(12),
            Opacity = 0.68,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        serialHost.Children.Add(WizardSerialPlaceholderText);

        serialSurface.Child = serialHost;
        WizardDiagnosticsPanel.Children.Add(serialSurface);
        body.Children.Add(WizardDiagnosticsPanel);

        WizardFlashProgressHost = new Grid
        {
            ColumnSpacing = 10,
            Visibility = Visibility.Collapsed,
        };
        WizardFlashProgressHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        WizardFlashProgressHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        WizardFlashProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        WizardFlashProgressHost.Children.Add(WizardFlashProgressBar);

        WizardFlashPercentText = new TextBlock
        {
            Text = "0%",
            Opacity = 0.9,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(WizardFlashPercentText, 1);
        WizardFlashProgressHost.Children.Add(WizardFlashPercentText);

        body.Children.Add(WizardFlashProgressHost);

        cardGrid.Children.Add(body);

        var footer = new Grid
        {
            Padding = new Thickness(WizardFooterHorizontalPadding, WizardFooterVerticalPadding, WizardFooterHorizontalPadding, WizardFooterVerticalPadding),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        Grid.SetRow(footer, 2);

        var footerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        WizardFinishButton = CreateToolbarButton(BuildButtonWithGlyph("\uE73E", "Concluir"));
        WizardFinishButton.MinWidth = 110;
        WizardFinishButton.Height = WizardControlHeight;

        footerActions.Children.Add(WizardFinishButton);
        footer.Children.Add(footerActions);

        cardGrid.Children.Add(footer);

        WizardCardBorder.Child = cardGrid;
        WizardOverlay.Children.Add(WizardCardBorder);

        return WizardOverlay;
    }

    private static StackPanel BuildMetricCard(string title, out TextBlock valueText, out ProgressBar progressBar, out TextBlock subText)
    {
        var stack = new StackPanel { Spacing = 8 };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.88,
        });

        valueText = new TextBlock
        {
            Text = "-",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            LineHeight = 30,
        };
        stack.Children.Add(valueText);

        progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 4,
        };
        stack.Children.Add(progressBar);

        subText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        };
        stack.Children.Add(subText);

        return stack;
    }
    private StackPanel BuildStreamStatsTable()
    {
        var host = new StackPanel { Spacing = 6 };

        host.Children.Add(new TextBlock
        {
            Text = "Envio de imagens ao painel",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.88,
        });

        var grid = new Grid { ColumnSpacing = 0 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { Spacing = 4 };
        left.Children.Add(BuildStreamRow("Imagens enviadas", out StreamFramesReceivedText));
        left.Children.Add(BuildStreamRow("Imagens exibidas", out StreamFramesAppliedText));
        left.Children.Add(BuildStreamRow("Taxa de sucesso", out StreamSuccessRateText));
        grid.Children.Add(left);

        var right = new StackPanel
        {
            Spacing = 4,
            Padding = new Thickness(12, 0, 0, 0),
            Margin = new Thickness(12, 0, 0, 0),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            BorderThickness = new Thickness(1, 0, 0, 0),
        };
        right.Children.Add(BuildStreamRow("Pacotes perdidos", out StreamGapCountText));
        right.Children.Add(BuildStreamRow("Imagens com erro", out StreamInvalidCountText));
        right.Children.Add(BuildStreamRow("Ultima imagem no", out StreamLastSequenceText));
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        host.Children.Add(grid);
        return host;
    }

    private static Border BuildStreamRow(string key, out TextBlock valueText)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock
        {
            Text = key,
            FontSize = 11.5,
            Opacity = 0.78,
        });

        valueText = new TextBlock
        {
            Text = "-",
            FontSize = 11.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 4, 0, 4),
            Child = row,
        };
    }

    private static TextBlock BuildConnectivityItem(string label, string value)
    {
        return new TextBlock
        {
            Text = label + " " + value,
            FontSize = 12,
            Opacity = 0.84,
            Margin = new Thickness(0, 2, 8, 2),
        };
    }

    private static Button CreateToolbarButton(UIElement content)
    {
        return new Button
        {
            Content = content,
            Background = ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42)),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Foreground = ResolveBrush("AppTextPrimaryBrush", Color.FromArgb(255, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 0, 16, 0),
        };
    }

    private static StackPanel BuildButtonWithGlyph(string glyph, string label)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };

        stack.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 13,
        });

        if (!string.IsNullOrWhiteSpace(label))
        {
            stack.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return stack;
    }

    private static Border CreateSectionBorder()
    {
        return new Border
        {
            Padding = new Thickness(24, 16, 24, 16),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            Background = ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 18, 24, 32)),
        };
    }

    private static Border CreatePanelBorder(Thickness padding, double cornerRadius, bool elevated)
    {
        return new Border
        {
            Padding = padding,
            CornerRadius = new CornerRadius(cornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            Background = elevated
                ? ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42))
                : ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 18, 24, 32)),
        };
    }

    private static Border CreateCard(UIElement content, bool elevated = false, double padding = 12)
    {
        var card = CreatePanelBorder(new Thickness(padding), cornerRadius: 12, elevated: elevated);
        card.Child = content;
        return card;
    }

    private static Border CreateDashboardTile(UIElement content)
    {
        return new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(SectionCornerRadius),
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
