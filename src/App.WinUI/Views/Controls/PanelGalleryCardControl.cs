using System.ComponentModel;
using App.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/paineis.md#galeria-de-paineis
public sealed class PanelGalleryCardControl : UserControl
{
    private const double DefaultCardWidth = 332d;
    private const double PreviewHorizontalChrome = 32d;
    private const double PreviewVerticalChrome = 28d;
    private const double PreviewAspectRatio = 2d;
    private const double MinimumPreviewWidth = 196d;
    private const double MinimumPreviewHeight = 98d;

    private readonly Border cardBorder;
    private readonly Grid previewArea;
    private readonly Button previewButton;
    private readonly Hub75PanelThumbnailControl thumbnail;
    private readonly Border activeBadge;
    private readonly TextBlock titleText;
    private readonly TextBlock subtitleText;
    private readonly ToggleSwitch activeToggle;
    private PanelsPage.PanelGalleryItemState? state;
    private bool suppressToggle;

    public PanelGalleryCardControl()
    {
        previewButton = new Button
        {
            Padding = new Thickness(0),
            Background = null,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        previewButton.Click += OnOpenRequested;

        thumbnail = new Hub75PanelThumbnailControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        previewButton.Content = thumbnail;

        previewArea = new Grid
        {
            Padding = new Thickness(0, 12, 0, 6),
            MinHeight = MinimumPreviewHeight + PreviewVerticalChrome,
        };
        previewArea.Children.Add(previewButton);

        activeBadge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(999),
            Background = UiResourceResolver.ResolveBrush("SystemAccentColor", Color.FromArgb(255, 0, 120, 212)),
            Child = new TextBlock
            {
                Text = "ATIVO",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
            Visibility = Visibility.Collapsed,
        };
        previewArea.Children.Add(activeBadge);

        var menuButton = new Button
        {
            Content = new SymbolIcon(Symbol.More),
            Padding = new Thickness(8),
            Width = 36,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
        };
        var menuFlyout = new MenuFlyout();
        var editItem = new MenuFlyoutItem
        {
            Text = "Editar",
            Icon = new SymbolIcon(Symbol.Edit),
        };
        editItem.Click += OnOpenRequested;
        var duplicateItem = new MenuFlyoutItem
        {
            Text = "Duplicar",
            Icon = new SymbolIcon(Symbol.Copy),
        };
        duplicateItem.Click += OnDuplicateRequested;
        var deleteItem = new MenuFlyoutItem
        {
            Text = "Excluir",
            Icon = new SymbolIcon(Symbol.Delete),
        };
        deleteItem.Click += OnDeleteRequested;
        menuFlyout.Items.Add(editItem);
        menuFlyout.Items.Add(duplicateItem);
        menuFlyout.Items.Add(deleteItem);
        menuButton.Flyout = menuFlyout;
        previewArea.Children.Add(menuButton);

        var infoButton = new Button
        {
            Padding = new Thickness(0),
            Background = null,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        infoButton.Click += OnOpenRequested;

        titleText = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxLines = 2,
        };
        subtitleText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.WrapWholeWords,
        };
        infoButton.Content = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                titleText,
                subtitleText,
            },
        };

        activeToggle = new ToggleSwitch
        {
            OffContent = string.Empty,
            OnContent = string.Empty,
        };
        activeToggle.Toggled += OnActiveToggled;

        var footerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Ativo",
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.86,
                },
                activeToggle,
            },
        };

        var footerGrid = new Grid
        {
            Padding = new Thickness(16, 14, 16, 14),
            ColumnSpacing = 16,
        };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.Children.Add(infoButton);
        Grid.SetColumn(footerActions, 1);
        footerGrid.Children.Add(footerActions);

        var cardGrid = new Grid();
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.Children.Add(previewArea);
        Grid.SetRow(footerGrid, 1);
        cardGrid.Children.Add(footerGrid);

        cardBorder = new Border
        {
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 55, 68, 86)),
            Background = UiResourceResolver.ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 18, 24, 32)),
            Child = cardGrid,
        };

        Margin = new Thickness(6);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Content = cardBorder;

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        BindState(args.NewValue as PanelsPage.PanelGalleryItemState);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        BindState(null);
    }

    private void BindState(PanelsPage.PanelGalleryItemState? nextState)
    {
        if (ReferenceEquals(state, nextState))
        {
            ApplyState();
            return;
        }

        if (state is not null)
        {
            state.PropertyChanged -= OnStatePropertyChanged;
        }

        state = nextState;
        if (state is not null)
        {
            state.PropertyChanged += OnStatePropertyChanged;
        }

        ApplyState();
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyState();
    }

    private void ApplyState()
    {
        var cardWidth = state?.CardWidth ?? DefaultCardWidth;
        var previewSize = ResolvePreviewSize(cardWidth);
        Width = cardWidth;
        previewArea.Height = previewSize.Height + PreviewVerticalChrome;
        previewButton.Width = previewSize.Width;
        previewButton.Height = previewSize.Height;
        thumbnail.Width = previewSize.Width;
        thumbnail.Height = previewSize.Height;
        titleText.Text = state?.Name ?? string.Empty;
        subtitleText.Text = state?.Subtitle ?? string.Empty;
        thumbnail.Frame = state?.Frame ?? Array.Empty<MicaAudio.Core.Presets.RgbaColor>();

        var isActive = state?.IsActive ?? false;
        suppressToggle = true;
        activeToggle.IsOn = isActive;
        activeToggle.IsEnabled = state?.CanActivate ?? false;
        suppressToggle = false;

        cardBorder.BorderThickness = isActive ? new Thickness(2) : new Thickness(1);
        cardBorder.BorderBrush = isActive
            ? UiResourceResolver.ResolveBrush("SystemAccentColor", Color.FromArgb(255, 0, 120, 212))
            : UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 55, 68, 86));
        activeBadge.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static Size ResolvePreviewSize(double cardWidth)
    {
        var normalizedWidth = double.IsFinite(cardWidth) && cardWidth > 0d
            ? cardWidth
            : DefaultCardWidth;
        var previewWidth = normalizedWidth - PreviewHorizontalChrome;
        if (!double.IsFinite(previewWidth) || previewWidth <= 0d)
        {
            previewWidth = DefaultCardWidth - PreviewHorizontalChrome;
        }

        previewWidth = Math.Max(previewWidth, MinimumPreviewWidth);
        if (normalizedWidth > PreviewHorizontalChrome)
        {
            previewWidth = Math.Min(previewWidth, normalizedWidth - PreviewHorizontalChrome);
        }

        if (previewWidth <= 0d)
        {
            previewWidth = MinimumPreviewWidth;
        }

        var previewHeight = Math.Max(MinimumPreviewHeight, Math.Round(previewWidth / PreviewAspectRatio));
        return new Size(previewWidth, previewHeight);
    }

    private async void OnOpenRequested(object sender, RoutedEventArgs e)
    {
        if (state is not null)
        {
            await state.OpenEditorAsync(state.PanelId);
        }
    }

    private async void OnDuplicateRequested(object sender, RoutedEventArgs e)
    {
        if (state is not null)
        {
            await state.DuplicateAsync(state.PanelId);
        }
    }

    private async void OnDeleteRequested(object sender, RoutedEventArgs e)
    {
        if (state is not null)
        {
            await state.DeleteAsync(state.PanelId);
        }
    }

    private async void OnActiveToggled(object sender, RoutedEventArgs e)
    {
        if (suppressToggle || state is null)
        {
            return;
        }

        await state.SetActiveAsync(state.PanelId, activeToggle.IsOn);
    }
}
