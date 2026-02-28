using System.Diagnostics;
using Analyzer.Dsp.Analysis;
using App.WinUI.Services.Visualizer;
using App.WinUI.Views;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;
using Windows.UI;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/visual-win2d.md#galeria-de-presets
internal sealed class PresetPreviewThumbnailControl : Grid
{
    private const int BasePreviewFps = 24;
    private const int MaxHopsPerDraw = 12;

    private readonly Border frame;
    private readonly CanvasControl canvas;
    private readonly DispatcherQueueTimer timer;
    private readonly Stopwatch stopwatch = new();
    private VisualizerEngine visualizer = new();

    private PresetDefinition? preset;
    private PresetPreviewSettingsSnapshot previewSettings = PresetPreviewSettingsSnapshot.Default;
    private IAnalyzer? analyzer;
    private SpectrumFrame? lastFrame;
    private bool isRunning;
    private bool isSelected;
    private bool previewStateDirty = true;
    private float lastViewportWidth;
    private float processedSignalSeconds;

    public PresetPreviewThumbnailControl()
    {
        Width = 160;
        Height = 90;

        frame = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = UiResourceResolver.ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 5, 7, 10)),
            BorderThickness = new Thickness(1),
            BorderBrush = UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 52, 64, 80)),
        };

        canvas = new CanvasControl();
        canvas.Draw += OnDraw;
        frame.Child = canvas;
        Children.Add(frame);

        timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(1000d / BasePreviewFps);
        timer.Tick += (_, _) =>
        {
            if (isRunning)
            {
                canvas.Invalidate();
            }
        };

        Loaded += (_, _) =>
        {
            if (isRunning && !timer.IsRunning)
            {
                timer.Start();
            }
        };
        SizeChanged += (_, _) =>
        {
            previewStateDirty = true;
        };
        Unloaded += (_, _) => StopPreview();
    }

    public string PresetId => preset?.PresetId ?? string.Empty;

    public void Bind(PresetDefinition value)
    {
        preset = value;
        ResetPreviewState();
    }

    public void ApplyPreviewSettings(PresetPreviewSettingsSnapshot value)
    {
        if (value.Equals(previewSettings))
        {
            return;
        }

        previewSettings = value;
        ResetPreviewState();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        frame.BorderBrush = selected
            ? UiResourceResolver.ResolveBrush("AppAccentBrush", Color.FromArgb(255, 36, 196, 113))
            : UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 52, 64, 80));
    }

    public void StartPreview()
    {
        if (preset is null)
        {
            return;
        }

        isRunning = true;
        stopwatch.Restart();
        if (!timer.IsRunning)
        {
            timer.Start();
        }

        canvas.Invalidate();
    }

    public void StopPreview()
    {
        isRunning = false;
        if (timer.IsRunning)
        {
            timer.Stop();
        }

        stopwatch.Reset();
        ResetPreviewState();
    }

    private void ResetPreviewState()
    {
        analyzer = null;
        lastFrame = null;
        processedSignalSeconds = 0f;
        lastViewportWidth = 0f;
        previewStateDirty = true;
        visualizer = new VisualizerEngine();
        canvas.Invalidate();
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        args.DrawingSession.Clear(Color.FromArgb(255, 0, 0, 0));

        if (preset is null)
        {
            return;
        }

        var width = (float)Math.Max(1d, sender.ActualWidth);
        var height = (float)Math.Max(1d, sender.ActualHeight);

        EnsurePreviewFrame(width);
        if (isRunning)
        {
            AdvancePreviewPipeline();
        }

        if (lastFrame is not null)
        {
            visualizer.Render(
                args.DrawingSession,
                width,
                height,
                lastFrame,
                BuildRuntimePreset(),
                isRunning ? 1f / BasePreviewFps : 1f / 60f);
        }

        if (isSelected)
        {
            args.DrawingSession.DrawRectangle(
                0.5f,
                0.5f,
                Math.Max(1f, width - 1f),
                Math.Max(1f, height - 1f),
                Color.FromArgb(255, 36, 196, 113),
                1f);
        }
    }

    private void EnsurePreviewFrame(float viewportWidthPx)
    {
        if (preset is null)
        {
            return;
        }

        if (!previewStateDirty && MathF.Abs(lastViewportWidth - viewportWidthPx) <= 0.5f)
        {
            return;
        }

        previewStateDirty = false;
        lastViewportWidth = viewportWidthPx;
        visualizer = new VisualizerEngine();
        analyzer = new SpectrumAnalyzer(VisualizerAnalyzerConfigFactory.Build(BuildRuntimePreset(), previewSettings, viewportWidthPx));
        lastFrame = null;
        processedSignalSeconds = 0f;

        var hopDuration = PresetPreviewSignalFactory.HopSize / (float)PresetPreviewSignalFactory.SampleRate;
        var warmupHops = Math.Clamp((int)Math.Ceiling(previewSettings.FftSize / (double)PresetPreviewSignalFactory.HopSize) + 2, 4, 48);
        for (var i = 0; i < warmupHops; i++)
        {
            ProcessHop(processedSignalSeconds);
            processedSignalSeconds += hopDuration;
        }
    }

    private void AdvancePreviewPipeline()
    {
        if (analyzer is null || preset is null)
        {
            return;
        }

        var hopDuration = PresetPreviewSignalFactory.HopSize / (float)PresetPreviewSignalFactory.SampleRate;
        var targetSignalSeconds = (float)stopwatch.Elapsed.TotalSeconds;
        var hops = 0;

        while ((processedSignalSeconds + hopDuration) <= (targetSignalSeconds + 1e-5f) && hops < MaxHopsPerDraw)
        {
            ProcessHop(processedSignalSeconds);
            processedSignalSeconds += hopDuration;
            hops++;
        }
    }

    private void ProcessHop(float startSeconds)
    {
        if (analyzer is null || preset is null)
        {
            return;
        }

        var pcmFrame = PresetPreviewSignalFactory.CreatePcmFrame(
            preset,
            previewSettings,
            startSeconds,
            PresetPreviewSignalFactory.HopSize,
            PresetPreviewSignalFactory.SampleRate);
        var spectrumFrame = analyzer.Process(pcmFrame);
        if (spectrumFrame is not null)
        {
            lastFrame = spectrumFrame;
        }
    }

    private PresetDefinition BuildRuntimePreset()
    {
        if (preset is null)
        {
            return new PresetDefinition();
        }

        return new PresetDefinition
        {
            SchemaVersion = preset.SchemaVersion,
            PresetId = preset.PresetId,
            Name = preset.Name,
            RendererId = preset.RendererId,
            DisplayBandCount = Math.Max(8, previewSettings.DisplayBandCount),
            ScaleMode = preset.ScaleMode,
            FpsTarget = preset.FpsTarget,
            Layout = preset.Layout,
            EnableGlow = preset.EnableGlow,
            RendererParameters = new Dictionary<string, float>(preset.RendererParameters, StringComparer.OrdinalIgnoreCase),
            Palette = preset.Palette,
        };
    }
}

