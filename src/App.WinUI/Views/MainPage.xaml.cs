using System.Diagnostics;
using Analyzer.Dsp.Analysis;
using App.WinUI.Services;
using Audio.Loopback.Capture;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;
using Visual.Win2D.Engine;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace App.WinUI.Views;

public partial class MainPage : Page
{
    private const string AudioMotionClonePresetId = "audiomotion-clone";
    private const float DefaultMinDecibels = -85f;
    private const float DefaultMaxDecibels = -25f;
    private const float DefaultLinearBoost = 1.6f;

    private readonly VisualizerEngine visualizer = new();
    private readonly ILoopbackCapture capture = new WasapiLoopbackCaptureService();
    private readonly SimulatorLedOutput simulatorLedOutput = new();
    private readonly NullLedOutput nullLedOutput = new();

    private readonly PresetRepository presetRepository;
    private readonly SettingsRepository settingsRepository;
    private readonly Dictionary<string, PresetDefinition> presetsById = new(StringComparer.OrdinalIgnoreCase);

    private IAnalyzer analyzer = new SpectrumAnalyzer(new AnalyzerConfig());
    private ILedOutput ledOutput;

    private CancellationTokenSource? pipelineCts;
    private Task? pipelineTask;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? renderTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? cloneViewportDebounceTimer;
    private AppWindow? appWindow;

    private SpectrumFrame? latestFrame;
    private AppSettings appSettings = new();
    private PresetDefinition activePreset = new();
    private string selectedRendererId = RendererIds.AudioMotionClone;
    private string currentPresetId = AudioMotionClonePresetId;

    private long lastRenderQpc;
    private float lastCloneViewportWidth;
    private bool initialized;
    private bool running;
    private bool hubPreviewEnabled;
    private bool fullscreen;
    private bool suppressSensitivityMinChanged;
    private bool suppressSensitivityMaxChanged;
    private bool suppressLinearBoostChanged;
    private bool suppressBarCountChanged;
    private bool suppressFftSizeChanged;
    private bool suppressFftSmoothingChanged;
    private bool suppressWeightingFilterChanged;
    private bool suppressFrequencyScaleChanged;
    private bool suppressFrequencyMinChanged;
    private bool suppressFrequencyMaxChanged;
    private float sensitivityMinDb = DefaultMinDecibels;
    private float sensitivityMaxDb = DefaultMaxDecibels;
    private float linearBoost = DefaultLinearBoost;
    private int displayBandCount = 38;
    private int fftSize = 1024;
    private float fftSmoothing = 0.8f;
    private WeightingFilter weightingFilter = WeightingFilter.Off;
    private FrequencyScale frequencyScale = FrequencyScale.Logarithmic;
    private float frequencyMinHz = 30f;
    private float frequencyMaxHz = 16_000f;

    private static readonly int[] FftSizeOptions = FftSizePolicy.UiSupportedSizes;

    private static readonly float[] FrequencyMinOptions =
    [
        16f, 20f, 30f, 40f, 60f, 80f, 100f, 160f, 200f, 250f, 315f, 400f, 500f, 630f, 800f, 1000f,
    ];

    private static readonly float[] FrequencyMaxOptions =
    [
        250f, 315f, 400f, 500f, 630f, 800f, 1000f, 1250f, 1600f, 2000f, 2500f, 3150f, 4000f, 5000f,
        6300f, 8000f, 10_000f, 12_000f, 16_000f, 20_000f,
    ];

