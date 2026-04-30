using App.WinUI.Models.Apps;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
internal sealed class WidgetRailCardControl : UserControl
{
    private readonly Border frame;
    private readonly Border artTile;
    private readonly TextBlock titleText;
    private readonly TextBlock categoryText;
    private bool available = true;

    public WidgetRailCardControl(AppCatalogItem item, string displayName, string categoryLabel)
    {
        Item = item;
        DisplayName = displayName;
        CategoryLabel = categoryLabel;
        var visual = ResolveVisual(item, displayName);
        VisualKind = visual.Kind;
        VisualToken = visual.PrimaryToken;

        MinWidth = 196;
        MinHeight = 74;
        IsHitTestVisible = false;

        frame = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 60, 70, 86)),
            Background = UiResourceResolver.ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 26, 34)),
        };

        var layout = new Grid
        {
            ColumnSpacing = 12,
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        artTile = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 10, 13, 17)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 38, 46, 58)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Width = 96,
            Height = 48,
            VerticalAlignment = VerticalAlignment.Center,
            Child = BuildVisualSurface(visual),
        };
        Grid.SetColumn(artTile, 0);
        layout.Children.Add(artTile);

        var textStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };

        titleText = new TextBlock
        {
            Text = displayName,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        textStack.Children.Add(titleText);

        categoryText = new TextBlock
        {
            Text = categoryLabel,
            Opacity = 0.72,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        textStack.Children.Add(categoryText);

        Grid.SetColumn(textStack, 1);
        layout.Children.Add(textStack);

        frame.Child = layout;
        Content = frame;
    }

    public AppCatalogItem Item { get; }

    public string DisplayName { get; }

    public string CategoryLabel { get; }

    public string VisualKind { get; }

    public string VisualToken { get; }

    public void SetAvailability(bool isAvailable)
    {
        available = isAvailable;
        Opacity = isAvailable ? 1d : 0.68d;
        frame.Background = isAvailable
            ? UiResourceResolver.ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 26, 34))
            : UiResourceResolver.ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 30, 34, 42));
        artTile.Opacity = isAvailable ? 1d : 0.72d;
    }

    private static FrameworkElement BuildVisualSurface(WidgetRailVisualSpec visual)
    {
        return visual.Kind switch
        {
            "clock" => BuildClockSurface(visual),
            "audio" => BuildSpectrumSurface(),
            _ => BuildTokenSurface(visual),
        };
    }

    private static StackPanel BuildClockSurface(WidgetRailVisualSpec visual)
    {
        var stack = new StackPanel
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        stack.Children.Add(new TextBlock
        {
            Text = visual.PrimaryToken,
            FontSize = 15,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = CreateBrush(visual.PrimaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (!string.IsNullOrWhiteSpace(visual.SecondaryToken))
        {
            stack.Children.Add(new TextBlock
            {
                Text = visual.SecondaryToken,
                FontSize = 7,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = CreateBrush(visual.SecondaryColor ?? Color.FromArgb(255, 60, 220, 120)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.9,
            });
        }

        return stack;
    }

    private static StackPanel BuildSpectrumSurface()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var heights = new[] { 14d, 22d, 30d, 20d, 26d, 16d };
        var colors = new[]
        {
            Color.FromArgb(255, 255, 119, 119),
            Color.FromArgb(255, 255, 187, 64),
            Color.FromArgb(255, 255, 221, 71),
            Color.FromArgb(255, 86, 214, 110),
            Color.FromArgb(255, 116, 212, 255),
            Color.FromArgb(255, 171, 134, 255),
        };

        for (var i = 0; i < heights.Length; i++)
        {
            row.Children.Add(new Border
            {
                Width = 6,
                Height = heights[i],
                CornerRadius = new CornerRadius(2),
                Background = CreateBrush(colors[i]),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, Math.Max(0d, 30d - heights[i]), 0, 0),
            });
        }

        return row;
    }

    private static TextBlock BuildTokenSurface(WidgetRailVisualSpec visual)
    {
        return new TextBlock
        {
            Text = visual.PrimaryToken,
            FontSize = visual.PrimaryToken.Length > 4 ? 14 : 16,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = CreateBrush(visual.PrimaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static WidgetRailVisualSpec ResolveVisual(AppCatalogItem item, string displayName)
    {
        var previewKind = Normalize(item.Preview?.Kind);
        var category = Normalize(item.Category);

        if (previewKind is "clock" || category is "relogio" or "relógio")
        {
            return new WidgetRailVisualSpec(
                Kind: "clock",
                PrimaryToken: "15:43",
                PrimaryColor: Color.FromArgb(255, 64, 234, 255),
                SecondaryToken: "BRT",
                SecondaryColor: Color.FromArgb(255, 88, 221, 125));
        }

        if (previewKind is "gif" or "gifhub75" || category is "midia" or "mídia")
        {
            return new WidgetRailVisualSpec(
                Kind: "media",
                PrimaryToken: "GIF",
                PrimaryColor: Color.FromArgb(255, 219, 120, 255));
        }

        if (category == "clima")
        {
            return new WidgetRailVisualSpec(
                Kind: "weather",
                PrimaryToken: "CLM",
                PrimaryColor: Color.FromArgb(255, 202, 196, 255));
        }

        if (category is "finance" or "financas" or "finanças")
        {
            return new WidgetRailVisualSpec(
                Kind: "finance",
                PrimaryToken: "BTC",
                PrimaryColor: Color.FromArgb(255, 255, 204, 61));
        }

        if (category == "audio")
        {
            return new WidgetRailVisualSpec(
                Kind: "audio",
                PrimaryToken: string.Empty,
                PrimaryColor: Color.FromArgb(255, 255, 255, 255));
        }

        if (category == "esportes")
        {
            return new WidgetRailVisualSpec(
                Kind: "sports",
                PrimaryToken: "3-1",
                PrimaryColor: Color.FromArgb(255, 98, 255, 84));
        }

        if (previewKind == "text" || category == "texto")
        {
            return new WidgetRailVisualSpec(
                Kind: "text",
                PrimaryToken: "TXT",
                PrimaryColor: Color.FromArgb(255, 255, 72, 214));
        }

        return new WidgetRailVisualSpec(
            Kind: "generic",
            PrimaryToken: BuildFallbackToken(displayName, item.Id),
            PrimaryColor: Color.FromArgb(255, 220, 228, 239));
    }

    private static string BuildFallbackToken(string displayName, string itemId)
    {
        var candidate = string.IsNullOrWhiteSpace(displayName) ? itemId : displayName;
        var normalized = new string(candidate
            .Where(static ch => char.IsLetterOrDigit(ch))
            .Take(4)
            .ToArray())
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? "APP" : normalized;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        return new SolidColorBrush(color);
    }

    private readonly record struct WidgetRailVisualSpec(
        string Kind,
        string PrimaryToken,
        Color PrimaryColor,
        string? SecondaryToken = null,
        Color? SecondaryColor = null);
}
