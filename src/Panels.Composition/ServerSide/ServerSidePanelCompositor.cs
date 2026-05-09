using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Panels.Composition.Drawing;
using Panels.Composition.Models;

namespace Panels.Composition.ServerSide;

// Stateless compositor for server-only panels. Builds a list of widget
// runtimes from a PanelDefinition and renders frames on demand. Returns
// null when the panel contains widgets that require the WinUI client.
public sealed class ServerSidePanelCompositor : IDisposable
{
    public const int TargetFps = 30;

    private readonly PanelDefinition panel;
    private readonly List<IServerWidgetRuntime> widgetRuntimes;

    private ServerSidePanelCompositor(PanelDefinition normalizedPanel, List<IServerWidgetRuntime> runtimes)
    {
        panel = normalizedPanel;
        widgetRuntimes = runtimes;
    }

    public PanelDefinition Panel => panel.Clone();

    public static ServerSidePanelCompositor? TryCreate(PanelDefinition? sourcePanel)
    {
        if (sourcePanel is null)
        {
            return null;
        }

        var capability = PanelServerCapabilityClassifier.Classify(sourcePanel);
        if (capability == PanelServerCapability.RequiresClient)
        {
            return null;
        }

        var normalized = sourcePanel.Clone();
        normalized.Normalize();

        var runtimes = new List<IServerWidgetRuntime>(normalized.Widgets.Count);
        foreach (var widget in normalized.Widgets.OrderBy(static w => w.ZIndex))
        {
            var appId = widget.AppId?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (appId)
            {
                case "analogclock":
                    runtimes.Add(new ServerClockWidgetRuntime(widget, normalized.Width, normalized.Height));
                    break;
                default:
                    // Any non-server-capable widget should have been rejected above by the
                    // capability classifier; if a new widget id is added without updating
                    // the classifier, fail loudly so we do not silently drop frames.
                    foreach (var runtime in runtimes)
                    {
                        runtime.Dispose();
                    }
                    return null;
            }
        }

        return new ServerSidePanelCompositor(normalized, runtimes);
    }

    public RgbaColor[] RenderFrame(DateTimeOffset utcNow)
    {
        var frame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        RenderFrameInto(utcNow, frame);
        return frame;
    }

    public void RenderFrameInto(DateTimeOffset utcNow, RgbaColor[] targetFrame)
    {
        ArgumentNullException.ThrowIfNull(targetFrame);
        if (targetFrame.Length != LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        {
            throw new ArgumentException(
                $"Target frame has {targetFrame.Length} pixels but expected {LedDefaults.MatrixWidth * LedDefaults.MatrixHeight}.",
                nameof(targetFrame));
        }

        PanelsMatrixDrawHelpers.Clear(targetFrame);
        foreach (var runtime in widgetRuntimes)
        {
            runtime.Render(utcNow, targetFrame, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight);
        }
    }

    public void Dispose()
    {
        foreach (var runtime in widgetRuntimes)
        {
            runtime.Dispose();
        }

        widgetRuntimes.Clear();
    }
}