    public MainPage()
    {
        InitializeComponent();
        ConfigureSliderDefaults();

        var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MicaAudio");
        presetRepository = new PresetRepository(appDataRoot);
        settingsRepository = new SettingsRepository(appDataRoot);
        ledOutput = nullLedOutput;

        capture.StatusChanged += OnCaptureStatusChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void ConfigureSliderDefaults()
    {
        LinearBoostSlider.Minimum = 1d;
        LinearBoostSlider.Maximum = 3d;
        LinearBoostSlider.SmallChange = 0.1d;
        LinearBoostSlider.StepFrequency = 0.1d;

        FftSmoothingSlider.Minimum = 0d;
        FftSmoothingSlider.Maximum = 0.99d;
        FftSmoothingSlider.SmallChange = 0.01d;
        FftSmoothingSlider.StepFrequency = 0.01d;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        await InitializeAsync();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        await StopPipelineAsync();
        capture.StatusChanged -= OnCaptureStatusChanged;
        renderTimer?.Stop();
        cloneViewportDebounceTimer?.Stop();

        SaveWindowSizeIntoSettings();
        await settingsRepository.SaveAsync(appSettings);
        await capture.DisposeAsync();
    }

    private async Task InitializeAsync()
    {
        appSettings = await settingsRepository.LoadAsync().ConfigureAwait(false);
        appSettings = MigrateSettings(appSettings);
        var presets = await presetRepository.LoadOrSeedAsync().ConfigureAwait(false);

        presetsById.Clear();
        foreach (var preset in presets)
        {
            presetsById[preset.PresetId] = preset;
        }

        await DispatcherQueue.EnqueueAsync(() =>
        {
            appWindow = GetAppWindow();
            EnsureVisibleUiState();
            RestoreWindowSize();

            PopulatePresetCombo();
            PopulateRendererCombo();
            PopulateFftSizeCombo();
            PopulateWeightingFilterCombo();
            PopulateFrequencyScaleCombo();
            PopulateFrequencyRangeCombos();

            activePreset = ResolveActivePreset(appSettings.ActivePresetId);
            currentPresetId = activePreset.PresetId;
            selectedRendererId = activePreset.RendererId;
            sensitivityMinDb = CoerceSensitivityMinDb(appSettings.SensitivityMinDb);
            sensitivityMaxDb = CoerceSensitivityMaxDb(appSettings.SensitivityMaxDb);
            linearBoost = CoerceLinearBoost(appSettings.LinearBoost);
            EnsureSensitivityDbOrder();
            displayBandCount = appSettings.BarCount;
            fftSize = CoerceFftSize(appSettings.FftSize);
            fftSmoothing = CoerceFftSmoothing(appSettings.FftSmoothing);
            weightingFilter = CoerceWeightingFilter(appSettings.WeightingFilter);
            frequencyScale = CoerceFrequencyScale(appSettings.FrequencyScale);
            frequencyMinHz = CoerceFrequencyMin(appSettings.FrequencyMinHz);
            frequencyMaxHz = CoerceFrequencyMax(appSettings.FrequencyMaxHz);
            EnsureFrequencyRangeOrder();

            SelectComboOption(PresetCombo, activePreset.PresetId);
            SelectComboOption(RendererCombo, selectedRendererId);
            suppressSensitivityMinChanged = true;
            SensitivityMinDbSlider.Value = sensitivityMinDb;
            suppressSensitivityMinChanged = false;
            suppressSensitivityMaxChanged = true;
            SensitivityMaxDbSlider.Value = sensitivityMaxDb;
            suppressSensitivityMaxChanged = false;
            suppressLinearBoostChanged = true;
            LinearBoostSlider.Value = linearBoost;
            suppressLinearBoostChanged = false;
            suppressFftSizeChanged = true;
            SelectComboOption(FftSizeCombo, FormatFftSizeId(fftSize));
            suppressFftSizeChanged = false;
            suppressFftSmoothingChanged = true;
            FftSmoothingSlider.Value = fftSmoothing;
            suppressFftSmoothingChanged = false;
            suppressWeightingFilterChanged = true;
            SelectComboOption(WeightingFilterCombo, ToWeightingFilterId(weightingFilter));
            suppressWeightingFilterChanged = false;
            suppressFrequencyScaleChanged = true;
            SelectComboOption(FrequencyScaleCombo, frequencyScale.ToString());
            suppressFrequencyScaleChanged = false;
            UpdateFftSmoothingText();
            UpdateFrequencyScaleToolTip();
            UpdateFrequencyRangeCombos();
            UpdateSensitivityDbTexts();
            UpdateLinearBoostText();
            UpdateCloneModeUi();

            hubPreviewEnabled = appSettings.Hub75PreviewEnabled;
            Hub75Toggle.IsOn = hubPreviewEnabled;
            UpdateHubPreviewVisibility();

            lastCloneViewportWidth = GetAnalyzerViewportWidth();
            Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
            appSettings = CopySettings(
                ActivePresetId: currentPresetId,
                SensitivityMinDb: sensitivityMinDb,
                SensitivityMaxDb: sensitivityMaxDb,
                LinearBoost: linearBoost,
                Sensitivity: sensitivityMaxDb,
                FftSize: fftSize,
                FftSmoothing: fftSmoothing,
                WeightingFilter: weightingFilter,
                FrequencyScale: frequencyScale,
                FrequencyMinHz: frequencyMinHz,
                FrequencyMaxHz: frequencyMaxHz,
                BarCount: displayBandCount);
            StatusText.Text = "Pronto";
        });

        await DispatcherQueue.EnqueueAsync(() =>
        {
            SetupRenderTimer();
        });

        await StartPipelineAsync().ConfigureAwait(false);
    }

    private void SetupRenderTimer()
    {
        renderTimer = DispatcherQueue.CreateTimer();
        renderTimer.Interval = TimeSpan.FromMilliseconds(1000d / 60d);
        renderTimer.Tick += (_, _) =>
        {
            MainCanvas.Invalidate();
            if (hubPreviewEnabled)
            {
                HubCanvas.Invalidate();
            }
        };
        renderTimer.Start();
        EnsureCloneViewportDebounceTimer();
    }

    private void ScheduleCloneViewportAnalyzerRebuild()
    {
        if (!initialized || !IsClonePresetActive())
        {
            return;
        }

        EnsureCloneViewportDebounceTimer();
        var timer = cloneViewportDebounceTimer!;
        timer.Stop();
        timer.Interval = TimeSpan.FromMilliseconds(80);
        timer.Start();
    }

    private void EnsureCloneViewportDebounceTimer()
    {
        if (cloneViewportDebounceTimer is not null)
        {
            return;
        }

        cloneViewportDebounceTimer = DispatcherQueue.CreateTimer();
        cloneViewportDebounceTimer.Interval = TimeSpan.FromMilliseconds(80);
        cloneViewportDebounceTimer.Tick += (_, _) =>
        {
            cloneViewportDebounceTimer?.Stop();
            if (!IsClonePresetActive())
            {
                return;
            }

            Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        };
    }

    private async Task StartPipelineAsync()
    {
        if (running)
        {
            return;
        }

        await DispatcherQueue.EnqueueAsync(() =>
        {
            Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
            lastRenderQpc = 0;
            latestFrame = null;
        });

        pipelineCts = new CancellationTokenSource();

        try
        {
            await capture.StartAsync(new CaptureConfig
            {
                TargetSampleRate = 48_000,
                TargetChannels = 1,
                ChannelCapacity = 8,
                BufferMilliseconds = 12,
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await DispatcherQueue.EnqueueAsync(() =>
            {
                StatusText.Text = $"Erro de audio: {ex.Message}";
            });
            pipelineCts.Dispose();
            pipelineCts = null;
            return;
        }

        ledOutput = hubPreviewEnabled ? simulatorLedOutput : nullLedOutput;
        ledOutput.Start(new LedOutputConfig
        {
            Width = 64,
            Height = 32,
            Brightness = appSettings.Brightness,
        });

        pipelineTask = Task.Run(() => PipelineLoopAsync(pipelineCts.Token));
        running = true;

        await DispatcherQueue.EnqueueAsync(() =>
        {
            StatusText.Text = "Executando a 60 FPS";
        });
    }

    private async Task StopPipelineAsync()
    {
        if (!running)
        {
            return;
        }

        pipelineCts?.Cancel();

        try
        {
            await capture.StopAsync().ConfigureAwait(false);
            if (pipelineTask is not null)
            {
                await pipelineTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            pipelineCts?.Dispose();
            pipelineCts = null;
            pipelineTask = null;
            running = false;
            ledOutput.Stop();
        }

        await DispatcherQueue.EnqueueAsync(() =>
        {
            StatusText.Text = "Parado";
        });
    }

    private async Task PipelineLoopAsync(CancellationToken cancellationToken)
    {
        var reader = capture.Frames;

        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var pcmFrame))
            {
                var currentAnalyzer = Volatile.Read(ref analyzer);
                var spectrum = currentAnalyzer.Process(in pcmFrame);
                if (spectrum is null)
                {
                    continue;
                }

                Volatile.Write(ref latestFrame, spectrum);

                if (!hubPreviewEnabled)
                {
                    continue;
                }

                ledOutput.Send(new LedPayload
                {
                    Bins64 = spectrum.Bands64,
                    Level = spectrum.Level,
                    PresetId = currentPresetId,
                });
            }
        }
    }

    private void OnMainCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var frame = Volatile.Read(ref latestFrame);
        if (frame is null)
        {
            args.DrawingSession.Clear(Color.FromArgb(255, 0, 0, 0));
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var deltaSeconds = lastRenderQpc == 0
            ? 1f / 60f
            : (float)(now - lastRenderQpc) / Stopwatch.Frequency;
        lastRenderQpc = now;

        var preset = BuildRuntimePreset();
        visualizer.Render(args.DrawingSession, (float)sender.ActualWidth, (float)sender.ActualHeight, frame, preset, deltaSeconds);
    }

    private void OnMainCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!initialized || !IsClonePresetActive())
        {
            return;
        }

