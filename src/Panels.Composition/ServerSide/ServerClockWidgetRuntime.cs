using MicaAudio.Core.Presets;
using Panels.Composition.Models;

namespace Panels.Composition.ServerSide;

// Server-side clock widget runtime. All rendering is delegated to WatchfaceLibrary
// which provides 9 named watch face designs for the HUB75 128×64 panel.
public sealed class ServerClockWidgetRuntime : IServerWidgetRuntime
{
    private static readonly TimeZoneInfo BrasiliaTimeZone = ResolveBrasiliaTimeZone();

    private readonly PanelWidgetDefinition widget;
    private readonly bool use24Hour;
    private readonly string mostrador;

    public ServerClockWidgetRuntime(PanelWidgetDefinition widget, int panelWidth, int panelHeight)
    {
        ArgumentNullException.ThrowIfNull(widget);

        this.widget = widget.Clone();
        this.widget.Normalize(panelWidth, panelHeight);
        use24Hour = !this.widget.ConfigValues.TryGetValue("format24h", out var raw24h)
            || !bool.TryParse(raw24h, out var parsed24h)
            || parsed24h;
        mostrador = this.widget.ConfigValues.TryGetValue("mostrador", out var rawMostrador)
            && !string.IsNullOrWhiteSpace(rawMostrador)
            ? rawMostrador
            : "cyberterminal";
    }

    public string WidgetId => widget.WidgetId;

    public void Render(DateTimeOffset utcNow, RgbaColor[] targetFrame, int panelWidth, int panelHeight)
    {
        var now = TimeZoneInfo.ConvertTime(utcNow, BrasiliaTimeZone).DateTime;
        WatchfaceLibrary.Render(
            mostrador,
            targetFrame,
            panelWidth,
            panelHeight,
            widget.X,
            widget.Y,
            widget.Width,
            widget.Height,
            now,
            use24Hour);
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
}
