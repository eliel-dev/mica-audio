using System.Globalization;
using MicaAudio.Core.Presets;
using Panels.Composition.Drawing;
using Panels.Composition.Models;

namespace Panels.Composition.ServerSide;

// Server-side mirror of App.WinUI's ClockWidgetRuntime. Renders a Brasilia-
// timezone clock with optional 12/24h toggle and a configurable color, plus
// a thin progress bar that ticks each second along the bottom row.
//
// Why a copy rather than a shared lib: the WinUI runtime sits inside a
// nested private class of PanelsFrameComposer and is tagged
// [SupportedOSPlatform("windows")] alongside the GIF/Image runtimes that
// pull in System.Drawing. Lifting just the Clock out keeps this assembly
// cross-platform without forcing System.Drawing or Magick.NET on the server.
public sealed class ServerClockWidgetRuntime : IServerWidgetRuntime
{
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    private static readonly RgbaColor[] SpectrumPalette =
    [
        new RgbaColor(255, 89, 143, 255),
        new RgbaColor(255, 161, 87, 255),
        new RgbaColor(255, 244, 92, 255),
        new RgbaColor(132, 255, 122, 255),
        new RgbaColor(76, 196, 255, 255),
        new RgbaColor(178, 122, 255, 255),
    ];

    private readonly PanelWidgetDefinition widget;
    private readonly bool use24Hour;
    private readonly RgbaColor color;

    public ServerClockWidgetRuntime(PanelWidgetDefinition widget, int panelWidth, int panelHeight)
    {
        ArgumentNullException.ThrowIfNull(widget);

        this.widget = widget.Clone();
        this.widget.Normalize(panelWidth, panelHeight);
        use24Hour = !this.widget.ConfigValues.TryGetValue("format24h", out var raw24h)
            || !bool.TryParse(raw24h, out var parsed24h)
            || parsed24h;
        color = ResolveClockColor(this.widget.ConfigValues.TryGetValue("fontColor", out var rawColor) ? rawColor : null);
    }

    public string WidgetId => widget.WidgetId;

    public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
    {
        var now = TimeZoneInfo.ConvertTime(utcNow, BrasiliaTimeZone).DateTime;
        var format = use24Hour ? "HH:mm" : "hh:mm";
        var timeText = now.ToString(format, CultureInfo.InvariantCulture);
        var timeWidth = Math.Max(1, (timeText.Length * 6) - 1);
        var timeX = widget.X + Math.Max(0, (widget.Width - timeWidth) / 2);
        var timeY = widget.Y + Math.Max(0, Math.Min(widget.Height - 7, Math.Max(0, (widget.Height / 2) - 7)));

        PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, timeX, timeY, timeText, color);

        if (!use24Hour && widget.Height >= 16)
        {
            var period = now.ToString("tt", CultureInfo.InvariantCulture).ToUpperInvariant();
            var periodWidth = Math.Max(1, (period.Length * 6) - 1);
            var periodX = widget.X + Math.Max(0, widget.Width - periodWidth - 1);
            PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, periodX, widget.Y + 1, period, new RgbaColor(192, 204, 228, 255));
        }

        if (widget.Height >= 20)
        {
            PanelsMatrixDrawHelpers.DrawText5x7(targetFrame, panelWidth, panelHeight, widget.X + 1, widget.Y + widget.Height - 8, "BRT", new RgbaColor(150, 185, 225, 255));
        }

        if (widget.Height >= 10 && widget.Width >= 12)
        {
            var progressWidth = Math.Clamp((int)Math.Round(((now.Second + 1) / 60d) * Math.Max(1, widget.Width - 2)), 0, Math.Max(1, widget.Width - 2));
            for (var offset = 0; offset < progressWidth; offset++)
            {
                PanelsMatrixDrawHelpers.DrawPixel(
                    targetFrame,
                    panelWidth,
                    panelHeight,
                    widget.X + 1 + offset,
                    widget.Y + widget.Height - 1,
                    ResolveSpectrumColor(offset / (float)Math.Max(1, widget.Width - 2)));
            }
        }
    }

    public void Dispose()
    {
    }

    private static TimeZoneInfo ResolveBrasiliaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasilia (UTC-3)", "Brasilia (UTC-3)");
            }
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasilia (UTC-3)", "Brasilia (UTC-3)");
        }
    }

    private static RgbaColor ResolveClockColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new RgbaColor(255, 255, 255, 255);
        }

        var hex = raw.Trim().TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return new RgbaColor((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
        }

        return new RgbaColor(255, 255, 255, 255);
    }

    private static RgbaColor ResolveSpectrumColor(float t)
    {
        var clamped = Math.Clamp(t, 0f, 1f);
        var scaled = clamped * (SpectrumPalette.Length - 1);
        var lowIndex = (int)Math.Floor(scaled);
        var highIndex = Math.Min(SpectrumPalette.Length - 1, lowIndex + 1);
        var blend = scaled - lowIndex;

        var low = SpectrumPalette[lowIndex];
        var high = SpectrumPalette[highIndex];
        return new RgbaColor(
            (byte)(low.R + ((high.R - low.R) * blend)),
            (byte)(low.G + ((high.G - low.G) * blend)),
            (byte)(low.B + ((high.B - low.B) * blend)),
            255);
    }
}