        var width = (float)e.NewSize.Width;
        if (width <= 1f || MathF.Abs(width - lastCloneViewportWidth) < 1f)
        {
            return;
        }

        lastCloneViewportWidth = width;
        ScheduleCloneViewportAnalyzerRebuild();
    }

    private void OnHubCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!hubPreviewEnabled)
        {
            args.DrawingSession.Clear(Color.FromArgb(255, 8, 10, 14));
            return;
        }

        var snapshot = simulatorLedOutput.GetFrameSnapshot();
        if (snapshot.Length == 0)
        {
            args.DrawingSession.Clear(Color.FromArgb(255, 8, 10, 14));
            return;
        }

        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;
        var matrixWidth = (float)LedDefaults.MatrixWidth;
        var matrixHeight = (float)LedDefaults.MatrixHeight;
        var matrixAspect = matrixWidth / matrixHeight;
        var canvasAspect = width <= 0f || height <= 0f ? matrixAspect : (width / height);
        var drawWidth = width;
        var drawHeight = height;

        if (canvasAspect > matrixAspect)
        {
            drawWidth = height * matrixAspect;
            drawHeight = height;
        }
        else
        {
            drawWidth = width;
            drawHeight = width / matrixAspect;
        }

        var offsetX = (width - drawWidth) * 0.5f;
        var offsetY = (height - drawHeight) * 0.5f;
        var cellW = drawWidth / matrixWidth;
        var cellH = drawHeight / matrixHeight;

        args.DrawingSession.Clear(Color.FromArgb(255, 8, 10, 14));

        for (var y = 0; y < LedDefaults.MatrixHeight; y++)
        {
            for (var x = 0; x < LedDefaults.MatrixWidth; x++)
            {
                var pixel = snapshot[(y * LedDefaults.MatrixWidth) + x];
                if (pixel.A == 0)
                {
                    continue;
                }

                var color = Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B);
                args.DrawingSession.FillRectangle(
                    offsetX + (x * cellW),
                    offsetY + (y * cellH),
                    Math.Max(1f, cellW),
                    Math.Max(1f, cellH),
                    color);
            }
        }
    }

    private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is not ComboOption option || !presetsById.TryGetValue(option.Id, out var preset))
        {
            return;
        }

        activePreset = preset;
        currentPresetId = preset.PresetId;
        selectedRendererId = preset.RendererId;

        SelectComboOption(RendererCombo, selectedRendererId);
        UpdateCloneModeUi();
        lastCloneViewportWidth = GetAnalyzerViewportWidth();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));

        appSettings = CopySettings(ActivePresetId: currentPresetId);
    }

    private void OnRendererSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsClonePresetActive())
        {
            selectedRendererId = RendererIds.AudioMotionClone;
            return;
        }

        if (RendererCombo.SelectedItem is not ComboOption option)
        {
            return;
        }

        selectedRendererId = option.Id;
    }

    private void OnHub75Toggled(object sender, RoutedEventArgs e)
    {
        hubPreviewEnabled = Hub75Toggle.IsOn;
        UpdateHubPreviewVisibility();

        ledOutput = hubPreviewEnabled ? simulatorLedOutput : nullLedOutput;
        ledOutput.Start(new LedOutputConfig
        {
            Width = 64,
            Height = 32,
            Brightness = appSettings.Brightness,
        });

        appSettings = CopySettings(Hub75PreviewEnabled: hubPreviewEnabled);
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (fullscreen)
        {
            return;
        }

        SettingsSplitView.IsPaneOpen = !SettingsSplitView.IsPaneOpen;
    }

    private void OnSettingsPaneCloseClicked(object sender, RoutedEventArgs e)
    {
        SettingsSplitView.IsPaneOpen = false;
    }

    private void OnSensitivityMinDbChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressSensitivityMinChanged)
        {
            return;
        }

        sensitivityMinDb = CoerceSensitivityMinDb((float)e.NewValue);
        EnsureSensitivityDbOrder();
        suppressSensitivityMinChanged = true;
        SensitivityMinDbSlider.Value = sensitivityMinDb;
        suppressSensitivityMinChanged = false;
        suppressSensitivityMaxChanged = true;
        SensitivityMaxDbSlider.Value = sensitivityMaxDb;
        suppressSensitivityMaxChanged = false;
        UpdateSensitivityDbTexts();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(SensitivityMinDb: sensitivityMinDb, SensitivityMaxDb: sensitivityMaxDb, Sensitivity: sensitivityMaxDb);
    }

    private void OnSensitivityMaxDbChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressSensitivityMaxChanged)
        {
            return;
        }

        sensitivityMaxDb = CoerceSensitivityMaxDb((float)e.NewValue);
        EnsureSensitivityDbOrder();
        suppressSensitivityMinChanged = true;
        SensitivityMinDbSlider.Value = sensitivityMinDb;
        suppressSensitivityMinChanged = false;
        suppressSensitivityMaxChanged = true;
        SensitivityMaxDbSlider.Value = sensitivityMaxDb;
        suppressSensitivityMaxChanged = false;
        UpdateSensitivityDbTexts();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(SensitivityMinDb: sensitivityMinDb, SensitivityMaxDb: sensitivityMaxDb, Sensitivity: sensitivityMaxDb);
    }

    private void OnLinearBoostChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressLinearBoostChanged)
        {
            return;
        }

        linearBoost = CoerceLinearBoost((float)e.NewValue);
        suppressLinearBoostChanged = true;
        LinearBoostSlider.Value = linearBoost;
        suppressLinearBoostChanged = false;
        UpdateLinearBoostText();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(LinearBoost: linearBoost);
    }

    private void OnBarCountChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressBarCountChanged || IsClonePresetActive())
        {
            return;
        }

        var minBands = Math.Max(8, activePreset.DisplayBandCount);
        displayBandCount = (int)Math.Clamp(Math.Round(e.NewValue), minBands, 128);
        ApplyBandCountBounds();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(BarCount: displayBandCount);
    }

    private void OnFftSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressFftSizeChanged)
        {
            return;
        }

        if (FftSizeCombo.SelectedItem is not ComboOption option
            || !int.TryParse(
                option.Id,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var selectedFftSize))
        {
            return;
        }

        fftSize = CoerceFftSize(selectedFftSize);
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(FftSize: fftSize);
    }

    private void OnFftSmoothingChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressFftSmoothingChanged)
        {
            return;
        }

        fftSmoothing = CoerceFftSmoothing((float)e.NewValue);
        UpdateFftSmoothingText();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(FftSmoothing: fftSmoothing);
    }

    private void OnWeightingFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressWeightingFilterChanged)
        {
            return;
        }

        if (WeightingFilterCombo.SelectedItem is not ComboOption option)
        {
            return;
        }

        weightingFilter = ParseWeightingFilterId(option.Id);
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(WeightingFilter: weightingFilter);
    }

    private void OnFrequencyScaleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressFrequencyScaleChanged)
        {
            return;
        }

        if (FrequencyScaleCombo.SelectedItem is not ComboOption option
            || !Enum.TryParse<FrequencyScale>(option.Id, ignoreCase: true, out var selectedScale))
        {
            return;
        }

        frequencyScale = CoerceFrequencyScale(selectedScale);
        UpdateFrequencyScaleToolTip();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(FrequencyScale: frequencyScale);
    }

    private void OnFrequencyMinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressFrequencyMinChanged)
        {
            return;
        }

        if (FrequencyMinCombo.SelectedItem is not ComboOption option
            || !TryParseFrequencyOption(option.Id, out var selectedMin))
        {
            return;
        }

        frequencyMinHz = selectedMin;
        EnsureFrequencyRangeOrder();
        UpdateFrequencyRangeCombos();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(FrequencyMinHz: frequencyMinHz, FrequencyMaxHz: frequencyMaxHz);
    }

    private void OnFrequencyMaxChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressFrequencyMaxChanged)
        {
            return;
        }

        if (FrequencyMaxCombo.SelectedItem is not ComboOption option
            || !TryParseFrequencyOption(option.Id, out var selectedMax))
        {
            return;
        }

        frequencyMaxHz = selectedMax;
        EnsureFrequencyRangeOrder();
        UpdateFrequencyRangeCombos();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
        appSettings = CopySettings(FrequencyMinHz: frequencyMinHz, FrequencyMaxHz: frequencyMaxHz);
    }

    private void OnFullscreenClicked(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen(!fullscreen);
    }

    private void OnFullscreenAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (sender.Key == Windows.System.VirtualKey.F11)
        {
            ToggleFullscreen(!fullscreen);
            args.Handled = true;
            return;
        }

        if (sender.Key == Windows.System.VirtualKey.Escape && fullscreen)
        {
            ToggleFullscreen(false);
            args.Handled = true;
        }
    }

    private void ToggleFullscreen(bool enable)
    {
        appWindow ??= GetAppWindow();
        if (appWindow is null)
        {
            return;
        }

        fullscreen = enable;
        if (fullscreen)
        {
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else
        {
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }

        UpdateFullscreenUiState();
    }

    private void UpdateFullscreenUiState()
    {
        ControlsPanel.Visibility = fullscreen ? Visibility.Collapsed : Visibility.Visible;
        FullscreenButton.Content = fullscreen ? "Janela" : "Tela cheia";
        VisualizerLayout.Margin = fullscreen ? new Thickness(0) : new Thickness(12, 0, 12, 12);
        MainCanvasBorder.CornerRadius = fullscreen ? new CornerRadius(0) : new CornerRadius(12);
        if (fullscreen)
        {
            SettingsSplitView.IsPaneOpen = false;
        }
        UpdateHubPreviewVisibility();
    }

    private PresetDefinition BuildRuntimePreset()
    {
        var rendererId = IsClonePreset(activePreset) ? RendererIds.AudioMotionClone : selectedRendererId;
        return new PresetDefinition
        {
            SchemaVersion = activePreset.SchemaVersion,
            PresetId = activePreset.PresetId,
            Name = activePreset.Name,
            RendererId = rendererId,
            DisplayBandCount = displayBandCount,
            ScaleMode = activePreset.ScaleMode,
            FpsTarget = activePreset.FpsTarget,
            Layout = activePreset.Layout,
            EnableGlow = activePreset.EnableGlow,
            RendererParameters = new Dictionary<string, float>(activePreset.RendererParameters, StringComparer.OrdinalIgnoreCase),
            Palette = activePreset.Palette,
        };
    }

    private IAnalyzer CreateAnalyzer(PresetDefinition preset)
    {
        var cloneMode = IsClonePreset(preset);
        var viewportWidth = GetAnalyzerViewportWidth();
        var barSpace = preset.RendererParameters.TryGetValue("barSpace", out var configuredBarSpace)
            ? configuredBarSpace
            : 0.10f;

        return new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = fftSize,
            HopSize = 256,
            DisplayBandCount = preset.DisplayBandCount,
            DisplayMode = cloneMode ? DisplayMode.AudioMotionMode0 : DisplayMode.FixedBands,
            DisplayViewportWidthPx = cloneMode ? MathF.Max(2f, viewportWidth) : 0f,
            BarSpace = Math.Clamp(barSpace, 0f, 0.95f),
            OutputBandCount = LedDefaults.MatrixWidth,
            MinHz = frequencyMinHz,
            MaxHz = frequencyMaxHz,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = frequencyScale,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            UseLinearAmplitude = true,
            LinearBoost = linearBoost,
            MinDecibels = sensitivityMinDb,
            MaxDecibels = sensitivityMaxDb,
            DbFloor = sensitivityMinDb,
            DbCeiling = sensitivityMaxDb,
            DisplaySmoothingRise = 0.82f,
            DisplaySmoothingFall = 0.06f,
            DisplayMotionDamping = 0.30f,
            OutputSmoothingRise = 0.82f,
            OutputSmoothingFall = 0.06f,
            OutputMotionDamping = 0.30f,
            InputGain = 1f,
        });
    }

    private void PopulatePresetCombo()
    {
        PresetCombo.ItemsSource = presetsById.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ComboOption(p.PresetId, p.Name))
            .ToList();
    }

    private void PopulateRendererCombo()
    {
        RendererCombo.ItemsSource = visualizer.Renderers
            .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ComboOption(r.RendererId, r.DisplayName))
            .ToList();
    }

    private void PopulateFftSizeCombo()
    {
        FftSizeCombo.ItemsSource = FftSizeOptions
            .Select(v => new ComboOption(FormatFftSizeId(v), FormatFftSizeLabel(v)))
            .ToList();
    }

    private void PopulateWeightingFilterCombo()
    {
        WeightingFilterCombo.ItemsSource = new List<ComboOption>
        {
            new("OFF", "Off", "Nao aplica ponderacao psicoacustica."),
            new("A", "A", "Atenua graves e agudos extremos, aproximando a audicao em nivel moderado."),
            new("B", "B", "Intermediario entre A e C, com menos atenuacao nos graves."),
            new("C", "C", "Curva quase plana, com leve atenuacao de graves profundos."),
            new("D", "D", "Curva usada historicamente para ruido de aeronaves."),
            new("468", "468", "Ponderacao ITU-R 468, enfatiza faixa mais sensivel para ruido percebido."),
        };
    }

    private void PopulateFrequencyScaleCombo()
    {
        FrequencyScaleCombo.ItemsSource = new List<ComboOption>
        {
            new(
                nameof(FrequencyScale.Logarithmic),
                "Logaritmica",
                "Distribui as barras de forma exponencial. Realca graves e organiza agudos com menos barras."),
            new(
                nameof(FrequencyScale.Mel),
                "Mel",
                "Escala perceptual baseada na audicao humana. Traz leitura mais natural nas medias frequencias."),
            new(
                nameof(FrequencyScale.Bark),
                "Bark",
                "Escala psicoacustica por bandas criticas. Destaca zonas de percepcao do ouvido."),
        };
    }

    private void PopulateFrequencyRangeCombos()
    {
        FrequencyMinCombo.ItemsSource = FrequencyMinOptions
            .Select(v => new ComboOption(FormatFrequencyId(v), FormatFrequencyLabel(v)))
            .ToList();

        FrequencyMaxCombo.ItemsSource = FrequencyMaxOptions
            .Select(v => new ComboOption(FormatFrequencyId(v), FormatFrequencyLabel(v)))
            .ToList();
    }

    private void UpdateFrequencyRangeCombos()
    {
        suppressFrequencyMinChanged = true;
        suppressFrequencyMaxChanged = true;
        SelectComboOption(FrequencyMinCombo, FormatFrequencyId(frequencyMinHz));
        SelectComboOption(FrequencyMaxCombo, FormatFrequencyId(frequencyMaxHz));
        suppressFrequencyMinChanged = false;
        suppressFrequencyMaxChanged = false;
    }

    private void UpdateFrequencyScaleToolTip()
    {
        if (FrequencyScaleCombo.SelectedItem is ComboOption option && !string.IsNullOrWhiteSpace(option.ToolTip))
        {
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(FrequencyScaleCombo, option.ToolTip);
            return;
        }

        Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(
            FrequencyScaleCombo,
            "Define como as barras sao distribuidas no espectro.");
    }

    private PresetDefinition ResolveActivePreset(string presetId)
    {
        if (!string.IsNullOrWhiteSpace(presetId) && presetsById.TryGetValue(presetId, out var preset))
        {
            return preset;
        }

        if (presetsById.TryGetValue(AudioMotionClonePresetId, out var clonePreset))
        {
            return clonePreset;
        }

        return presetsById.Values.First();
    }

    private static void SelectComboOption(ComboBox comboBox, string id)
    {
        if (comboBox.ItemsSource is not IEnumerable<ComboOption> options)
        {
            return;
        }

        var match = options.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            comboBox.SelectedItem = match;
        }
    }

    private void UpdateHubPreviewVisibility()
    {
        var visible = hubPreviewEnabled;
        HubPreviewPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        HubPreviewColumn.Width = visible ? new GridLength(340) : new GridLength(0);
    }

    private void ApplyBandCountBounds()
    {
        var cloneMode = IsClonePresetActive();
        var minBands = Math.Max(8, activePreset.DisplayBandCount);
        var maxBands = cloneMode ? minBands : 128;
        displayBandCount = Math.Clamp(displayBandCount <= 0 ? minBands : displayBandCount, minBands, maxBands);

        suppressBarCountChanged = true;
        BarCountSlider.Minimum = minBands;
        BarCountSlider.Maximum = maxBands;
        BarCountSlider.IsEnabled = !cloneMode;
        BarCountSlider.Value = displayBandCount;
        BarCountValueText.Text = displayBandCount.ToString();
        suppressBarCountChanged = false;
    }

    private void UpdateCloneModeUi()
    {
        var cloneMode = IsClonePresetActive();
        selectedRendererId = cloneMode ? RendererIds.AudioMotionClone : selectedRendererId;
        BarCountPanel.Visibility = cloneMode ? Visibility.Collapsed : Visibility.Visible;
        CloneBarsHintText.Visibility = cloneMode ? Visibility.Visible : Visibility.Collapsed;
        ApplyBandCountBounds();
    }

    private void UpdateFftSmoothingText()
    {
        FftSmoothingValueText.Text = fftSmoothing.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void UpdateSensitivityDbTexts()
    {
        SensitivityMinDbValueText.Text = $"{sensitivityMinDb:0} dB";
        SensitivityMaxDbValueText.Text = $"{sensitivityMaxDb:0} dB";
    }

    private void UpdateLinearBoostText()
    {
        LinearBoostValueText.Text = $"x{linearBoost:0.0}";
    }

    private void EnsureVisibleUiState()
    {
        fullscreen = false;
        ControlsPanel.Visibility = Visibility.Visible;
        FullscreenButton.Content = "Tela cheia";
    }

    private static bool IsClonePreset(PresetDefinition preset)
    {
        return string.Equals(preset.PresetId, AudioMotionClonePresetId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(preset.RendererId, RendererIds.AudioMotionClone, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsClonePresetActive() => IsClonePreset(activePreset);

    private float GetAnalyzerViewportWidth()
    {
        var width = (float)MainCanvas.ActualWidth;
        if (width > 1f)
        {
            var dpiScale = MathF.Max(1f, MainCanvas.Dpi / 96f);
            return width * dpiScale;
        }

        appWindow ??= GetAppWindow();
        var fallbackWidth = appWindow is null ? 1280f : Math.Max(640f, appWindow.Size.Width - 24f);
        var rasterScale = (float)(XamlRoot?.RasterizationScale ?? 1d);
        return fallbackWidth * MathF.Max(1f, rasterScale);
    }

    private static FrequencyScale CoerceFrequencyScale(FrequencyScale scale)
    {
        return Enum.IsDefined(typeof(FrequencyScale), scale)
            ? scale
            : FrequencyScale.Bark;
    }

    private static int CoerceFftSize(int value)
        => FftSizePolicy.CoerceUiSize(value);

    private static float CoerceFftSmoothing(float value) => Math.Clamp(value, 0f, 0.99f);

    private static float CoerceLinearBoost(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultLinearBoost;
        }

        return Math.Clamp(value, 1.0f, 3.0f);
    }

    private static float CoerceSensitivityMinDb(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultMinDecibels;
        }

        return Math.Clamp(value, -120f, -30f);
    }

    private static float CoerceSensitivityMaxDb(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultMaxDecibels;
        }

        return Math.Clamp(value, -60f, 0f);
    }

    private static float CoerceLegacySensitivityToMaxDb(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultMaxDecibels;
        }

        return CoerceSensitivityMaxDb(value);
    }

    private void EnsureSensitivityDbOrder()
    {
        if (sensitivityMaxDb >= sensitivityMinDb + 3f)
        {
            return;
        }

        sensitivityMaxDb = Math.Clamp(sensitivityMinDb + 3f, -60f, 0f);
        if (sensitivityMaxDb <= sensitivityMinDb)
        {
            sensitivityMinDb = Math.Clamp(sensitivityMaxDb - 3f, -120f, -30f);
        }
    }

    private static WeightingFilter CoerceWeightingFilter(WeightingFilter value)
    {
        return Enum.IsDefined(typeof(WeightingFilter), value)
            ? value
            : WeightingFilter.Off;
    }

    private static string ToWeightingFilterId(WeightingFilter value)
    {
        return value switch
        {
            WeightingFilter.A => "A",
            WeightingFilter.B => "B",
            WeightingFilter.C => "C",
            WeightingFilter.D => "D",
            WeightingFilter.Filter468 => "468",
            _ => "OFF",
        };
    }

    private static WeightingFilter ParseWeightingFilterId(string id)
    {
        return id.ToUpperInvariant() switch
        {
            "A" => WeightingFilter.A,
            "B" => WeightingFilter.B,
            "C" => WeightingFilter.C,
            "D" => WeightingFilter.D,
            "468" => WeightingFilter.Filter468,
            _ => WeightingFilter.Off,
        };
    }

    private static float CoerceFrequencyMin(float value) => Math.Clamp(value, 16f, 10_000f);

    private static float CoerceFrequencyMax(float value) => Math.Clamp(value, 250f, 20_000f);

    private void EnsureFrequencyRangeOrder()
    {
        if (frequencyMaxHz > frequencyMinHz)
        {
            frequencyMinHz = FindClosest(FrequencyMinOptions, frequencyMinHz);
            frequencyMaxHz = FindClosest(FrequencyMaxOptions, frequencyMaxHz);
            return;
        }

        var nextMax = FrequencyMaxOptions.FirstOrDefault(v => v > frequencyMinHz);
        if (nextMax > 0f)
        {
            frequencyMaxHz = nextMax;
        }
        else
        {
            frequencyMinHz = FrequencyMinOptions[^2];
            frequencyMaxHz = FrequencyMaxOptions[^1];
        }
    }

    private static float FindClosest(IReadOnlyList<float> options, float value)
    {
        var best = options[0];
        var bestDistance = Math.Abs(best - value);

        for (var i = 1; i < options.Count; i++)
        {
            var current = options[i];
            var distance = Math.Abs(current - value);
            if (distance < bestDistance)
            {
                best = current;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static string FormatFrequencyId(float value)
        => value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatFftSizeId(int value)
        => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatFftSizeLabel(int value)
        => value >= 1000
            ? $"{value / 1000}k"
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatFrequencyLabel(float value)
        => value >= 1000f
            ? $"{value / 1000f:0.#} kHz"
            : $"{value:0} Hz";

    private static bool TryParseFrequencyOption(string value, out float frequencyHz)
        => float.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out frequencyHz);

    private static AppSettings MigrateSettings(AppSettings settings)
    {
        var minDb = CoerceSensitivityMinDb(settings.SensitivityMinDb);
        var maxDb = CoerceSensitivityMaxDb(settings.SensitivityMaxDb);
        var legacyMaxDb = CoerceLegacySensitivityToMaxDb(settings.Sensitivity);

        var likelyLegacyOnlySensitivity = AreClose(settings.SensitivityMinDb, DefaultMinDecibels)
            && AreClose(settings.SensitivityMaxDb, DefaultMaxDecibels)
            && !AreClose(settings.Sensitivity, DefaultMaxDecibels);

        if (likelyLegacyOnlySensitivity)
        {
            maxDb = legacyMaxDb;
        }

        if (maxDb <= minDb + 3f)
        {
            maxDb = Math.Clamp(minDb + 3f, -60f, 0f);
            if (maxDb <= minDb)
            {
                minDb = Math.Clamp(maxDb - 3f, -120f, -30f);
            }
        }

        var activePresetId = string.IsNullOrWhiteSpace(settings.ActivePresetId)
            ? AudioMotionClonePresetId
            : settings.ActivePresetId;

        var minHz = CoerceFrequencyMin(settings.FrequencyMinHz);
        var maxHz = CoerceFrequencyMax(settings.FrequencyMaxHz);
        var linearBoost = CoerceLinearBoost(settings.LinearBoost);
        if (maxHz <= minHz)
        {
            minHz = 20f;
            maxHz = 1000f;
        }

        return new AppSettings
        {
            ActivePresetId = activePresetId,
            Hub75PreviewEnabled = settings.Hub75PreviewEnabled,
            Brightness = settings.Brightness,
            Sensitivity = maxDb,
            SensitivityMinDb = minDb,
            SensitivityMaxDb = maxDb,
            LinearBoost = linearBoost,
            BarCount = settings.BarCount <= 0 ? 38 : settings.BarCount,
            FrequencyScale = CoerceFrequencyScale(settings.FrequencyScale),
            FrequencyMinHz = minHz,
            FrequencyMaxHz = maxHz,
            FftSize = CoerceFftSize(settings.FftSize),
            FftSmoothing = CoerceFftSmoothing(settings.FftSmoothing),
            WeightingFilter = CoerceWeightingFilter(settings.WeightingFilter),
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
        };
    }

    private static bool AreClose(float a, float b) => MathF.Abs(a - b) < 0.001f;

    private void RestoreWindowSize()
    {
        if (appWindow is null || appSettings.WindowWidth < 640 || appSettings.WindowHeight < 480)
        {
            return;
        }

        appWindow.Resize(new SizeInt32(appSettings.WindowWidth, appSettings.WindowHeight));
    }

    private void SaveWindowSizeIntoSettings()
    {
        appWindow ??= GetAppWindow();
        if (appWindow is null)
        {
            return;
        }

        appSettings = CopySettings(
            WindowWidth: appWindow.Size.Width,
            WindowHeight: appWindow.Size.Height,
            Hub75PreviewEnabled: hubPreviewEnabled,
            ActivePresetId: currentPresetId,
            Sensitivity: sensitivityMaxDb,
            SensitivityMinDb: sensitivityMinDb,
            SensitivityMaxDb: sensitivityMaxDb,
            LinearBoost: linearBoost,
            BarCount: displayBandCount);
    }

    private void OnCaptureStatusChanged(object? sender, CaptureStatusChangedEventArgs e)
    {
        _ = DispatcherQueue.EnqueueAsync(() =>
        {
            StatusText.Text = e.Message;
        });
    }

    private AppSettings CopySettings(
        string? ActivePresetId = null,
        bool? Hub75PreviewEnabled = null,
        float? Brightness = null,
        float? Sensitivity = null,
        float? SensitivityMinDb = null,
        float? SensitivityMaxDb = null,
        float? LinearBoost = null,
        int? FftSize = null,
        float? FftSmoothing = null,
        WeightingFilter? WeightingFilter = null,
        FrequencyScale? FrequencyScale = null,
        float? FrequencyMinHz = null,
        float? FrequencyMaxHz = null,
        int? BarCount = null,
        int? WindowWidth = null,
        int? WindowHeight = null)
    {
        return new AppSettings
        {
            ActivePresetId = ActivePresetId ?? appSettings.ActivePresetId,
            Hub75PreviewEnabled = Hub75PreviewEnabled ?? appSettings.Hub75PreviewEnabled,
            Brightness = Brightness ?? appSettings.Brightness,
            Sensitivity = Sensitivity ?? appSettings.Sensitivity,
            SensitivityMinDb = SensitivityMinDb ?? appSettings.SensitivityMinDb,
            SensitivityMaxDb = SensitivityMaxDb ?? appSettings.SensitivityMaxDb,
            LinearBoost = LinearBoost ?? appSettings.LinearBoost,
            FftSize = FftSize ?? appSettings.FftSize,
            FftSmoothing = FftSmoothing ?? appSettings.FftSmoothing,
            WeightingFilter = WeightingFilter ?? appSettings.WeightingFilter,
            FrequencyScale = FrequencyScale ?? appSettings.FrequencyScale,
            FrequencyMinHz = FrequencyMinHz ?? appSettings.FrequencyMinHz,
            FrequencyMaxHz = FrequencyMaxHz ?? appSettings.FrequencyMaxHz,
            BarCount = BarCount ?? appSettings.BarCount,
            WindowWidth = WindowWidth ?? appSettings.WindowWidth,
            WindowHeight = WindowHeight ?? appSettings.WindowHeight,
        };
    }

    private static AppWindow? GetAppWindow()
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(id);
    }

    private sealed record ComboOption(string Id, string Label, string? ToolTip = null)
    {
        public override string ToString() => Label;
    }
}
