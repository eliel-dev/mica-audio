using System.Diagnostics;
using App.WinUI.Services.Gif;
using App.WinUI.Services.Visualizer;
using Visual.Win2D.Engine;

namespace App.WinUI.Views;

public partial class MainPage
{
    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-toggle-hub75-como-gate-e-runtime-em-background
    private const int HubFrameTargetFps = 30;

    private readonly Hub75VisualizerFrameRenderer hub75VisualizerFrameRenderer = new();
    private long lastHubFrameQpc;

    private void SyncHubTransportMode()
    {
        pipelineCoordinator.SetCurrentVisualizerIdentity(currentPresetId, ResolveCurrentRendererId());
        pipelineCoordinator.SetHubTransportMode(ResolveCurrentHubTransportMode());
        lastHubFrameQpc = 0;
    }

    private RendererHubTransportMode ResolveCurrentHubTransportMode()
    {
        return GetActiveRendererCapabilities().HubTransportMode;
    }

    private bool ShouldShowHubPreviewPanel()
    {
        return isPageVisible
            && (hub75ModeEnabled || contentSourceMode == GifContentSourceMode.Gif);
    }

    private bool ShouldEnableHub75DeviceOutput()
    {
        return hub75ModeEnabled
            && !suppressHub75SessionWhileInEditor
            && contentSourceMode == GifContentSourceMode.Audio;
    }

    private bool ShouldDriveHubOutputs()
    {
        return ShouldEnableHub75DeviceOutput() || ShouldShowHubPreviewPanel();
    }

    private bool ShouldRunAudioRenderLoop()
    {
        if (contentSourceMode != GifContentSourceMode.Audio)
        {
            return false;
        }

        if (isPageVisible)
        {
            return true;
        }

        return ShouldEnableHub75DeviceOutput() && ResolveCurrentHubTransportMode() == RendererHubTransportMode.Frame128x64;
    }

    private void UpdateRenderLoopState()
    {
        if (!ShouldRunAudioRenderLoop())
        {
            renderTimer?.Stop();
            return;
        }

        EnsureRenderTimerStarted();
    }

    private void ConfigureHubOutputsForCurrentState()
    {
        pipelineCoordinator.ConfigureHubOutputs(
            enableSimulator: ShouldShowHubPreviewPanel(),
            enableHub75DeviceOutput: ShouldEnableHub75DeviceOutput(),
            brightness: appSettings.Brightness);
    }

    private void RefreshVisibleCanvases()
    {
        if (!isPageVisible)
        {
            return;
        }

        MainCanvas.Invalidate();

        if (ShouldShowHubPreviewPanel())
        {
            InvalidateHubPreviews();
        }
    }

    private void PushCurrentHubFrameIfNeeded(bool forceAudioFrame = false)
    {
        if (!ShouldDriveHubOutputs())
        {
            return;
        }

        if (contentSourceMode == GifContentSourceMode.Gif)
        {
            if (ShouldShowHubPreviewPanel() && lastGifFrame is { Length: > 0 } currentGifFrame)
            {
                pipelineCoordinator.SendHubFrame(currentGifFrame, forceSimulator: true);
            }

            return;
        }

        PumpHubFrameOutput(forceAudioFrame);
    }

    private void PumpHubFrameOutput(bool force = false)
    {
        if (contentSourceMode != GifContentSourceMode.Audio)
        {
            return;
        }

        if (!ShouldDriveHubOutputs())
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
