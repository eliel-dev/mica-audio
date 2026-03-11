using App.WinUI.Models.Apps;
using App.WinUI.Views.Controls.Renderers;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Windows.UI;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/paineis.md#galeria-de-paineis
internal sealed class Hub75PanelThumbnailControl : Grid
{
    private const double MinimumVisibleWidth = 148d;
    private const double MinimumVisibleHeight = 74d;
    private static readonly AppCatalogItem EmptyItem = new();
    private static readonly IReadOnlyDictionary<string, string> EmptyConfigValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly RgbaColor[] BlackFrame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();

    private readonly CanvasControl canvas;
    private RgbaColor[] frame = BlackFrame;

    public Hub75PanelThumbnailControl()
    {
        MinWidth = MinimumVisibleWidth;
        MinHeight = MinimumVisibleHeight;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = UiResourceResolver.ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 5, 7, 10)),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 52, 64, 80)),
            Padding = new Thickness(8),
        };

        canvas = new CanvasControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        canvas.Draw += OnDraw;
        border.Child = canvas;

        Children.Add(border);
    }

    public RgbaColor[] Frame
    {
        get => frame;
        set
        {
            frame = value?.Length == BlackFrame.Length
                ? value
                : BlackFrame;
            canvas.Invalidate();
        }
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        args.DrawingSession.Clear(Color.FromArgb(255, 4, 6, 8));

        var context = new AppPreviewRenderContext(
            args.DrawingSession,
            (float)Math.Max(1d, sender.ActualWidth),
            (float)Math.Max(1d, sender.ActualHeight),
            0f,
            EmptyItem,
            isSelected: false,
            EmptyConfigValues,
            frame);

        Hub75PreviewHelper.DrawPanel(context, out var ox, out var oy, out var pitch, out var ledSize);
        for (var y = 0; y < LedDefaults.MatrixHeight; y++)
        {
            for (var x = 0; x < LedDefaults.MatrixWidth; x++)
            {
                var color = frame[(y * LedDefaults.MatrixWidth) + x];
                var hasSignal = (color.R | color.G | color.B) != 0;
                var ledColor = hasSignal
                    ? Color.FromArgb(color.A, color.R, color.G, color.B)
                    : Color.FromArgb(255, 10, 12, 16);
                Hub75PreviewHelper.DrawPixel(args.DrawingSession, ox, oy, pitch, ledSize, x, y, ledColor, glow: hasSignal);
            }
        }
    }
}
