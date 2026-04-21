using App.WinUI.Models.Apps;
using Windows.UI;
namespace App.WinUI.Views.Controls;
// DOCS: docs/wiki/guides/setup-new-device.md#tela-dispositivos
internal sealed class DeviceListRowControl : Grid
{
    private readonly AppPreviewThumbnailControl preview;
    private readonly TextBlock previewPlaceholder;
    private readonly TextBlock nameText;
    private string lastDeviceName = string.Empty;
    private string? lastPreviewAppId;
    private string lastPreviewPlaceholderText = string.Empty;
    private bool isPreviewVisible;
    private bool isSelected;
    public DeviceListRowControl()
    {
        Margin = new Thickness(0, 4, 0, 4);
        ColumnSpacing = 10;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var previewHost = new Grid
        {
            Width = 96,
            Height = 48,
        };
        preview = new AppPreviewThumbnailControl
        {
            Width = 96,
            Height = 48,
        };
        previewPlaceholder = new TextBlock
        {
            Text = "Sem app",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
            Foreground = UiResourceResolver.ResolveBrush("AppTextSecondaryBrush", Color.FromArgb(255, 180, 190, 205)),
        };
        previewHost.Children.Add(preview);
        previewHost.Children.Add(previewPlaceholder);
        Children.Add(previewHost);
        nameText = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(nameText, 1);
        Children.Add(nameText);
    }
    public void Bind(string name, AppCatalogItem? previewItem, string previewPlaceholderText)
    {
        if (!string.Equals(lastDeviceName, name, StringComparison.Ordinal))
        {
            nameText.Text = name;
            lastDeviceName = name;
        }
        var nextPreviewAppId = previewItem?.Id;
        if (string.IsNullOrWhiteSpace(nextPreviewAppId))
        {
            if (isPreviewVisible)
            {
                preview.Stop();
                preview.Visibility = Visibility.Collapsed;
                previewPlaceholder.Visibility = Visibility.Visible;
                isPreviewVisible = false;
            }
            if (!string.Equals(lastPreviewPlaceholderText, previewPlaceholderText, StringComparison.Ordinal))
            {
                previewPlaceholder.Text = previewPlaceholderText;
                lastPreviewPlaceholderText = previewPlaceholderText;
            }
            lastPreviewAppId = null;
            return;
        }
        if (!isPreviewVisible)
        {
            preview.Visibility = Visibility.Visible;
            previewPlaceholder.Visibility = Visibility.Collapsed;
            isPreviewVisible = true;
        }
        if (!string.Equals(lastPreviewAppId, nextPreviewAppId, StringComparison.OrdinalIgnoreCase))
        {
            preview.Bind(previewItem!);
            lastPreviewAppId = nextPreviewAppId;
        }
        preview.Start();
    }
    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
        {
            return;
        }
        isSelected = selected;
        preview.SetSelected(selected);
    }
    public void StartPreview()
    {
        preview.Start();
    }
    public void StopPreview()
    {
        preview.Stop();
    }
    public void SetRuntimeFrame(MicaAudio.Core.Presets.RgbaColor[]? frame)
    {
        preview.SetRuntimeFrame(frame);
    }

    public void SetPreviewConfig(IReadOnlyDictionary<string, string>? values)
    {
        preview.SetConfig(values);
    }
}
