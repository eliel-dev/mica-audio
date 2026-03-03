using System.Collections.Generic;
using System.Reflection;
using App.WinUI.Views;
using BenchmarkDotNet.Attributes;
using MicaAudio.Core.Presets;
using Microsoft.Graphics.Canvas;
using Microsoft.VSDiagnostics;

namespace App.WinUI.Benchmarks;
[CPUUsageDiagnoser]
public class MainPageDrawHubFrameBenchmark
{
    private const int MatrixWidth = 64;
    private const int MatrixHeight = 32;
    private static readonly Action<CanvasDrawingSession, float, float, IReadOnlyList<RgbaColor>, int, int> DrawHubFrameDelegate = CreateDrawHubFrameDelegate();
    private readonly RgbaColor[] pixels = new RgbaColor[MatrixWidth * MatrixHeight];
    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        for (var i = 0; i < pixels.Length; i++)
        {
            var alpha = (byte)(random.NextDouble() < 0.1 ? 0 : 255);
            pixels[i] = new RgbaColor((byte)random.Next(16, 240), (byte)random.Next(16, 240), (byte)random.Next(16, 240), alpha);
        }
    }

    [Benchmark]
    public void DrawHubFrame()
    {
        var device = CanvasDevice.GetSharedDevice();
        using var renderTarget = new CanvasRenderTarget(device, 640, 320, 96);
        using var drawingSession = renderTarget.CreateDrawingSession();
        DrawHubFrameDelegate(drawingSession, 640f, 320f, pixels, MatrixWidth, MatrixHeight);
    }

    private static Action<CanvasDrawingSession, float, float, IReadOnlyList<RgbaColor>, int, int> CreateDrawHubFrameDelegate()
    {
        var method = typeof(MainPage).GetMethod("DrawHubFrame", BindingFlags.NonPublic | BindingFlags.Static, binder: null, [typeof(CanvasDrawingSession), typeof(float), typeof(float), typeof(IReadOnlyList<RgbaColor>), typeof(int), typeof(int)], modifiers: null) ?? throw new InvalidOperationException("MainPage.DrawHubFrame method not found.");
        return (Action<CanvasDrawingSession, float, float, IReadOnlyList<RgbaColor>, int, int>)method.CreateDelegate(typeof(Action<CanvasDrawingSession, float, float, IReadOnlyList<RgbaColor>, int, int>));
    }
}
