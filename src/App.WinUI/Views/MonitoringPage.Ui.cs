using App.WinUI.Services.Monitoring;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
public sealed partial class MonitoringPage
{
    private TextBlock SourceStatusText = null!;
    private TextBlock SourceHintText = null!;
    private TextBlock LastRefreshText = null!;
    private TextBlock SensorCountText = null!;
    private Grid KpiGrid = null!;
    private KpiTileView[] KpiTiles = [];

    private void InitializeComponent()
    {
        var root = new Grid
        {
            Background = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 11, 15, 20)),
            Padding = new Thickness(16),
            RowSpacing = 12,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(BuildHeaderBar());

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scroll, 1);
        scroll.Content = BuildKpisSection();
        root.Children.Add(scroll);

        Content = root;
    }

    private Border BuildHeaderBar()
    {
        var border = CreateCard(padding: 12, elevated: true);
        var host = new Grid { RowSpacing = 10, ColumnSpacing = 16 };
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusStack = new StackPanel { Spacing = 4 };
        SourceStatusText = new TextBlock
        {
            Text = "HWiNFO64: carregando...",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
            TextWrapping = TextWrapping.Wrap,
        };
        SourceHintText = new TextBlock
        {
            Text = "Preparando a leitura dos sensores locais do PC.",
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };
        statusStack.Children.Add(SourceStatusText);
        statusStack.Children.Add(SourceHintText);
        host.Children.Add(statusStack);

        LastRefreshText = BuildMetaTextBlock("Ultima leitura: -");
        Grid.SetColumn(LastRefreshText, 1);
        host.Children.Add(LastRefreshText);

        SensorCountText = BuildMetaTextBlock("Sensores: 0 | Leituras: 0");
        Grid.SetColumn(SensorCountText, 2);
        host.Children.Add(SensorCountText);

        border.Child = host;
        return border;
    }

    private Border BuildKpisSection()
    {
        var section = CreateCard(padding: 12);
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Painel de hardware",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
        });

        KpiGrid = new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 12,
        };

        KpiTiles =
        [
            CreateKpiTile(),
            CreateKpiTile(),
            CreateKpiTile(),
            CreateKpiTile(),
            CreateKpiTile(),
            CreateKpiTile(),
        ];

        foreach (var tile in KpiTiles)
        {
            KpiGrid.Children.Add(tile.Root);
        }

        stack.Children.Add(KpiGrid);
        section.Child = stack;
        return section;
    }

    private static KpiTileView CreateKpiTile()
    {
        var title = new TextBlock
        {
            Text = "Card",
            Opacity = 0.84,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        var context = new TextBlock
        {
            Text = "Contexto do hardware",
            Opacity = 0.66,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };

        var metricsHost = new StackPanel { Spacing = 10 };
        KpiMetricRowView[] rows =
        [
            CreateKpiMetricRow(),
            CreateKpiMetricRow(),
        ];

        foreach (var row in rows)
        {
            metricsHost.Children.Add(row.Root);
        }

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(title);
        stack.Children.Add(context);
        stack.Children.Add(metricsHost);

        return new KpiTileView(CreateCard(stack, padding: 14, elevated: true), title, context, rows);
    }

    private static KpiMetricRowView CreateKpiMetricRow()
    {
        var label = new TextBlock
        {
            Text = "Rotulo",
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.9,
        };

        var hardware = new TextBlock
        {
            Text = "Hardware",
            FontSize = 11,
            Opacity = 0.66,
            TextWrapping = TextWrapping.Wrap,
        };

        var value = new TextBlock
        {
            Text = "Indisponivel",
            FontSize = 23,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextWrapping = TextWrapping.Wrap,
        };

        var topRow = new Grid { ColumnSpacing = 12 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftStack = new StackPanel { Spacing = 2 };
        leftStack.Children.Add(label);
        leftStack.Children.Add(hardware);
        topRow.Children.Add(leftStack);

        Grid.SetColumn(value, 1);
        topRow.Children.Add(value);

        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 4,
            Visibility = Visibility.Collapsed,
        };

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(topRow);
        stack.Children.Add(progress);

        return new KpiMetricRowView(
            new Border
            {
                Padding = new Thickness(0, 6, 0, 6),
                BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = stack,
            },
            label,
            hardware,
            value,
            progress);
    }

    private void ReflowKpiTiles(int columns)
    {
        KpiGrid.ColumnDefinitions.Clear();
        KpiGrid.RowDefinitions.Clear();

        for (var index = 0; index < columns; index++)
        {
            KpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rowCount = (int)Math.Ceiling(KpiTiles.Length / (double)columns);
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            KpiGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < KpiTiles.Length; index++)
        {
            Grid.SetColumn(KpiTiles[index].Root, index % columns);
            Grid.SetRow(KpiTiles[index].Root, index / columns);
        }
    }

    private static TextBlock BuildMetaTextBlock(string text)
    {
        return new TextBlock
        {
            Text = text,
            Opacity = 0.82,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static Border CreateCard(UIElement? content = null, double padding = 12, bool elevated = false)
    {
        return new Border
        {
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 49, 62, 81)),
            Background = elevated
                ? ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 32, 42))
                : ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 18, 24, 32)),
            Child = content,
        };
    }

    private static Brush ResolveBrush(string key, Color fallback)
        => UiResourceResolver.ResolveBrush(key, fallback);

    private sealed class KpiTileView
    {
        public KpiTileView(Border root, TextBlock title, TextBlock context, KpiMetricRowView[] metrics)
        {
            Root = root;
            Title = title;
            Context = context;
            Metrics = metrics;
        }

        public Border Root { get; }

        public TextBlock Title { get; }

        public TextBlock Context { get; }

        public KpiMetricRowView[] Metrics { get; }
    }

    private sealed class KpiMetricRowView
    {
        public KpiMetricRowView(Border root, TextBlock label, TextBlock hardware, TextBlock value, ProgressBar progress)
        {
            Root = root;
            Label = label;
            Hardware = hardware;
            Value = value;
            Progress = progress;
        }

        public Border Root { get; }

        public TextBlock Label { get; }

        public TextBlock Hardware { get; }

        public TextBlock Value { get; }

        public ProgressBar Progress { get; }
    }
}
