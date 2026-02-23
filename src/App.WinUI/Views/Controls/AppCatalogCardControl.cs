using App.WinUI.Models.Apps;
using App.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace App.WinUI.Views.Controls;

internal sealed class AppCatalogCardControl : UserControl
{
    private readonly Border frame;
    private readonly TextBlock titleText;
    private readonly TextBlock summaryText;
    private readonly TextBlock categoryText;

    public AppCatalogCardControl(AppCatalogItem item)
    {
        Item = item;
        Width = 245;
        Margin = new Thickness(0, 0, 10, 10);

        frame = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 48, 67, 91)),
            Background = UiResourceResolver.ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 20, 27, 37)),
        };

        var stack = new StackPanel { Spacing = 6 };
        Preview = new AppPreviewThumbnailControl();
        Preview.Bind(item);
        stack.Children.Add(Preview);

        titleText = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Text = item.Name,
        };

        summaryText = new TextBlock
        {
            Text = item.Summary,
            Opacity = 0.84,
            TextWrapping = TextWrapping.Wrap,
        };

        categoryText = new TextBlock
        {
            Text = ToDisplayCategory(item.Category),
            Opacity = 0.7,
        };

        stack.Children.Add(titleText);
        stack.Children.Add(summaryText);
        stack.Children.Add(categoryText);

        frame.Child = stack;
        Content = frame;
    }

    public AppCatalogItem Item { get; }

    public AppPreviewThumbnailControl Preview { get; }

    public void SetSelectedVisual(bool selected)
    {
        Preview.SetSelected(selected);
        frame.BorderBrush = selected
            ? UiResourceResolver.ResolveBrush("AppAccentBrush", Color.FromArgb(255, 36, 196, 113))
            : UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 48, 67, 91));

        titleText.Opacity = selected ? 1 : 0.95;
        summaryText.Opacity = selected ? 0.92 : 0.84;
        categoryText.Opacity = selected ? 0.82 : 0.7;
    }

    public void SetPreviewConfig(IReadOnlyDictionary<string, string>? values)
    {
        Preview.SetConfig(values);
    }

    private static string ToDisplayCategory(string category)
    {
        return category.Trim().ToLowerInvariant() switch
        {
            "clima" => "Clima",
            "relogio" => "Relógio",
            "relógio" => "Relógio",
            "midia" => "Mídia",
            _ => category,
        };
    }
}
