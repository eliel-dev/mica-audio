using App.WinUI.Models.Apps;
using App.WinUI.Services.Devices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace App.WinUI.Views.Controls;
// DOCS: docs/wiki/guides/setup-new-device.md#tela-dispositivos
internal sealed class DeviceListRowControl : Grid
{
    private readonly AppPreviewThumbnailControl preview;
    private readonly TextBlock previewPlaceholder;
    private readonly Grid textHost;
    private readonly SymbolIcon stateIcon;
    private readonly TextBlock nameText;
    private readonly TextBlock deviceIdText;
    private readonly TextBlock statusText;
    private string lastDeviceName = string.Empty;
    private string lastDeviceId = string.Empty;
    private string lastStatusLine = string.Empty;
    private DeviceLifecycleIcon lastIcon;
    private DeviceLifecycleTone lastTone = DeviceLifecycleTone.Normal;
    private string? lastPreviewAppId;
    private string lastPreviewPlaceholderText = string.Empty;
    private bool lastIconHidden;
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
        textHost = new Grid
        {
            ColumnSpacing = 8,
        };
        textHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        textHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        textHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        textHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        textHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(textHost, 1);
        stateIcon = new SymbolIcon(Symbol.Help)
        {
            VerticalAlignment = VerticalAlignment.Center,
        };
        textHost.Children.Add(stateIcon);
        nameText = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        Grid.SetColumn(nameText, 1);
        textHost.Children.Add(nameText);
        deviceIdText = new TextBlock
        {
            Opacity = 0.82,
        };
        Grid.SetColumn(deviceIdText, 1);
        Grid.SetRow(deviceIdText, 1);
        textHost.Children.Add(deviceIdText);
        statusText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(statusText, 1);
        Grid.SetRow(statusText, 2);
        textHost.Children.Add(statusText);
        Children.Add(textHost);
    }
    public void Bind(string name, string deviceId, string statusLine, DeviceLifecyclePresentation presence, AppCatalogItem? previewItem, string previewPlaceholderText)
    {
        if (!string.Equals(lastDeviceName, name, StringComparison.Ordinal))
        {
            nameText.Text = name;
            lastDeviceName = name;
        }
        if (!string.Equals(lastDeviceId, deviceId, StringComparison.Ordinal))
        {
            deviceIdText.Text = deviceId;
            lastDeviceId = deviceId;
        }
        if (!string.Equals(lastStatusLine, statusLine, StringComparison.Ordinal))
        {
            statusText.Text = statusLine;
            lastStatusLine = statusLine;
        }
        if (lastIcon != presence.Icon)
        {
            stateIcon.Symbol = MapSymbol(presence.Icon);
            lastIcon = presence.Icon;
        }
        var hideIcon = presence.Icon == DeviceLifecycleIcon.Pause;
        if (lastIconHidden != hideIcon)
        {
            stateIcon.Visibility = hideIcon ? Visibility.Collapsed : Visibility.Visible;
            textHost.ColumnSpacing = hideIcon ? 0 : 8;
            lastIconHidden = hideIcon;
        }
        if (lastTone != presence.Tone)
        {
            ApplyTone(presence.Tone);
            lastTone = presence.Tone;
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
    private void ApplyTone(DeviceLifecycleTone tone)
    {
        textHost.Opacity = 1d;
        nameText.Foreground = null;
        stateIcon.Foreground = null;
        switch (tone)
        {
            case DeviceLifecycleTone.Success:
                var successBrush = UiResourceResolver.ResolveBrush("SuccessTextFillColorPrimaryBrush", Color.FromArgb(255, 130, 220, 155));
                nameText.Foreground = successBrush;
                stateIcon.Foreground = successBrush;
                break;
            case DeviceLifecycleTone.Warning:
                var warningBrush = UiResourceResolver.ResolveBrush("SystemFillColorCautionBrush", Color.FromArgb(255, 255, 195, 85));
                nameText.Foreground = warningBrush;
                stateIcon.Foreground = warningBrush;
                break;
            case DeviceLifecycleTone.Muted:
                textHost.Opacity = 0.72;
                break;
            case DeviceLifecycleTone.Normal:
            default:
                break;
        }
    }
    private static Symbol MapSymbol(DeviceLifecycleIcon icon)
    {
        return icon switch
        {
            DeviceLifecycleIcon.Accept => Symbol.Accept,
            DeviceLifecycleIcon.Pause => Symbol.Pause,
            DeviceLifecycleIcon.Important => Symbol.Important,
            DeviceLifecycleIcon.Clock => Symbol.Clock,
            _ => Symbol.Help,
        };
    }
}


