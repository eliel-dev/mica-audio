using App.WinUI.Services.Visualizer;
using App.WinUI.Views;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicaAudio.Core.Presets;
using Windows.UI;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/visual-win2d.md#galeria-de-presets
internal sealed class PresetGalleryCardControl : UserControl
{
    private readonly Border frame;
    private readonly TextBlock titleText;
    private readonly PresetPreviewThumbnailControl preview;

    private bool isSelected;
    private bool isHovered;

    public PresetGalleryCardControl()
    {
        Width = 180;
        Height = 140;
        Margin = new Thickness(0, 0, 12, 12);
        IsTabStop = true;
        UseSystemFocusVisuals = true;

        frame = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 60, 70, 86)),
            Background = UiResourceResolver.ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 26, 34)),
        };

        var stack = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        preview = new PresetPreviewThumbnailControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(preview);

        titleText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxLines = 1,
        };
        stack.Children.Add(titleText);

        frame.Child = stack;
        Content = frame;

        GotFocus += (_, _) => SetHovered(true);
        LostFocus += (_, _) => SetHovered(false);
    }

    public PresetDefinition? Preset { get; private set; }

    public string PresetId => Preset?.PresetId ?? string.Empty;

    public void Bind(PresetDefinition value)
    {
        Preset = value;
        titleText.Text = value.Name;
        ToolTipService.SetToolTip(this, value.Name);
        ToolTipService.SetToolTip(titleText, value.Name);
        preview.Bind(value);
        UpdateVisualState();
    }

    public void ApplyPreviewSettings(PresetPreviewSettingsSnapshot settings)
    {
        preview.ApplyPreviewSettings(settings);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        preview.SetSelected(selected);
        UpdateVisualState();
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        UpdateVisualState();
    }

    public void StartPreview()
    {
        preview.StartPreview();
    }

    public void StopPreview()
    {
        preview.StopPreview();
    }

    private void UpdateVisualState()
    {
        frame.BorderBrush = isSelected
            ? UiResourceResolver.ResolveBrush("AppAccentBrush", Color.FromArgb(255, 36, 196, 113))
            : UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 60, 70, 86));

        frame.Background = isSelected
            ? UiResourceResolver.ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 24, 31, 40))
            : isHovered
                ? UiResourceResolver.ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 22, 28, 36))
                : UiResourceResolver.ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 26, 34));

        titleText.Opacity = isSelected ? 1f : isHovered ? 0.96f : 0.9f;
    }
}
