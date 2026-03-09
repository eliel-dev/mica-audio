using App.WinUI.Models.Apps;
using App.WinUI.Views;
using MicaAudio.Core.Presets;
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
    private bool previewPlaybackActive;

    public AppCatalogCardControl(AppCatalogItem item)
    {
        Item = item;
        Width = 272;
        Margin = new Thickness(0, 0, 12, 12);

        frame = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 60, 70, 86)),
            Background = UiResourceResolver.ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 26, 34)),
        };

        var stack = new StackPanel { Spacing = 8 };
        Preview = new AppPreviewThumbnailControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 232,
            Height = 116,
        };
        Preview.Bind(item);
        stack.Children.Add(Preview);

        titleText = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Text = item.Name,
        };

        summaryText = new TextBlock
        {
            Text = item.Summary,
            Opacity = 0.9,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
        };

        categoryText = new TextBlock
        {
            Text = ToDisplayCategory(item.Category),
            Opacity = 0.72,
            TextAlignment = TextAlignment.Center,
        };

        stack.Children.Add(titleText);
        stack.Children.Add(summaryText);
        stack.Children.Add(categoryText);

        frame.Child = stack;
        Content = frame;

        PointerEntered += (_, _) =>
        {
            if (!previewPlaybackActive)
            {
                Preview.Start();
            }
        };
        PointerExited += (_, _) =>
        {
            if (!previewPlaybackActive)
            {
                Preview.Stop();
            }
        };
    }

    public AppCatalogItem Item { get; }

    public AppPreviewThumbnailControl Preview { get; }

    public void SetSelectedVisual(bool selected)
    {
        Preview.SetSelected(selected);
        frame.BorderBrush = selected
            ? UiResourceResolver.ResolveBrush("AppAccentBrush", Color.FromArgb(255, 36, 196, 113))
            : UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 60, 70, 86));

        frame.Background = selected
            ? UiResourceResolver.ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 31, 40))
            : UiResourceResolver.ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 26, 34));

        titleText.Opacity = selected ? 1 : 0.94;
        summaryText.Opacity = selected ? 0.95 : 0.9;
        categoryText.Opacity = selected ? 0.8 : 0.72;
    }

    public void SetPreviewConfig(IReadOnlyDictionary<string, string>? values)
    {
        Preview.SetConfig(values);
    }

    public void SetRuntimeFrame(RgbaColor[]? frame)
    {
        Preview.SetRuntimeFrame(frame);
    }

    public void SetPreviewPlayback(bool active)
    {
        previewPlaybackActive = active;
        if (active)
        {
            Preview.Start();
            return;
        }

        Preview.Stop();
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
