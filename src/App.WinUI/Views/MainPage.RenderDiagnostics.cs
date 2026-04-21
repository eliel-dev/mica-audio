using System.Diagnostics;
using Microsoft.UI.Xaml.Input;

namespace App.WinUI.Views;

public partial class MainPage
{
    private const double RenderDiagnosticsRefreshSeconds = 0.5d;
    private const double RenderDiagnosticsTargetFrameMs = 1000d / 60d;
    private const double RenderDiagnosticsFrameSlackMs = 0.5d;

    private bool renderDiagnosticsEnabled;
    private int renderDiagnosticsFrameCount;
    private int renderDiagnosticsOverBudgetCount;
    private double renderDiagnosticsAccumulatedFrameMs;
    private double renderDiagnosticsLastFrameMs;
    private double renderDiagnosticsWorstFrameMs;
    private long renderDiagnosticsWindowStartQpc;

    private void OnRenderDiagnosticsAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ToggleRenderDiagnostics();
        args.Handled = true;
    }

    private void ToggleRenderDiagnostics()
    {
        renderDiagnosticsEnabled = !renderDiagnosticsEnabled;
        ResetRenderDiagnosticsWindow();

        if (!renderDiagnosticsEnabled)
        {
            RenderDiagnosticsHud.Visibility = Visibility.Collapsed;
            return;
        }

        RenderDiagnosticsText.Text = "Medindo FPS do canvas principal...";
        RenderDiagnosticsHud.Visibility = Visibility.Visible;
        StatusText.Text = "Diagnostico de FPS ativo (Ctrl+Shift+D para ocultar).";
    }

    private void TrackRenderDiagnostics(float deltaSeconds)
    {
        if (!renderDiagnosticsEnabled)
        {
            return;
        }

        var frameMs = Math.Max(0.001d, deltaSeconds * 1000d);
        var now = Stopwatch.GetTimestamp();

        if (renderDiagnosticsWindowStartQpc == 0)
        {
            renderDiagnosticsWindowStartQpc = now;
        }

        renderDiagnosticsFrameCount++;
        renderDiagnosticsAccumulatedFrameMs += frameMs;
        renderDiagnosticsLastFrameMs = frameMs;
        renderDiagnosticsWorstFrameMs = Math.Max(renderDiagnosticsWorstFrameMs, frameMs);

        if (frameMs > RenderDiagnosticsTargetFrameMs + RenderDiagnosticsFrameSlackMs)
        {
            renderDiagnosticsOverBudgetCount++;
        }

        var elapsedSeconds = (double)(now - renderDiagnosticsWindowStartQpc) / Stopwatch.Frequency;
        if (elapsedSeconds < RenderDiagnosticsRefreshSeconds)
        {
            return;
        }

        var averageFps = renderDiagnosticsFrameCount / elapsedSeconds;
        var averageFrameMs = renderDiagnosticsAccumulatedFrameMs / Math.Max(1, renderDiagnosticsFrameCount);
        var instantFps = 1000d / renderDiagnosticsLastFrameMs;
        RenderDiagnosticsText.Text = string.Format(
            global::System.Globalization.CultureInfo.InvariantCulture,
            "FPS {0:0.0} avg | {1:0.0} inst\nFrame {2:0.00} ms avg | pico {3:0.00} ms\n>{4:0.00} ms: {5}",
            averageFps,
            instantFps,
            averageFrameMs,
            renderDiagnosticsWorstFrameMs,
            RenderDiagnosticsTargetFrameMs,
            renderDiagnosticsOverBudgetCount);

        ResetRenderDiagnosticsWindow(now);
    }

    private void ResetRenderDiagnosticsWindow(long now = 0)
    {
        renderDiagnosticsFrameCount = 0;
        renderDiagnosticsOverBudgetCount = 0;
        renderDiagnosticsAccumulatedFrameMs = 0d;
        renderDiagnosticsLastFrameMs = 0d;
        renderDiagnosticsWorstFrameMs = 0d;
        renderDiagnosticsWindowStartQpc = now == 0 ? Stopwatch.GetTimestamp() : now;
    }
}