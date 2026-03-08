using System.Diagnostics;
using App.WinUI.Services.Gif;
using App.WinUI.Services.Visualizer;
using Visual.Win2D.Engine;

namespace App.WinUI.Views;

public partial class MainPage
{
    private const int HubFrameTargetFps = 30;

    private readonly Hub75VisualizerFrameRenderer hub75VisualizerFrameRenderer = new();
    private long lastHubFrameQpc;

    private void SyncHubTransportMode()
    {
        pipelineCoordinator.SetHubTransportMode(ResolveCurrentHubTransportMode());
        lastHubFrameQpc = 0;
    }

    private RendererHubTransportMode ResolveCurrentHubTransportMode()
    {
        return GetActiveRendererCapabilities().HubTransportMode;
    }

    private void PumpHubFrameOutput(bool force = false)
    {
        if (contentSourceMode != GifContentSourceMode.Audio)
        {
            return;
        }

        if (ResolveCurrentHubTransportMode() != RendererHubTransportMode.Frame128x64)
        {
            return;
        }

        var frame = pipelineCoordinator.LatestFrame;
        if (frame is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var targetIntervalSeconds = 1f / HubFrameTargetFps;
        var elapsedSeconds = lastHubFrameQpc == 0
            ? targetIntervalSeconds
            : (float)(now - lastHubFrameQpc) / Stopwatch.Frequency;

        if (!force && elapsedSeconds < targetIntervalSeconds)
        {
            return;
        }

        lastHubFrameQpc = now;

        if (!hub75VisualizerFrameRenderer.TryRender(frame, BuildRuntimePreset(), elapsedSeconds, out var hubPixels))
        {
            return;
        }

        pipelineCoordinator.SendHubFrame(hubPixels);
    }

    private void DisposeHubFrameRenderer()
    {
        hub75VisualizerFrameRenderer.Dispose();
    }
}
