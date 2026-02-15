using System.Diagnostics;
using Analyzer.Dsp.Analysis;
using App.WinUI.Services;
using App.WinUI.ViewModels;
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
using Device.Server.Hosting;
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
    private const int DefaultBarCount = 38;
    private const int DefaultFftSize = 2048;
    private const float DefaultFftSmoothing = 0.75f;
    private const WeightingFilter DefaultWeightingFilter = WeightingFilter.B;
    private const FrequencyScale DefaultFrequencyScale = FrequencyScale.Bark;
    private const float DefaultFrequencyMinHz = 20f;
    private const float DefaultFrequencyMaxHz = 1000f;
    private const double HubPreviewHeightRatio = 0.20;
    private const double HubPreviewMinHeight = 84d;
    private const double HubPreviewMaxHeight = 220d;
    private const double FullscreenButtonAutoHideDelayMs = 1400d;

    private readonly VisualizerEngine visualizer = new();
    private readonly ILoopbackCapture capture = new WasapiLoopbackCaptureService();
    private readonly SimulatorLedOutput simulatorLedOutput = new();
    private readonly NullLedOutput nullLedOutput = new();
    private readonly MatrixPortalLedOutput matrixPortalLedOutput;

    private readonly PresetRepository presetRepository;
    private readonly SettingsRepository settingsRepository;
    private readonly AppSettingsDomainService settingsDomainService = new();
    private readonly MainPageViewModel viewModel = new();
    private readonly AudioPipelineCoordinator pipelineCoordinator;
    private readonly Dictionary<string, PresetDefinition> presetsById = new(StringComparer.OrdinalIgnoreCase);

    private IAnalyzer analyzer = new SpectrumAnalyzer(new AnalyzerConfig());

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? renderTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? cloneViewportDebounceTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? fullscreenButtonHideTimer;
    private AppWindow? appWindow;

    private AppSettings appSettings = new();
    private PresetDefinition activePreset = new();
    private string selectedRendererId = RendererIds.AudioMotionClone;
    private string currentPresetId = AudioMotionClonePresetId;

    private long lastRenderQpc;
    private float lastCloneViewportWidth;
    private bool initialized;
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
        var serverHost = App.DeviceIntegration?.Host ?? new DeviceServerHost();
        matrixPortalLedOutput = new MatrixPortalLedOutput(serverHost);
        pipelineCoordinator = new AudioPipelineCoordinator(capture, simulatorLedOutput, matrixPortalLedOutput, nullLedOutput, () => Volatile.Read(ref analyzer));

        capture.StatusChanged += OnCaptureStatusChanged;
        pipelineCoordinator.StatusChanged += OnPipelineCoordinatorStatusChanged;
        DataContext = viewModel;
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
        await pipelineCoordinator.StopAsync();
        capture.StatusChanged -= OnCaptureStatusChanged;
        renderTimer?.Stop();
        cloneViewportDebounceTimer?.Stop();
        fullscreenButtonHideTimer?.Stop();

        SaveWindowSizeIntoSettings();
        await settingsRepository.SaveAsync(appSettings);
        await capture.DisposeAsync();
    }

    private async Task InitializeAsync()
    {
        appSettings = await settingsRepository.LoadAsync().ConfigureAwait(false);
        appSettings = settingsDomainService.Migrate(appSettings);
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

            viewModel.CurrentPresetId = currentPresetId;
            viewModel.SelectedRendererId = selectedRendererId;
            viewModel.SensitivityMinDb = sensitivityMinDb;
            viewModel.SensitivityMaxDb = sensitivityMaxDb;
            viewModel.LinearBoost = linearBoost;
            viewModel.BarCount = displayBandCount;
            viewModel.FftSize = fftSize;
            viewModel.FftSmoothing = fftSmoothing;
            viewModel.WeightingFilter = weightingFilter;
            viewModel.FrequencyScale = frequencyScale;
            viewModel.FrequencyMinHz = frequencyMinHz;
            viewModel.FrequencyMaxHz = frequencyMaxHz;

            hubPreviewEnabled = appSettings.Hub75PreviewEnabled;
            Hub75Toggle.IsOn = hubPreviewEnabled;
            UpdateHubPreviewVisibility();

            lastCloneViewportWidth = GetAnalyzerViewportWidth();
            Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));
            appSettings = settingsDomainService.Copy(appSettings, b => { b.SetActivePresetId(currentPresetId); b.SetSensitivity(sensitivityMinDb, sensitivityMaxDb); b.SetLinearBoost(linearBoost); b.SetFftSize(fftSize); b.SetFftSmoothing(fftSmoothing); b.SetWeightingFilter(weightingFilter); b.SetFrequencyScale(frequencyScale); b.SetFrequencyRange(frequencyMinHz, frequencyMaxHz); b.SetBarCount(displayBandCount); });
            StatusText.Text = "Pronto";
        });

        await DispatcherQueue.EnqueueAsync(() =>
        {
            SetupRenderTimer();
        });

        await pipelineCoordinator.StartAsync(hubPreviewEnabled, appSettings.Brightness, currentPresetId).ConfigureAwait(false);
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
        EnsureFullscreenButtonHideTimer();
    }
    private void EnsureFullscreenButtonHideTimer()
    {
        if (fullscreenButtonHideTimer is not null)
        {
            return;
        }

        fullscreenButtonHideTimer = DispatcherQueue.CreateTimer();
        fullscreenButtonHideTimer.Interval = TimeSpan.FromMilliseconds(FullscreenButtonAutoHideDelayMs);
        fullscreenButtonHideTimer.Tick += (_, _) =>
        {
            fullscreenButtonHideTimer?.Stop();
            if (fullscreen)
            {
                HideFullscreenButtonOverlay();
            }
        };
    }

    private void ShowFullscreenButtonOverlay(bool restartAutoHide)
    {
        CanvasFullscreenButton.Visibility = Visibility.Visible;
        CanvasFullscreenButton.IsHitTestVisible = true;

        if (!fullscreen)
        {
            fullscreenButtonHideTimer?.Stop();
            return;
        }

        if (!restartAutoHide)
        {
            return;
        }

        EnsureFullscreenButtonHideTimer();
        fullscreenButtonHideTimer!.Stop();
        fullscreenButtonHideTimer.Interval = TimeSpan.FromMilliseconds(FullscreenButtonAutoHideDelayMs);
        fullscreenButtonHideTimer.Start();
    }

    private void HideFullscreenButtonOverlay()
    {
        CanvasFullscreenButton.Visibility = Visibility.Collapsed;
        CanvasFullscreenButton.IsHitTestVisible = false;
        fullscreenButtonHideTimer?.Stop();
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

    private void OnMainCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var frame = pipelineCoordinator.LatestFrame;
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
    private void OnMainCanvasHostPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ShowFullscreenButtonOverlay(restartAutoHide: true);
    }

    private void OnMainCanvasHostPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ShowFullscreenButtonOverlay(restartAutoHide: true);
    }

    private void OnMainCanvasHostPointerExited(object sender, PointerRoutedEventArgs e)
    {
        HideFullscreenButtonOverlay();
    }
    private void OnVisualizerLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateHubPreviewVisibility();
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
        pipelineCoordinator.SetCurrentPreset(currentPresetId);
        selectedRendererId = preset.RendererId;

        SelectComboOption(RendererCombo, selectedRendererId);
        UpdateCloneModeUi();
        lastCloneViewportWidth = GetAnalyzerViewportWidth();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));

        appSettings = settingsDomainService.Copy(appSettings, b => b.SetActivePresetId(currentPresetId));
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

        pipelineCoordinator.SetHubPreview(hubPreviewEnabled, appSettings.Brightness);

        appSettings = settingsDomainService.Copy(appSettings, b => b.SetHub75PreviewEnabled(hubPreviewEnabled));
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

    
    private void OnResetSettingsClicked(object sender, RoutedEventArgs e)
    {
        sensitivityMinDb = DefaultMinDecibels;
        sensitivityMaxDb = DefaultMaxDecibels;
        linearBoost = DefaultLinearBoost;
        displayBandCount = DefaultBarCount;
        fftSize = DefaultFftSize;
        fftSmoothing = DefaultFftSmoothing;
        weightingFilter = DefaultWeightingFilter;
        frequencyScale = DefaultFrequencyScale;
        frequencyMinHz = DefaultFrequencyMinHz;
        frequencyMaxHz = DefaultFrequencyMaxHz;

        EnsureSensitivityDbOrder();
        EnsureFrequencyRangeOrder();

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

        UpdateFrequencyRangeCombos();
        UpdateFrequencyScaleToolTip();
        UpdateSensitivityDbTexts();
        UpdateLinearBoostText();
        UpdateFftSmoothingText();
        ApplyBandCountBounds();

        viewModel.SensitivityMinDb = sensitivityMinDb;
        viewModel.SensitivityMaxDb = sensitivityMaxDb;
        viewModel.LinearBoost = linearBoost;
        viewModel.BarCount = displayBandCount;
        viewModel.FftSize = fftSize;
        viewModel.FftSmoothing = fftSmoothing;
        viewModel.WeightingFilter = weightingFilter;
        viewModel.FrequencyScale = frequencyScale;
        viewModel.FrequencyMinHz = frequencyMinHz;
        viewModel.FrequencyMaxHz = frequencyMaxHz;

        lastCloneViewportWidth = GetAnalyzerViewportWidth();
        Volatile.Write(ref analyzer, CreateAnalyzer(BuildRuntimePreset()));

        appSettings = settingsDomainService.Copy(appSettings, b =>
        {
            b.SetSensitivity(sensitivityMinDb, sensitivityMaxDb);
            b.SetLinearBoost(linearBoost);
            b.SetBarCount(displayBandCount);
            b.SetFftSize(fftSize);
            b.SetFftSmoothing(fftSmoothing);
            b.SetWeightingFilter(weightingFilter);
            b.SetFrequencyScale(frequencyScale);
            b.SetFrequencyRange(frequencyMinHz, frequencyMaxHz);
        });

        StatusText.Text = "Configuracoes restauradas";
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetSensitivity(sensitivityMinDb, sensitivityMaxDb));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetSensitivity(sensitivityMinDb, sensitivityMaxDb));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetLinearBoost(linearBoost));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetBarCount(displayBandCount));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetFftSize(fftSize));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetFftSmoothing(fftSmoothing));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetWeightingFilter(weightingFilter));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetFrequencyScale(frequencyScale));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetFrequencyRange(frequencyMinHz, frequencyMaxHz));
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
        appSettings = settingsDomainService.Copy(appSettings, b => b.SetFrequencyRange(frequencyMinHz, frequencyMaxHz));
    }

    private void OnFullscreenClicked(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen(!fullscreen);
    }

    
    private void OnMainCanvasDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ToggleFullscreen(!fullscreen);
        e.Handled = true;
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
        VisualizerLayout.Margin = fullscreen ? new Thickness(0) : new Thickness(12, 0, 12, 12);
        MainCanvasBorder.CornerRadius = fullscreen ? new CornerRadius(0) : new CornerRadius(12);
        HubPreviewPanel.CornerRadius = fullscreen ? new CornerRadius(0) : new CornerRadius(12);
        if (fullscreen)
        {
            SettingsSplitView.IsPaneOpen = false;
            ShowFullscreenButtonOverlay(restartAutoHide: true);
        }
        else
        {
            HideFullscreenButtonOverlay();
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
        var options = presetsById.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new ComboOption(p.PresetId, p.Name))
            .ToList();

        PresetCombo.ItemsSource = options;
        PresetCombo.IsEnabled = options.Count > 1;
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

        if (!visible)
        {
            HubPreviewRow.Height = new GridLength(0);
            return;
        }

        var availableHeight = VisualizerLayout.ActualHeight;
        if (!fullscreen)
        {
            availableHeight -= ControlsPanel.ActualHeight + ControlsPanel.Margin.Top + ControlsPanel.Margin.Bottom;
        }

        if (availableHeight <= 0d)
        {
            availableHeight = appWindow?.Size.Height ?? 720d;
        }

        var targetHeight = availableHeight * HubPreviewHeightRatio;
        targetHeight = Math.Clamp(targetHeight, HubPreviewMinHeight, HubPreviewMaxHeight);
        targetHeight = Math.Min(targetHeight, Math.Max(72d, availableHeight * 0.45d));

        HubPreviewRow.Height = new GridLength(targetHeight);
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
        HideFullscreenButtonOverlay();
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

        appSettings = settingsDomainService.Copy(appSettings, b =>
        {
            b.SetWindowSize(appWindow.Size.Width, appWindow.Size.Height);
            b.SetHub75PreviewEnabled(hubPreviewEnabled);
            b.SetActivePresetId(currentPresetId);
            b.SetSensitivity(sensitivityMinDb, sensitivityMaxDb);
            b.SetLinearBoost(linearBoost);
            b.SetBarCount(displayBandCount);
        });
    }

    private void OnCaptureStatusChanged(object? sender, CaptureStatusChangedEventArgs e)
    {
        _ = DispatcherQueue.EnqueueAsync(() =>
        {
            StatusText.Text = e.Message;
        });
    }

    private void OnPipelineCoordinatorStatusChanged(object? sender, string message)
    {
        _ = DispatcherQueue.EnqueueAsync(() =>
        {
            StatusText.Text = message;
        });
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




