using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MicaAudio.Core.Presets;
using Windows.Foundation;
using Windows.UI;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
internal sealed class PaletteGradientStripControl : Grid
{
    private readonly CanvasControl canvas = new();
    private IReadOnlyList<PaletteStop> stops = Array.Empty<PaletteStop>();
    private int selectedIndex = -1;

    public PaletteGradientStripControl()
    {
        Height = 76d;
        canvas.ClearColor = Color.FromArgb(0, 0, 0, 0);
        canvas.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
        canvas.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch;
        canvas.Draw += OnDraw;
        canvas.PointerPressed += OnPointerPressed;
        Children.Add(canvas);
        SizeChanged += (_, _) => canvas.Invalidate();
    }

    public IReadOnlyList<PaletteStop> Stops
    {
        get => stops;
        set
        {
            stops = value ?? Array.Empty<PaletteStop>();
            selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, stops.Count - 1));
            canvas.Invalidate();
        }
    }

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            selectedIndex = Math.Clamp(value, 0, Math.Max(0, stops.Count - 1));
            canvas.Invalidate();
        }
    }

    public event Action<int>? StopSelected;

    public event Action<float>? TrackInvoked;

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var width = (float)Math.Max(0d, ActualWidth);
        var height = (float)Math.Max(0d, ActualHeight);
        if (width <= 1f || height <= 1f)
        {
            return;
        }

        var ds = args.DrawingSession;
        var surfaceRect = new Rect(0d, 0d, width, height);
        var trackRect = GetTrackRect(width, height);
        var markerCenterY = (float)trackRect.Y + 10f;
        var markerRadius = 7f;

        ds.FillRoundedRectangle(
            0f,
            0f,
            width,
            height,
            14f,
            14f,
            Color.FromArgb(255, 29, 31, 38));
        ds.DrawRoundedRectangle(
            0.5f,
            0.5f,
            Math.Max(0f, width - 1f),
            Math.Max(0f, height - 1f),
            14f,
            14f,
            Color.FromArgb(255, 63, 68, 80),
            1f);

        if (stops.Count == 0)
        {
            ds.DrawText(
                "Sem gradiente",
                14f,
                (height / 2f) - 8f,
                Color.FromArgb(255, 180, 186, 198));
            return;
        }

        DrawGradientTrack(ds, trackRect);
        ds.DrawRoundedRectangle(
            (float)trackRect.X,
            (float)trackRect.Y,
            (float)trackRect.Width,
            (float)trackRect.Height,
            8f,
            8f,
            Color.FromArgb(255, 96, 102, 120),
            1f);

        for (var index = 0; index < stops.Count; index++)
        {
            var stop = stops[index];
            var markerCenterX = GetStopX(stop.Offset, trackRect);
            var markerColor = ToColor(stop.Color);

            ds.DrawLine(markerCenterX, markerCenterY + markerRadius, markerCenterX, (float)trackRect.Y, Color.FromArgb(255, 79, 84, 94), 1f);
            ds.FillCircle(markerCenterX, markerCenterY, markerRadius, markerColor);

            var outlineColor = index == selectedIndex
                ? Color.FromArgb(255, 255, 255, 255)
                : Color.FromArgb(255, 24, 26, 32);
            var outlineThickness = index == selectedIndex ? 2.4f : 1.2f;
            ds.DrawCircle(markerCenterX, markerCenterY, markerRadius, outlineColor, outlineThickness);
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (stops.Count == 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(this).Position;
        var trackRect = GetTrackRect((float)ActualWidth, (float)ActualHeight);
        var markerCenterY = (float)trackRect.Y + 10f;
        var markerRadius = 10f;

        for (var index = 0; index < stops.Count; index++)
        {
            var stop = stops[index];
            var markerCenterX = GetStopX(stop.Offset, trackRect);
            var dx = (float)point.X - markerCenterX;
            var dy = (float)point.Y - markerCenterY;
            if ((dx * dx) + (dy * dy) <= markerRadius * markerRadius)
            {
                StopSelected?.Invoke(index);
                e.Handled = true;
                return;
            }
        }

        var paddedTrackRect = new Rect(trackRect.X, trackRect.Y - 16d, trackRect.Width, trackRect.Height + 20d);
        if (!paddedTrackRect.Contains(point))
        {
            return;
        }

        var offset = trackRect.Width <= 0d
            ? 0f
            : (float)((point.X - trackRect.X) / trackRect.Width);

        TrackInvoked?.Invoke(Math.Clamp(offset, 0f, 1f));
        e.Handled = true;
    }

    private void DrawGradientTrack(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, Rect trackRect)
    {
        var trackX = (float)trackRect.X;
        var trackY = (float)trackRect.Y;
        var trackWidth = Math.Max(1, (int)Math.Round(trackRect.Width));
        var trackHeight = (float)trackRect.Height;

        for (var x = 0; x < trackWidth; x++)
        {
            var normalizedOffset = trackWidth <= 1 ? 0f : x / (float)(trackWidth - 1);
            var color = ToColor(SampleColorAt(normalizedOffset));
            ds.FillRectangle(trackX + x, trackY, 1f, trackHeight, color);
        }
    }

    private static Rect GetTrackRect(float width, float height)
    {
        const float horizontalPadding = 16f;
        const float trackHeight = 14f;
        const float trackBottomPadding = 16f;
        var trackWidth = Math.Max(0f, width - (horizontalPadding * 2f));
        var trackTop = Math.Max(18f, height - trackHeight - trackBottomPadding);
        return new Rect(horizontalPadding, trackTop, trackWidth, trackHeight);
    }

    private static float GetStopX(float offset, Rect trackRect)
        => (float)trackRect.X + ((float)trackRect.Width * Math.Clamp(offset, 0f, 1f));

    private RgbaColor SampleColorAt(float offset)
    {
        if (stops.Count == 0)
        {
            return new RgbaColor(255, 255, 255);
        }

        if (offset <= stops[0].Offset)
        {
            return stops[0].Color;
        }

        for (var index = 1; index < stops.Count; index++)
        {
            var previous = stops[index - 1];
            var next = stops[index];
            if (offset > next.Offset)
            {
                continue;
            }

            var range = Math.Max(0.0001f, next.Offset - previous.Offset);
            var t = Math.Clamp((offset - previous.Offset) / range, 0f, 1f);
            return Lerp(previous.Color, next.Color, t);
        }

        return stops[^1].Color;
    }

    private static RgbaColor Lerp(RgbaColor left, RgbaColor right, float t)
    {
        static byte LerpChannel(byte a, byte b, float blend)
            => (byte)Math.Clamp((int)Math.Round(a + ((b - a) * blend)), 0, 255);

        return new RgbaColor(
            LerpChannel(left.R, right.R, t),
            LerpChannel(left.G, right.G, t),
            LerpChannel(left.B, right.B, t),
            LerpChannel(left.A, right.A, t));
    }

    private static Color ToColor(RgbaColor color)
        => Color.FromArgb(color.A, color.R, color.G, color.B);
}
