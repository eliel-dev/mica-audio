using System.Runtime.InteropServices;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Microsoft.Graphics.Canvas;
using Visual.Win2D.Engine;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#hub75-frame-transport
internal sealed class Hub75VisualizerFrameRenderer : IDisposable
{
    private readonly VisualizerEngine visualizer = new();
    private CanvasRenderTarget? renderTarget;

    public bool TryRender(SpectrumFrame frame, PresetDefinition preset, float deltaSeconds, out RgbaColor[] pixels)
    {
        try
        {
            EnsureRenderTarget();

            using var drawingSession = renderTarget!.CreateDrawingSession();
            visualizer.Render(
                drawingSession,
                LedDefaults.MatrixWidth,
                LedDefaults.MatrixHeight,
                frame,
                preset,
                deltaSeconds);

            var sourcePixels = renderTarget.GetPixelColors();
            pixels = new RgbaColor[sourcePixels.Length];
            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var color = sourcePixels[i];
                pixels[i] = new RgbaColor(color.R, color.G, color.B, color.A);
            }

            return pixels.Length == (LedDefaults.MatrixWidth * LedDefaults.MatrixHeight);
        }
        catch (COMException)
        {
            DisposeRenderTarget();
        }
        catch (ObjectDisposedException)
        {
            DisposeRenderTarget();
        }

        pixels = Array.Empty<RgbaColor>();
        return false;
    }

    public void Dispose()
    {
        DisposeRenderTarget();
        GC.SuppressFinalize(this);
    }

    private void EnsureRenderTarget()
    {
        renderTarget ??= new CanvasRenderTarget(
            CanvasDevice.GetSharedDevice(),
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight,
            96f);
    }

    private void DisposeRenderTarget()
    {
        renderTarget?.Dispose();
        renderTarget = null;
    }
}
