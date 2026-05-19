using MicaAudio.Core.Presets;
using Panels.Composition.ServerSide;
using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-relogio
internal sealed class ClockPreviewRenderer : IAppPreviewRenderer
{
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    public string Kind => "clock";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        Hub75PreviewHelper.DrawPanel(context, out var ox, out var oy, out var pitch, out var ledSize);

        var use24h = !bool.TryParse(context.GetConfigValue("format24h"), out var parsedFormat24) || parsedFormat24;
        var mostrador = context.GetConfigValue("mostrador") ?? "cyberterminal";
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, BrasiliaTimeZone).DateTime;

        RenderMostradorPreview(ds, ox, oy, pitch, ledSize, mostrador, now, use24h);
    }

    private static void RenderMostradorPreview(
        Microsoft.Graphics.Canvas.CanvasDrawingSession ds,
        float ox,
        float oy,
        float pitch,
        float ledSize,
        string mostrador,
        DateTime now,
        bool use24h)
    {
        // Render into a 128×64 RGBA buffer using the shared library, then blit
        // each pixel onto the canvas via the existing helper so the LED look is preserved.
        var w = Hub75PreviewHelper.PanelWidth;
        var h = Hub75PreviewHelper.PanelHeight;
        var frame = new RgbaColor[w * h];
        // Background already black by default (RgbaColor default = 0,0,0,0)
        // But our library reads/writes via PanelsMatrixDrawHelpers which fills black explicitly.

        WatchfaceLibrary.Render(mostrador, frame, w, h, 0, 0, w, h, now, use24h);

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var px = frame[y * w + x];
            if (px.A == 0 && px.R == 0 && px.G == 0 && px.B == 0) continue;
            var color = Color.FromArgb(px.A == 0 ? (byte)255 : px.A, px.R, px.G, px.B);
            Hub75PreviewHelper.DrawPixel(ds, ox, oy, pitch, ledSize, x, y, color, glow: false);
        }
    }

    private static TimeZoneInfo ResolveBrasiliaTimeZone()
    {
        var candidates = new[]
        {
            "America/Sao_Paulo",
            "E. South America Standard Time",
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (TimeZoneInfo.TryConvertIanaIdToWindowsId(candidate, out var windowsId))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }

                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch
            {
                // tenta o próximo candidato
            }
        }

        return TimeZoneInfo.Local;
    }
}
