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

    /// <summary>
    /// Builds a compositor from <paramref name="sourcePanel"/>.
    /// </summary>
    /// <param name="sourcePanel">Panel definition to render.</param>
    /// <param name="mediaDirectory">
    /// Directory that contains uploaded media files for the device
    /// (typically <c>{StorageRoot}/media/{deviceId}/</c>). Required for
    /// <c>gifhub75</c> widgets; ignored for clock-only panels.
    /// </param>
    public static ServerSidePanelCompositor? TryCreate(
        PanelDefinition? sourcePanel,
        string? mediaDirectory = null)
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

                case "gifhub75":
                    // CA2000: use try-finally so the runtime is disposed if
                    // ownership transfer to `runtimes` is interrupted by an exception.
                    ServerGifWidgetRuntime? gifRuntime = null;
                    try
                    {
                        gifRuntime = ServerGifWidgetRuntime.TryCreate(widget, mediaDirectory);
                        if (gifRuntime is null)
                        {
                            // Media file missing or not yet uploaded — treat as RequiresClient
                            // so the device does not receive a blank frame.
                            foreach (var r in runtimes)
                            {
                                r.Dispose();
                            }
                            return null;
                        }
                        runtimes.Add(gifRuntime);
                        gifRuntime = null; // ownership transferred to runtimes list
                    }
                    finally
                    {
                        gifRuntime?.Dispose();
                    }
                    break;

                default:
                    // Any non-server-capable widget should have been rejected above by the
                    // capability classifier; if a new widget id is added without updating
                    // the classifier, fail loudly so we do not silently drop frames.
                    foreach (var r in runtimes)
                    {
                        r.Dispose();
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
