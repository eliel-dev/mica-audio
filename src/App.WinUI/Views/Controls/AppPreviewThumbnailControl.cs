using System.Diagnostics;
using App.WinUI.Models.Apps;
using App.WinUI.Views;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace App.WinUI.Views.Controls;

internal sealed class AppPreviewThumbnailControl : Grid
{
    private readonly CanvasControl canvas;
    private readonly DispatcherQueueTimer timer;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    private AppCatalogItem? item;
    private IAppPreviewRenderer renderer = AppPreviewRendererRegistry.Resolve(new AppCatalogItem());
    private bool isSelected;
    private bool isRunning;
    private IReadOnlyDictionary<string, string> configValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public AppPreviewThumbnailControl()
    {
        Width = 210;
        Height = 88;

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = UiResourceResolver.ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 9, 12, 16)),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 47, 64, 85)),
            Padding = new Thickness(0),
        };

        canvas = new CanvasControl();
        canvas.Draw += OnDraw;
        border.Child = canvas;

        Children.Add(border);

        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(33);
        timer.Tick += (_, _) =>
        {
            if (isRunning)
            {
                canvas.Invalidate();
            }
        };

        Unloaded += (_, _) => Stop();
        Loaded += (_, _) =>
        {
            if (isRunning)
            {
                timer.Start();
            }
        };
    }

    public AppCatalogItem? Item => item;

    public void Bind(AppCatalogItem appItem)
    {
        item = appItem;
        renderer = AppPreviewRendererRegistry.Resolve(appItem);
        canvas.Invalidate();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        canvas.Invalidate();
    }

    public void SetConfig(IReadOnlyDictionary<string, string>? values)
    {
        configValues = values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

        canvas.Invalidate();
    }

    public void Start()
    {
        isRunning = true;
        if (!timer.IsRunning)
        {
            timer.Start();
        }

        canvas.Invalidate();
    }

    public void Stop()
    {
        isRunning = false;
        if (timer.IsRunning)
        {
            timer.Stop();
        }
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        args.DrawingSession.Clear(Color.FromArgb(255, 4, 6, 8));

        if (item is null)
        {
            return;
        }

        var time = (float)stopwatch.Elapsed.TotalSeconds * Math.Max(item.Preview?.Speed ?? 1f, 0.1f);
        var context = new AppPreviewRenderContext(
            args.DrawingSession,
            (float)sender.ActualWidth,
            (float)sender.ActualHeight,
            time,
            item,
            isSelected,
            configValues);

        renderer.Draw(context);
    }
}
