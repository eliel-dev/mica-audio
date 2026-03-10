using App.WinUI.Models.Panels;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Windows.Foundation;
using Windows.UI;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
internal sealed class Hub75PanelEditorControl : Grid, IDisposable
{
    private const float OverlayLabelTopPadding = 4f;
    private const float OverlayLabelLeftPadding = 4f;
    private const float OverlayErrorTopPadding = 20f;
    private const double HandleVisualSize = 8d;
    private const double HandleHitThickness = 10d;

    private readonly CanvasControl canvas;
    private readonly CanvasTextFormat labelTextFormat = new()
    {
        FontFamily = "Segoe UI",
        FontSize = 12,
        WordWrapping = CanvasWordWrapping.NoWrap,
    };
    private readonly InputCursor arrowCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    private readonly InputCursor sizeHorizontalCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private readonly InputCursor sizeVerticalCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    private readonly InputCursor sizeDiagonalPrimaryCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast);
    private readonly InputCursor sizeDiagonalSecondaryCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest);

    private PanelDefinition? panel;
    private string? selectedWidgetId;
    private IReadOnlyDictionary<string, string> widgetErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private RgbaColor[] frame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();
    private LayoutInfo lastLayout;
    private PanelWidgetDefinition? activeWidget;
    private WidgetBounds startBounds;
    private int interactionStartX;
    private int interactionStartY;
    private bool interactionActive;
    private bool interactionChanged;
    private Hub75PanelWidgetInteractionKind activeInteractionKind = Hub75PanelWidgetInteractionKind.None;

    public Hub75PanelEditorControl()
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 55, 68, 86)),
            Background = UiResourceResolver.ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 8, 10, 14)),
            Padding = new Thickness(8),
        };

        canvas = new CanvasControl
        {
            MinHeight = 360,
        };
        canvas.Draw += OnCanvasDraw;
        canvas.PointerPressed += OnCanvasPointerPressed;
        canvas.PointerMoved += OnCanvasPointerMoved;
        canvas.PointerReleased += OnCanvasPointerReleased;
        canvas.PointerCaptureLost += OnCanvasPointerCaptureLost;
        canvas.PointerExited += OnCanvasPointerExited;
        canvas.SizeChanged += (_, _) => canvas.Invalidate();

        border.Child = canvas;
        Children.Add(border);
    }

    public event EventHandler<string?>? WidgetSelected;

    public event EventHandler<Hub75PanelWidgetBoundsChangedEventArgs>? WidgetBoundsChanged;

    public PanelDefinition? Panel
    {
        get => panel;
        set
        {
            panel = value;
            panel?.Normalize();
            canvas.Invalidate();
        }
    }

    public string? SelectedWidgetId
    {
        get => selectedWidgetId;
        set
        {
            selectedWidgetId = value;
            canvas.Invalidate();
        }
    }

    public IReadOnlyDictionary<string, string> WidgetErrors
    {
        get => widgetErrors;
        set
        {
            widgetErrors = value ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            canvas.Invalidate();
        }
    }

    public RgbaColor[] Frame
    {
        get => frame.ToArray();
        set
        {
            frame = value?.Length == LedDefaults.MatrixWidth * LedDefaults.MatrixHeight
                ? value.ToArray()
                : Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();
            canvas.Invalidate();
        }
    }

    public bool TryMapToMatrix(Point point, out int x, out int y)
    {
        var layout = lastLayout;
        if (!layout.IsValid)
        {
            x = 0;
            y = 0;
            return false;
        }

        x = ClampToMatrixX(point.X, layout);
        y = ClampToMatrixY(point.Y, layout);
        return point.X >= layout.OffsetX
            && point.X <= layout.OffsetX + layout.DrawWidth
            && point.Y >= layout.OffsetY
            && point.Y <= layout.OffsetY + layout.DrawHeight;
    }

    private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        args.DrawingSession.Clear(Color.FromArgb(255, 5, 7, 10));

        lastLayout = ComputeLayout(sender.ActualWidth, sender.ActualHeight);
        if (!lastLayout.IsValid)
        {
            return;
        }

        args.DrawingSession.FillRoundedRectangle(
            lastLayout.OffsetX - 6f,
            lastLayout.OffsetY - 6f,
            lastLayout.DrawWidth + 12f,
            lastLayout.DrawHeight + 12f,
            14f,
            14f,
            Color.FromArgb(255, 2, 3, 5));
        args.DrawingSession.DrawRoundedRectangle(
            lastLayout.OffsetX - 6f,
            lastLayout.OffsetY - 6f,
            lastLayout.DrawWidth + 12f,
            lastLayout.DrawHeight + 12f,
            14f,
            14f,
            Color.FromArgb(255, 34, 42, 54),
            1f);

        DrawMatrix(args);
        DrawWidgetOverlays(args);
    }

    private void DrawMatrix(CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        for (var y = 0; y < LedDefaults.MatrixHeight; y++)
        {
            for (var x = 0; x < LedDefaults.MatrixWidth; x++)
            {
                var index = (y * LedDefaults.MatrixWidth) + x;
                var color = frame[index];
                var ledColor = (color.R | color.G | color.B) == 0
                    ? Color.FromArgb(255, 10, 12, 16)
                    : Color.FromArgb(color.A, color.R, color.G, color.B);
                var left = lastLayout.OffsetX + (x * lastLayout.Pitch) + ((lastLayout.Pitch - lastLayout.LedSize) * 0.5f);
                var top = lastLayout.OffsetY + (y * lastLayout.Pitch) + ((lastLayout.Pitch - lastLayout.LedSize) * 0.5f);
                ds.FillRectangle(left, top, lastLayout.LedSize, lastLayout.LedSize, ledColor);
            }
        }
    }

    private void DrawWidgetOverlays(CanvasDrawEventArgs args)
    {
        if (panel?.Widgets is not { Count: > 0 })
        {
            return;
        }

        var ds = args.DrawingSession;
        foreach (var widget in panel.Widgets.OrderBy(static widget => widget.ZIndex))
        {
            var rect = ToScreenRect(widget);
            var isSelected = string.Equals(widget.WidgetId, selectedWidgetId, StringComparison.OrdinalIgnoreCase);
            var hasError = widgetErrors.TryGetValue(widget.WidgetId, out var errorMessage);
            var stroke = hasError
                ? Color.FromArgb(255, 255, 104, 104)
                : isSelected
                    ? Color.FromArgb(255, 74, 222, 255)
                    : Color.FromArgb(255, 160, 178, 204);
            var fill = hasError
                ? Color.FromArgb(42, 255, 104, 104)
                : isSelected
                    ? Color.FromArgb(34, 74, 222, 255)
                    : Color.FromArgb(20, 255, 255, 255);

            ds.FillRectangle(rect, fill);
            ds.DrawRectangle(rect, stroke, isSelected ? 2f : 1f);
            ds.DrawText(widget.AppId, new System.Numerics.Vector2((float)rect.X + OverlayLabelLeftPadding, (float)rect.Y + OverlayLabelTopPadding), stroke, labelTextFormat);

            if (hasError && !string.IsNullOrWhiteSpace(errorMessage))
            {
                ds.DrawText(errorMessage, new System.Numerics.Vector2((float)rect.X + OverlayLabelLeftPadding, (float)rect.Y + OverlayErrorTopPadding), Color.FromArgb(255, 255, 196, 196), labelTextFormat);
            }

            if (isSelected)
            {
                DrawSelectionHandles(ds, rect, stroke);
            }
        }
    }

    private static void DrawSelectionHandles(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, Rect rect, Color stroke)
    {
        var fill = Color.FromArgb(255, 8, 12, 18);
        foreach (var handleRect in EnumerateHandleRects(rect))
        {
            ds.FillRectangle(handleRect, fill);
            ds.DrawRectangle(handleRect, stroke, 1.5f);
        }
    }

    private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (panel is null || !lastLayout.IsValid)
        {
            return;
        }

        var point = e.GetCurrentPoint(canvas).Position;
        var hit = ResolveInteraction(point);
        SelectedWidgetId = hit.Widget?.WidgetId;
        WidgetSelected?.Invoke(this, SelectedWidgetId);

        if (hit.Widget is null || hit.Kind == Hub75PanelWidgetInteractionKind.None)
        {
            ResetInteraction();
            UpdateCursor(point);
            return;
        }

        activeWidget = hit.Widget;
        activeInteractionKind = hit.Kind;
        startBounds = WidgetBounds.From(activeWidget);
        interactionStartX = ClampToMatrixX(point.X, lastLayout);
        interactionStartY = ClampToMatrixY(point.Y, lastLayout);
        interactionActive = true;
        interactionChanged = false;
        ProtectedCursor = ResolveCursor(hit.Kind);
        canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(canvas).Position;
        if (!interactionActive || activeWidget is null || panel is null)
        {
            UpdateCursor(point);
            return;
        }

        var matrixX = ClampToMatrixX(point.X, lastLayout);
        var matrixY = ClampToMatrixY(point.Y, lastLayout);
        var nextBounds = CalculateNextBounds(startBounds, matrixX - interactionStartX, matrixY - interactionStartY, activeInteractionKind, panel.Width, panel.Height);

        if (!ApplyBounds(activeWidget, nextBounds))
        {
            return;
        }

        interactionChanged = true;
        canvas.Invalidate();
        WidgetBoundsChanged?.Invoke(this, new Hub75PanelWidgetBoundsChangedEventArgs(
            activeWidget.WidgetId,
            activeWidget.X,
            activeWidget.Y,
            activeWidget.Width,
            activeWidget.Height,
            activeInteractionKind,
            isCommit: false));
    }

    private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CompleteInteraction();
    }

    private void OnCanvasPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        CompleteInteraction();
    }

    private void OnCanvasPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!interactionActive)
        {
            ProtectedCursor = arrowCursor;
        }
    }

    private void CompleteInteraction()
    {
        if (!interactionActive || activeWidget is null)
        {
            ResetInteraction();
            return;
        }

        if (interactionChanged)
        {
            WidgetBoundsChanged?.Invoke(this, new Hub75PanelWidgetBoundsChangedEventArgs(
                activeWidget.WidgetId,
                activeWidget.X,
                activeWidget.Y,
                activeWidget.Width,
                activeWidget.Height,
                activeInteractionKind,
                isCommit: true));
        }

        ResetInteraction();
    }

    private void ResetInteraction()
    {
        interactionActive = false;
        interactionChanged = false;
        activeWidget = null;
        activeInteractionKind = Hub75PanelWidgetInteractionKind.None;
        ProtectedCursor = arrowCursor;
    }

    private void UpdateCursor(Point point)
    {
        var hover = ResolveInteraction(point);
        ProtectedCursor = ResolveCursor(hover.Kind);
    }

    private InteractionHit ResolveInteraction(Point point)
    {
        if (panel?.Widgets is null || panel.Widgets.Count == 0)
        {
            return InteractionHit.None;
        }

        var selectedWidget = panel.Widgets.FirstOrDefault(widget => string.Equals(widget.WidgetId, selectedWidgetId, StringComparison.OrdinalIgnoreCase));
        if (selectedWidget is not null)
        {
            var selectedInteraction = ResolveSelectedWidgetInteraction(point, selectedWidget);
            if (selectedInteraction != Hub75PanelWidgetInteractionKind.None)
            {
                return new InteractionHit(selectedWidget, selectedInteraction);
            }
        }

        var matrixX = ClampToMatrixX(point.X, lastLayout);
        var matrixY = ClampToMatrixY(point.Y, lastLayout);
        var topmostWidget = panel.Widgets
            .OrderByDescending(widget => widget.ZIndex)
            .FirstOrDefault(widget =>
                matrixX >= widget.X
                && matrixX < widget.X + widget.Width
                && matrixY >= widget.Y
                && matrixY < widget.Y + widget.Height);

        return topmostWidget is null
            ? InteractionHit.None
            : new InteractionHit(topmostWidget, Hub75PanelWidgetInteractionKind.Move);
    }

    private Hub75PanelWidgetInteractionKind ResolveSelectedWidgetInteraction(Point point, PanelWidgetDefinition widget)
    {
        var rect = ToScreenRect(widget);
        var left = rect.X;
        var top = rect.Y;
        var right = rect.X + rect.Width;
        var bottom = rect.Y + rect.Height;
        var withinVerticalEdge = point.Y >= top - HandleHitThickness && point.Y <= bottom + HandleHitThickness;
        var withinHorizontalEdge = point.X >= left - HandleHitThickness && point.X <= right + HandleHitThickness;
        var nearLeft = Math.Abs(point.X - left) <= HandleHitThickness;
        var nearRight = Math.Abs(point.X - right) <= HandleHitThickness;
        var nearTop = Math.Abs(point.Y - top) <= HandleHitThickness;
        var nearBottom = Math.Abs(point.Y - bottom) <= HandleHitThickness;

        if (nearLeft && nearTop)
        {
            return Hub75PanelWidgetInteractionKind.ResizeTopLeft;
        }

        if (nearRight && nearTop)
        {
            return Hub75PanelWidgetInteractionKind.ResizeTopRight;
        }

        if (nearLeft && nearBottom)
        {
            return Hub75PanelWidgetInteractionKind.ResizeBottomLeft;
        }

        if (nearRight && nearBottom)
        {
            return Hub75PanelWidgetInteractionKind.ResizeBottomRight;
        }

        if (nearLeft && withinVerticalEdge)
        {
            return Hub75PanelWidgetInteractionKind.ResizeLeft;
        }

        if (nearRight && withinVerticalEdge)
        {
            return Hub75PanelWidgetInteractionKind.ResizeRight;
        }

        if (nearTop && withinHorizontalEdge)
        {
            return Hub75PanelWidgetInteractionKind.ResizeTop;
        }

        if (nearBottom && withinHorizontalEdge)
        {
            return Hub75PanelWidgetInteractionKind.ResizeBottom;
        }

        return rect.Contains(point) ? Hub75PanelWidgetInteractionKind.Move : Hub75PanelWidgetInteractionKind.None;
    }

    private static WidgetBounds CalculateNextBounds(
        WidgetBounds startBounds,
        int deltaX,
        int deltaY,
        Hub75PanelWidgetInteractionKind interactionKind,
        int panelWidth,
        int panelHeight)
    {
        var x = startBounds.X;
        var y = startBounds.Y;
        var width = startBounds.Width;
        var height = startBounds.Height;
        var right = startBounds.X + startBounds.Width;
        var bottom = startBounds.Y + startBounds.Height;

        switch (interactionKind)
        {
            case Hub75PanelWidgetInteractionKind.Move:
                x = Math.Clamp(startBounds.X + deltaX, 0, Math.Max(0, panelWidth - startBounds.Width));
                y = Math.Clamp(startBounds.Y + deltaY, 0, Math.Max(0, panelHeight - startBounds.Height));
                break;

            case Hub75PanelWidgetInteractionKind.ResizeLeft:
                x = Math.Clamp(startBounds.X + deltaX, 0, right - 1);
                width = right - x;
                break;

            case Hub75PanelWidgetInteractionKind.ResizeRight:
                width = Math.Clamp(startBounds.Width + deltaX, 1, Math.Max(1, panelWidth - startBounds.X));
                break;

            case Hub75PanelWidgetInteractionKind.ResizeTop:
                y = Math.Clamp(startBounds.Y + deltaY, 0, bottom - 1);
                height = bottom - y;
                break;

            case Hub75PanelWidgetInteractionKind.ResizeBottom:
                height = Math.Clamp(startBounds.Height + deltaY, 1, Math.Max(1, panelHeight - startBounds.Y));
                break;

            case Hub75PanelWidgetInteractionKind.ResizeTopLeft:
                x = Math.Clamp(startBounds.X + deltaX, 0, right - 1);
                y = Math.Clamp(startBounds.Y + deltaY, 0, bottom - 1);
                width = right - x;
                height = bottom - y;
                break;

            case Hub75PanelWidgetInteractionKind.ResizeTopRight:
                y = Math.Clamp(startBounds.Y + deltaY, 0, bottom - 1);
                width = Math.Clamp(startBounds.Width + deltaX, 1, Math.Max(1, panelWidth - startBounds.X));
                height = bottom - y;
                break;

            case Hub75PanelWidgetInteractionKind.ResizeBottomLeft:
                x = Math.Clamp(startBounds.X + deltaX, 0, right - 1);
                width = right - x;
                height = Math.Clamp(startBounds.Height + deltaY, 1, Math.Max(1, panelHeight - startBounds.Y));
                break;

            case Hub75PanelWidgetInteractionKind.ResizeBottomRight:
                width = Math.Clamp(startBounds.Width + deltaX, 1, Math.Max(1, panelWidth - startBounds.X));
                height = Math.Clamp(startBounds.Height + deltaY, 1, Math.Max(1, panelHeight - startBounds.Y));
                break;
        }

        width = Math.Clamp(width, 1, Math.Max(1, panelWidth - x));
        height = Math.Clamp(height, 1, Math.Max(1, panelHeight - y));
        x = Math.Clamp(x, 0, Math.Max(0, panelWidth - width));
        y = Math.Clamp(y, 0, Math.Max(0, panelHeight - height));
        return new WidgetBounds(x, y, width, height);
    }

    private static bool ApplyBounds(PanelWidgetDefinition widget, WidgetBounds bounds)
    {
        if (widget.X == bounds.X
            && widget.Y == bounds.Y
            && widget.Width == bounds.Width
            && widget.Height == bounds.Height)
        {
            return false;
        }

        widget.X = bounds.X;
        widget.Y = bounds.Y;
        widget.Width = bounds.Width;
        widget.Height = bounds.Height;
        return true;
    }

    private Rect ToScreenRect(PanelWidgetDefinition widget)
    {
        var x = lastLayout.OffsetX + (widget.X * lastLayout.Pitch);
        var y = lastLayout.OffsetY + (widget.Y * lastLayout.Pitch);
        var width = Math.Max(lastLayout.Pitch, widget.Width * lastLayout.Pitch);
        var height = Math.Max(lastLayout.Pitch, widget.Height * lastLayout.Pitch);
        return new Rect(x, y, width, height);
    }

    private static IEnumerable<Rect> EnumerateHandleRects(Rect rect)
    {
        var half = HandleVisualSize * 0.5d;
        yield return CreateHandleRect(rect.X, rect.Y, half);
        yield return CreateHandleRect(rect.X + (rect.Width * 0.5d), rect.Y, half);
        yield return CreateHandleRect(rect.X + rect.Width, rect.Y, half);
        yield return CreateHandleRect(rect.X, rect.Y + (rect.Height * 0.5d), half);
        yield return CreateHandleRect(rect.X + rect.Width, rect.Y + (rect.Height * 0.5d), half);
        yield return CreateHandleRect(rect.X, rect.Y + rect.Height, half);
        yield return CreateHandleRect(rect.X + (rect.Width * 0.5d), rect.Y + rect.Height, half);
        yield return CreateHandleRect(rect.X + rect.Width, rect.Y + rect.Height, half);
    }

    private static Rect CreateHandleRect(double centerX, double centerY, double half)
    {
        return new Rect(centerX - half, centerY - half, HandleVisualSize, HandleVisualSize);
    }

    private static LayoutInfo ComputeLayout(double width, double height)
    {
        var safeWidth = Math.Max(1d, width);
        var safeHeight = Math.Max(1d, height);
        var padding = Math.Min(20d, Math.Max(8d, Math.Min(safeWidth, safeHeight) * 0.05d));
        var availableWidth = Math.Max(1d, safeWidth - (padding * 2d));
        var availableHeight = Math.Max(1d, safeHeight - (padding * 2d));
        var pitch = Math.Min(availableWidth / LedDefaults.MatrixWidth, availableHeight / LedDefaults.MatrixHeight);
        pitch = Math.Max(0.25d, pitch);
        var ledSize = pitch < 1d
            ? Math.Min(pitch, Math.Max(0.25d, pitch * 0.92d))
            : Math.Max(1d, pitch * 0.78d);
        var drawWidth = LedDefaults.MatrixWidth * pitch;
        var drawHeight = LedDefaults.MatrixHeight * pitch;
        var offsetX = (safeWidth - drawWidth) * 0.5d;
        var offsetY = (safeHeight - drawHeight) * 0.5d;
        return new LayoutInfo(true, (float)offsetX, (float)offsetY, (float)pitch, (float)ledSize, (float)drawWidth, (float)drawHeight);
    }

    private static int ClampToMatrixX(double x, LayoutInfo layout)
    {
        var local = (x - layout.OffsetX) / Math.Max(layout.Pitch, 0.0001f);
        return Math.Clamp((int)Math.Floor(local), 0, LedDefaults.MatrixWidth - 1);
    }

    private static int ClampToMatrixY(double y, LayoutInfo layout)
    {
        var local = (y - layout.OffsetY) / Math.Max(layout.Pitch, 0.0001f);
        return Math.Clamp((int)Math.Floor(local), 0, LedDefaults.MatrixHeight - 1);
    }

    private InputCursor ResolveCursor(Hub75PanelWidgetInteractionKind interactionKind)
    {
        return interactionKind switch
        {
            Hub75PanelWidgetInteractionKind.ResizeLeft or Hub75PanelWidgetInteractionKind.ResizeRight => sizeHorizontalCursor,
            Hub75PanelWidgetInteractionKind.ResizeTop or Hub75PanelWidgetInteractionKind.ResizeBottom => sizeVerticalCursor,
            Hub75PanelWidgetInteractionKind.ResizeTopLeft or Hub75PanelWidgetInteractionKind.ResizeBottomRight => sizeDiagonalPrimaryCursor,
            Hub75PanelWidgetInteractionKind.ResizeTopRight or Hub75PanelWidgetInteractionKind.ResizeBottomLeft => sizeDiagonalSecondaryCursor,
            _ => arrowCursor,
        };
    }

    private readonly record struct LayoutInfo(
        bool IsValid,
        float OffsetX,
        float OffsetY,
        float Pitch,
        float LedSize,
        float DrawWidth,
        float DrawHeight);

    private readonly record struct WidgetBounds(int X, int Y, int Width, int Height)
    {
        public static WidgetBounds From(PanelWidgetDefinition widget)
        {
            return new WidgetBounds(widget.X, widget.Y, widget.Width, widget.Height);
        }
    }

    private readonly record struct InteractionHit(PanelWidgetDefinition? Widget, Hub75PanelWidgetInteractionKind Kind)
    {
        public static InteractionHit None => new(null, Hub75PanelWidgetInteractionKind.None);
    }

    public void Dispose()
    {
        arrowCursor.Dispose();
        sizeHorizontalCursor.Dispose();
        sizeVerticalCursor.Dispose();
        sizeDiagonalPrimaryCursor.Dispose();
        sizeDiagonalSecondaryCursor.Dispose();
        labelTextFormat.Dispose();
    }
}

internal enum Hub75PanelWidgetInteractionKind
{
    None,
    Move,
    ResizeLeft,
    ResizeTop,
    ResizeRight,
    ResizeBottom,
    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomLeft,
    ResizeBottomRight,
}

internal sealed class Hub75PanelWidgetBoundsChangedEventArgs : EventArgs
{
    public Hub75PanelWidgetBoundsChangedEventArgs(
        string widgetId,
        int x,
        int y,
        int width,
        int height,
        Hub75PanelWidgetInteractionKind interactionKind,
        bool isCommit)
    {
        WidgetId = widgetId;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        InteractionKind = interactionKind;
        IsCommit = isCommit;
    }

    public string WidgetId { get; }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public Hub75PanelWidgetInteractionKind InteractionKind { get; }

    public bool IsCommit { get; }
}
