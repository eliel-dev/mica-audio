using System.Diagnostics;
using Analyzer.Dsp.Analysis;
using App.WinUI.Services;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Gif;
using App.WinUI.Services.Panels;
using App.WinUI.Services.Visualizer;
using App.WinUI.ViewModels;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Output.Led;
using Visual.Win2D.Engine;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;
namespace App.WinUI.Views;// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao

public partial class MainPage : Page
{
    private const string AudioMotionClonePresetId = "audiomotion-clone";
    private const float DefaultMinDecibels = -85f;
    private const float DefaultMaxDecibels = -25f;
    private const float DefaultLinearBoost = 1.3f;
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
    private const int GifTargetFps = 12;
    private const int GifMaxDownloadBytes = 25 * 1024 * 1024;

    private readonly VisualizerEngine visualizer = new();
    private readonly ILoopbackCapture capture;
    private readonly SimulatorLedOutput simulatorLedOutput;
    private readonly NullLedOutput nullLedOutput;
    private readonly Esp32S3LedOutput esp32s3LedOutput;
    private readonly Hub75VisualizerSessionService hub75VisualizerSessionService;
    private readonly PanelsPlaybackService panelsPlaybackService;
    private readonly Hub75GifDecoder gifDecoder = new(Hub75GifDecoder.DefaultMaxGifFrames);
    private readonly Hub75FrameFormatter gifFrameFormatter = new();
    private readonly Hub75GifPlayer gifPlayer = new(TimeSpan.FromMilliseconds(1000d / GifTargetFps));
    private readonly PresetRepository presetRepository;
    private readonly SettingsRepository settingsRepository;
    private readonly AppSettingsDomainService settingsDomainService;
    private readonly MainPageViewModel viewModel;
    private readonly AudioPipelineCoordinator pipelineCoordinator;
    private readonly Dictionary<string, PresetDefinition> presetsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PresetDefinition> presetNavigationOrder = [];

    private int currentPresetIndex = -1;
    private DispatcherQueueTimer? presetSwitchHudHideTimer;
    private IAnalyzer analyzer = new SpectrumAnalyzer(new AnalyzerConfig());
    private DispatcherQueueTimer? renderTimer;
    private DispatcherQueueTimer? cloneViewportDebounceTimer;
    private DispatcherQueueTimer? fullscreenButtonHideTimer;
    private AppWindow? appWindow;
    private AppSettings appSettings = new();
    private PresetDefinition activePreset = new();
    private string selectedRendererId = RendererIds.AudioMotionClone;
    private string currentPresetId = AudioMotionClonePresetId;
    private GifContentSourceMode contentSourceMode = GifContentSourceMode.Audio;
    private GifScaleMode gifScaleMode = GifScaleMode.Fit;
    private IReadOnlyList<DecodedGifFrame> decodedGifFrames = Array.Empty<DecodedGifFrame>();
    private IReadOnlyList<RgbaColor[]> gifFrames = Array.Empty<RgbaColor[]>();
    private RgbaColor[]? lastGifFrame;
    private CancellationTokenSource? gifLoadCts;
    private long lastRenderQpc;
    private float lastCloneViewportWidth;
    private bool initialized;
    private bool hub75ModeEnabled;
    private bool isPageVisible;
    private bool fullscreen;
    private bool suppressBarCountChanged;
    private bool suppressFftSmoothingChanged;
    private bool suppressWeightingFilterChanged;
    private bool suppressFrequencyScaleChanged;
    private bool suppressFrequencyMinChanged;
    private bool suppressFrequencyMaxChanged;
    private bool suppressContentModeChanged;
    private bool suppressGifScaleModeChanged;
    private bool gifLoading;
    private float linearBoost = DefaultLinearBoost;
    private int displayBandCount = 38;
    private int fftSize = DefaultFftSize;
    private float fftSmoothing = 0.8f;
    private WeightingFilter weightingFilter = WeightingFilter.Off;
    private FrequencyScale frequencyScale = FrequencyScale.Logarithmic;
    private float frequencyMinHz = 30f;
    private float frequencyMaxHz = 16_000f;

    private static readonly float[] FrequencyMinOptions =
    [
        16f, 20f, 30f, 40f, 60f, 80f, 100f, 160f, 200f, 250f, 315f, 400f, 500f, 630f, 800f, 1000f,
    ];

    private static readonly float[] FrequencyMaxOptions =
    [
        250f, 315f, 400f, 500f, 630f, 800f, 1000f, 1250f, 1600f, 2000f, 2500f, 3150f, 4000f, 5000f, 6300f, 8000f,
        10_000f, 12_000f, 16_000f, 20_000f,
    ];

    private static readonly RgbaColor[] BlackHubFrame =
        Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();
    private static readonly float[] BlackHubBins = new float[LedDefaults.MatrixWidth];

    internal MainPage(
        MainPageViewModel viewModel,
        PresetRepository presetRepository,
        SettingsRepository settingsRepository,
        AppSettingsDomainService settingsDomainService,
        ILoopbackCapture capture,
        SimulatorLedOutput simulatorLedOutput,
        NullLedOutput nullLedOutput,
        Esp32S3LedOutput esp32s3LedOutput,
        Hub75VisualizerSessionService hub75VisualizerSessionService,
        PanelsPlaybackService panelsPlaybackService)
    {
        this.viewModel = viewModel;
        this.presetRepository = presetRepository;
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;
        this.capture = capture;
        this.simulatorLedOutput = simulatorLedOutput;
        this.nullLedOutput = nullLedOutput;
        this.esp32s3LedOutput = esp32s3LedOutput;
        this.hub75VisualizerSessionService = hub75VisualizerSessionService;
        this.panelsPlaybackService = panelsPlaybackService;

        pipelineCoordinator = new AudioPipelineCoordinator(
            capture,
            simulatorLedOutput,
            esp32s3LedOutput,
            nullLedOutput,
            Volatile.Read(ref analyzer));

        using (uiBootstrapGuard.Enter())
        {
            InitializeComponent();
            ConfigureSliderDefaults();
        }

        gifPlayer.FrameReady += OnGifFrameReady;
        capture.StatusChanged += OnCaptureStatusChanged;
        pipelineCoordinator.StatusChanged += OnPipelineCoordinatorStatusChanged;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    private void ConfigureSliderDefaults() { FftSmoothingSlider.Minimum = 0d; FftSmoothingSlider.Maximum = 0.99d; FftSmoothingSlider.SmallChange = 0.01d; FftSmoothingSlider.StepFrequency = 0.01d; }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        isPageVisible = true;

        if (!initialized)
        {
            try
            {
                await InitializeAsync().ConfigureAwait(true);
                initialized = true;
            }
            catch (Exception ex)
            {
                App.ReportStartupFailure("MainPage.OnLoaded failed", ex);
                StatusText.Text = "Falha ao iniciar o visualizador.";
            }

            return;
        }

        ConfigureHubOutputsForCurrentState();
        UpdateRenderLoopState();
        RefreshVisibleCanvases();

        if (contentSourceMode == GifContentSourceMode.Gif)
        {
            PushCurrentHubFrameIfNeeded();
            await SyncHub75DeviceSessionAsync().ConfigureAwait(true);
            return;
        }

        await ActivateVisualizerSessionAsync().ConfigureAwait(true);
        await SyncHub75DeviceSessionAsync().ConfigureAwait(true);
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        isPageVisible = false;
        App.SetShellChromeHidden(false);
        cloneViewportDebounceTimer?.Stop();
        StopVisualizerRuntimeDebounceTimer();
        fullscreenButtonHideTimer?.Stop();
        gifLoadCts?.Cancel();
        gifLoadCts?.Dispose();
        gifLoadCts = null;

        ConfigureHubOutputsForCurrentState();
        UpdateRenderLoopState();

        if (!hub75ModeEnabled)
        {
            gifPlayer.Stop();
            await pipelineCoordinator.StopAsync().ConfigureAwait(true);
        }

        SaveWindowSizeIntoSettings();
        await settingsRepository.SaveAsync(appSettings);
    }
    private async Task InitializeAsync() { App.RecordStartupBreadcrumb("MainPage.InitializeAsync"); appSettings = await settingsRepository.LoadAsync().ConfigureAwait(false); appSettings = settingsDomainService.Migrate(appSettings); var presets = await presetRepository.LoadOrSeedAsync().ConfigureAwait(false); var shouldPersistNormalizedState = false; presetsById.Clear(); foreach (var preset in presets) { if (preset is null || string.IsNullOrWhiteSpace(preset.PresetId)) { continue; } presetsById[preset.PresetId] = preset; } await DispatcherQueue.EnqueueAsync(() => { ApplyLoadedStateToUi(out shouldPersistNormalizedState); SyncHubTransportMode(); PersistCurrentVisualizerSettings(includePreview: true); StatusText.Text = "Pronto"; }); if (shouldPersistNormalizedState) { await settingsRepository.SaveAsync(appSettings).ConfigureAwait(false); } await ActivateVisualizerSessionAsync().ConfigureAwait(false); await SyncHub75DeviceSessionAsync().ConfigureAwait(false); }
    private async Task ActivateVisualizerSessionAsync()
    {
        App.RecordStartupBreadcrumb("MainPage.ActivateVisualizerSessionAsync");
        SyncHubTransportMode();

        try
        {
            await pipelineCoordinator.StartAsync(
                    enableSimulator: ShouldShowHubPreviewPanel(),
                    enableHub75DeviceOutput: ShouldEnableHub75DeviceOutput(),
                    brightness: appSettings.Brightness,
                    presetId: currentPresetId,
                    rendererId: ResolveCurrentRendererId())
                .ConfigureAwait(false);

            await DispatcherQueue.EnqueueAsync(() =>
            {
                UpdateRenderLoopState();
                RefreshVisibleCanvases();
            });
        }
        catch (Exception ex)
        {
            App.ReportStartupFailure("MainPage.ActivateVisualizerSessionAsync failed", ex);
            await DispatcherQueue.EnqueueAsync(() =>
            {
                StatusText.Text = "Falha ao iniciar a captura do visualizador.";
            });
        }
    }

    private void EnsureRenderTimerStarted()
    {
        if (renderTimer is null)
        {
            renderTimer = DispatcherQueue.CreateTimer();
            renderTimer.Interval = TimeSpan.FromMilliseconds(1000d / 60d);
            renderTimer.Tick += (_, _) =>
            {
                PumpHubFrameOutput();

                if (!isPageVisible)
                {
                    return;
                }

                MainCanvas.Invalidate();
                if (ShouldShowHubPreviewPanel())
                {
                    InvalidateHubPreviews();
                }
            };
        }

        if (!renderTimer.IsRunning)
        {
            renderTimer.Start();
        }

        EnsureCloneViewportDebounceTimer();
        EnsureFullscreenButtonHideTimer();
    }
    private void EnsureFullscreenButtonHideTimer() { if (fullscreenButtonHideTimer is not null) { return; } fullscreenButtonHideTimer = DispatcherQueue.CreateTimer(); fullscreenButtonHideTimer.Interval = TimeSpan.FromMilliseconds(FullscreenButtonAutoHideDelayMs); fullscreenButtonHideTimer.Tick += (_, _) => { fullscreenButtonHideTimer?.Stop(); if (fullscreen) { HideFullscreenButtonOverlay(); } }; }
    private void ShowFullscreenButtonOverlay(bool restartAutoHide) { CanvasFullscreenButton.Visibility = Visibility.Visible; CanvasFullscreenButton.IsHitTestVisible = true; if (!fullscreen) { fullscreenButtonHideTimer?.Stop(); return; } if (!restartAutoHide) { return; } EnsureFullscreenButtonHideTimer(); fullscreenButtonHideTimer!.Stop(); fullscreenButtonHideTimer.Interval = TimeSpan.FromMilliseconds(FullscreenButtonAutoHideDelayMs); fullscreenButtonHideTimer.Start(); }
    private void HideFullscreenButtonOverlay() { CanvasFullscreenButton.Visibility = Visibility.Collapsed; CanvasFullscreenButton.IsHitTestVisible = false; fullscreenButtonHideTimer?.Stop(); }
    private void ScheduleCloneViewportAnalyzerRebuild() { if (!initialized || !IsCloneDisplayModeActive()) { return; } EnsureCloneViewportDebounceTimer(); var timer = cloneViewportDebounceTimer!; timer.Stop(); timer.Interval = TimeSpan.FromMilliseconds(80); timer.Start(); }
    private void EnsureCloneViewportDebounceTimer() { if (cloneViewportDebounceTimer is not null) { return; } cloneViewportDebounceTimer = DispatcherQueue.CreateTimer(); cloneViewportDebounceTimer.Interval = TimeSpan.FromMilliseconds(80); cloneViewportDebounceTimer.Tick += (_, _) => { cloneViewportDebounceTimer?.Stop(); if (!IsCloneDisplayModeActive()) { return; } _ = ApplyPendingVisualizerRuntime(forceRebuild: true, persistSettings: false, refreshOutputs: false, updateStatusOnFailure: true); }; }
    private void OnMainCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args) { var now = Stopwatch.GetTimestamp(); var deltaSeconds = lastRenderQpc == 0 ? 1f / 60f : (float)(now - lastRenderQpc) / Stopwatch.Frequency; lastRenderQpc = now; if (contentSourceMode == GifContentSourceMode.Gif) { var gifFrame = lastGifFrame; if (gifFrame is null || gifFrame.Length == 0) { args.DrawingSession.Clear(Color.FromArgb(255, 0, 0, 0)); TrackRenderDiagnostics(deltaSeconds); return; } DrawHubFrame(args.DrawingSession, (float)sender.ActualWidth, (float)sender.ActualHeight, gifFrame, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight); TrackRenderDiagnostics(deltaSeconds); return; } var frame = pipelineCoordinator.LatestFrame; if (frame is null) { args.DrawingSession.Clear(Color.FromArgb(255, 0, 0, 0)); TrackRenderDiagnostics(deltaSeconds); return; } var preset = BuildRuntimePreset(GetRenderRuntimeSettings()); visualizer.Render(args.DrawingSession, (float)sender.ActualWidth, (float)sender.ActualHeight, frame, preset, deltaSeconds); TrackRenderDiagnostics(deltaSeconds); }
    private void OnMainCanvasSizeChanged(object sender, SizeChangedEventArgs e) { if (!initialized || !IsCloneDisplayModeActive()) { return; } var width = (float)e.NewSize.Width; if (width <= 1f || MathF.Abs(width - lastCloneViewportWidth) < 1f) { return; } lastCloneViewportWidth = width; ScheduleCloneViewportAnalyzerRebuild(); }
    private void OnMainCanvasHostPointerEntered(object sender, PointerRoutedEventArgs e) { ShowFullscreenButtonOverlay(restartAutoHide: true); }
    private void OnMainCanvasHostPointerMoved(object sender, PointerRoutedEventArgs e) { ShowFullscreenButtonOverlay(restartAutoHide: true); }
    private void OnMainCanvasHostPointerExited(object sender, PointerRoutedEventArgs e) { HideFullscreenButtonOverlay(); }
    private void OnVisualizerLayoutSizeChanged(object sender, SizeChangedEventArgs e) { UpdateHubPreviewVisibility(); }
    private void OnHubCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!ShouldShowHubPreviewPanel())
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

        DrawHubFrame(
            args.DrawingSession,
            (float)sender.ActualWidth,
            (float)sender.ActualHeight,
            snapshot,
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight);
    }
    private static void DrawHubFrame(CanvasDrawingSession drawingSession, float width, float height, RgbaColor[] pixels, int matrixWidth, int matrixHeight)
    {
        var matrixAspect = (float)matrixWidth / matrixHeight;
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
        var drawCellW = Math.Max(1f, cellW);
        var drawCellH = Math.Max(1f, cellH);

        drawingSession.Clear(Color.FromArgb(255, 8, 10, 14));

        var requiredPixels = matrixWidth * matrixHeight;
        if (pixels.Length < requiredPixels)
        {
            return;
        }

        for (var y = 0; y < matrixHeight; y++)
        {
            var rowStart = y * matrixWidth;
            var drawY = offsetY + (y * cellH);

            var x = 0;
            while (x < matrixWidth)
            {
                var pixel = pixels[rowStart + x];
                if (pixel.A == 0)
                {
                    x++;
                    continue;
                }

                var runStart = x;
                x++;

                while (x < matrixWidth)
                {
                    var candidate = pixels[rowStart + x];
                    if (candidate.A != pixel.A || candidate.R != pixel.R || candidate.G != pixel.G || candidate.B != pixel.B)
                    {
                        break;
                    }

                    x++;
                }

                var runLength = x - runStart;
                var color = Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B);
                drawingSession.FillRectangle(
                    offsetX + (runStart * cellW),
                    drawY,
                    drawCellW * runLength,
                    drawCellH,
                    color);
            }
        }
    }
    private void InvalidateHubPreviews() { HubCanvas.Invalidate(); }
    private void OnHub75Toggled(object sender, RoutedEventArgs e)
    {
        if (ShouldIgnoreUiMutation())
        {
            return;
        }

        var enableHub75Mode = Hub75Toggle.IsOn;
        if (!enableHub75Mode && !panelsPlaybackService.HasSuspendedPanel && ShouldEnableHub75DeviceOutput())
        {
            pipelineCoordinator.ConfigureHubOutputs(
                enableSimulator: false,
                enableHub75DeviceOutput: true,
                brightness: appSettings.Brightness);
            pipelineCoordinator.SendHubBins(BlackHubBins);
        }

        hub75ModeEnabled = enableHub75Mode;
        UpdateGifControlsVisibility();
        ConfigureHubOutputsForCurrentState();
        PushCurrentHubFrameIfNeeded(forceAudioFrame: true);
        RefreshVisibleCanvases();
        PersistCurrentVisualizerSettings(includePreview: true);
        _ = SyncHub75DeviceSessionAsync();
    }
    private async void OnGifScaleModeSelectionChanged(object sender, SelectionChangedEventArgs e) { if (suppressGifScaleModeChanged) { return; } if (GifScaleModeCombo.SelectedItem is not ComboOption option || !Enum.TryParse<GifScaleMode>(option.Id, ignoreCase: true, out var mode)) { return; } gifScaleMode = mode; await ReformatLoadedGifFramesAsync().ConfigureAwait(true); }
    private async void OnGifOpenFileClicked(object sender, RoutedEventArgs e) { var file = await PickGifFileAsync().ConfigureAwait(true); if (file is null) { return; } await LoadGifAsync($"Arquivo: {file.Name}", async cancellationToken => { var fileInfo = new FileInfo(file.Path); if (fileInfo.Length > GifMaxDownloadBytes) { throw new InvalidDataException("Arquivo acima de 25MB."); } return await File.ReadAllBytesAsync(file.Path, cancellationToken).ConfigureAwait(false); }).ConfigureAwait(true); }
    private async void OnGifPlayClicked(object sender, RoutedEventArgs e) { if (gifFrames.Count == 0) { StatusText.Text = "Carregue um GIF para iniciar."; return; } if (contentSourceMode != GifContentSourceMode.Gif) { await SwitchContentModeAsync(GifContentSourceMode.Gif).ConfigureAwait(true); } if (gifPlayer.Play()) { StatusText.Text = "GIF em reproducao (12 FPS)."; UpdateGifTransportState(); } }
    private void OnGifPauseClicked(object sender, RoutedEventArgs e) { gifPlayer.Pause(); StatusText.Text = "GIF pausado."; UpdateGifTransportState(); }
    private void OnGifStopClicked(object sender, RoutedEventArgs e)
    {
        gifPlayer.Stop();
        if (gifFrames.Count > 0)
        {
            lastGifFrame = gifFrames[0];
            PushCurrentHubFrameIfNeeded();
            RefreshVisibleCanvases();
        }

        StatusText.Text = "GIF parado.";
        UpdateGifTransportState();
    }
    private void OnFullscreenClicked(object sender, RoutedEventArgs e) { ToggleFullscreen(!fullscreen); }
    private void OnMainCanvasDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) { ToggleFullscreen(!fullscreen); e.Handled = true; }
    private void OnFullscreenAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { if (sender.Key == Windows.System.VirtualKey.F11) { ToggleFullscreen(!fullscreen); args.Handled = true; return; } if (sender.Key == Windows.System.VirtualKey.Escape && fullscreen) { ToggleFullscreen(false); args.Handled = true; } }
    private void OnPreviousPresetAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { TryNavigatePresetFromKeyboard(-1, args); }
    private void OnNextPresetAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { TryNavigatePresetFromKeyboard(1, args); }
    private void TryNavigatePresetFromKeyboard(int direction, KeyboardAcceleratorInvokedEventArgs args) { if (!CanHandlePresetNavigationKeyboard()) { return; } NavigatePreset(direction); args.Handled = true; }
    private bool CanHandlePresetNavigationKeyboard() { if (SettingsSplitView.IsPaneOpen || presetNavigationOrder.Count == 0) { return false; } var focusedElement = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; return !IsPresetNavigationBlockedByFocus(focusedElement); }
    private bool IsPresetNavigationBlockedByFocus(DependencyObject? element) { if (element is null) { return false; } if (element is TextBox or ComboBox or Slider or ToggleSwitch or Button) { return true; } return IsInsideElementTree(element, SettingsSplitView.Pane as DependencyObject); }
    private static bool IsInsideElementTree(DependencyObject? element, DependencyObject? root) { if (element is null || root is null) { return false; } var current = element; while (current is not null) { if (ReferenceEquals(current, root)) { return true; } current = VisualTreeHelper.GetParent(current); } return false; }
    private void ToggleFullscreen(bool enable) { appWindow ??= GetAppWindow(); if (appWindow is null) { return; } fullscreen = enable; if (fullscreen) { appWindow.SetPresenter(AppWindowPresenterKind.FullScreen); } else { appWindow.SetPresenter(AppWindowPresenterKind.Overlapped); } UpdateFullscreenUiState(); }
    private void UpdateFullscreenUiState() { App.SetShellChromeHidden(fullscreen); ControlsPanel.Visibility = fullscreen ? Visibility.Collapsed : Visibility.Visible; SettingsButton.Visibility = fullscreen ? Visibility.Collapsed : Visibility.Visible; VisualizerLayout.Margin = fullscreen ? new Thickness(0) : new Thickness(12, 0, 12, 12); MainCanvasBorder.CornerRadius = fullscreen ? new CornerRadius(0) : new CornerRadius(12); HubPreviewPanel.CornerRadius = fullscreen ? new CornerRadius(0) : new CornerRadius(12); if (fullscreen) { SettingsSplitView.IsPaneOpen = false; SyncSettingsButtonState(); ShowFullscreenButtonOverlay(restartAutoHide: true); } else { HideFullscreenButtonOverlay(); } UpdateHubPreviewVisibility(); }
    private PresetDefinition BuildRuntimePreset() { return BuildRuntimePreset(GetRenderRuntimeSettings()); }
    private void BuildPresetNavigationOrder() { presetNavigationOrder.Clear(); foreach (var preset in PresetNavigationHelper.BuildOrder(presetsById.Values)) { presetNavigationOrder.Add(preset); } }
    private void UpdateCurrentPresetIndex() { currentPresetIndex = PresetNavigationHelper.ResolveIndex(presetNavigationOrder, currentPresetId); if (currentPresetIndex < 0 || currentPresetIndex >= presetNavigationOrder.Count) { return; } var resolvedPreset = presetNavigationOrder[currentPresetIndex]; if (!string.Equals(currentPresetId, resolvedPreset.PresetId, StringComparison.OrdinalIgnoreCase)) { activePreset = resolvedPreset; currentPresetId = resolvedPreset.PresetId; } }
    private void NavigatePreset(int direction) { if (presetNavigationOrder.Count == 0) { return; } var nextIndex = PresetNavigationHelper.WrapIndex(currentPresetIndex, direction, presetNavigationOrder.Count); if (nextIndex < 0) { return; } SelectPreset(presetNavigationOrder[nextIndex], showHud: true); }
    private void SelectPreset(PresetDefinition preset, bool showHud) { var selectedPreset = string.IsNullOrWhiteSpace(preset?.PresetId) ? ResolveBuiltInFallbackPreset(AudioMotionClonePresetId) : preset; activePreset = selectedPreset; currentPresetId = selectedPreset.PresetId; selectedRendererId = ResolveSelectedRendererId(selectedPreset.RendererId, selectedPreset.RendererId); UpdateCurrentPresetIndex(); if (showHud) { ShowPresetSwitchHud(); } var samePreset = string.Equals(viewModel.CurrentPresetId, currentPresetId, StringComparison.OrdinalIgnoreCase) && string.Equals(viewModel.SelectedRendererId, selectedRendererId, StringComparison.OrdinalIgnoreCase); if (samePreset) { return; } pipelineCoordinator.SetCurrentVisualizerIdentity(currentPresetId, ResolveCurrentRendererId()); viewModel.CurrentPresetId = currentPresetId; viewModel.SelectedRendererId = selectedRendererId; SelectComboOption(RendererCombo, selectedRendererId); ApplyRendererControlState(); lastCloneViewportWidth = GetAnalyzerViewportWidth(); _ = ApplyPendingVisualizerRuntimeImmediately(forceRebuild: false, persistSettings: true, refreshOutputs: true, updateStatusOnFailure: true); }
    private void EnsurePresetSwitchHudHideTimer() { if (presetSwitchHudHideTimer is not null) { return; } presetSwitchHudHideTimer = DispatcherQueue.CreateTimer(); presetSwitchHudHideTimer.Interval = TimeSpan.FromMilliseconds(1200); presetSwitchHudHideTimer.Tick += (_, _) => { presetSwitchHudHideTimer?.Stop(); HidePresetSwitchHud(); }; }
    private void ShowPresetSwitchHud() { if (currentPresetIndex < 0) { return; } PresetQuickSwitchText.Text = $"{currentPresetIndex + 1:00}. {activePreset.Name}"; PresetQuickSwitchHud.Visibility = Visibility.Visible; EnsurePresetSwitchHudHideTimer(); presetSwitchHudHideTimer!.Stop(); presetSwitchHudHideTimer.Start(); }
    private void HidePresetSwitchHud() { PresetQuickSwitchHud.Visibility = Visibility.Collapsed; }
    private AnalyzerRebuildOutcome? RebuildAnalyzer() { return ApplyPendingVisualizerRuntimeImmediately(forceRebuild: true, persistSettings: true, refreshOutputs: true, updateStatusOnFailure: true); }
    private SpectrumAnalyzer CreateAnalyzer(PresetDefinition preset, VisualizerRuntimeSettings runtimeSettings)
    {        // DOCS: docs/wiki/guides/change-visualizer-settings.md#passos
        return new SpectrumAnalyzer(VisualizerAnalyzerConfigFactory.Build(preset, runtimeSettings, GetAnalyzerViewportWidth(), preset.RendererId));
    }
    private void PopulateGifScaleModeCombo() { GifScaleModeCombo.ItemsSource = new List<ComboOption> { new(GifScaleMode.Fit.ToString(), "Fit", "Mantem proporcao com bordas."), new(GifScaleMode.Fill.ToString(), "Fill", "Preenche tudo com recorte central."), new(GifScaleMode.Stretch.ToString(), "Stretch", "Estica para ocupar toda a matriz."), }; }
    private PresetDefinition ResolveActivePreset(string presetId) { if (!string.IsNullOrWhiteSpace(presetId) && presetsById.TryGetValue(presetId, out var preset)) { return preset; } if (presetsById.TryGetValue(AudioMotionClonePresetId, out var clonePreset)) { return clonePreset; } return presetsById.Count > 0 ? presetsById.Values.First() : ResolveBuiltInFallbackPreset(AudioMotionClonePresetId); }
    private static void SelectComboOption(ComboBox comboBox, string id) { if (!string.IsNullOrWhiteSpace(comboBox.SelectedValuePath)) { comboBox.SelectedValue = id; return; } if (comboBox.ItemsSource is not IEnumerable<ComboOption> options) { return; } var match = options.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase)); if (match is not null) { comboBox.SelectedItem = match; } }
    private void UpdateHubPreviewVisibility()
    {
        var visible = ShouldShowHubPreviewPanel();
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
            if (GifControlsPanel.Visibility == Visibility.Visible)
            {
                availableHeight -= GifControlsPanel.ActualHeight + GifControlsPanel.Margin.Top + GifControlsPanel.Margin.Bottom;
            }
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

    private void UpdateGifControlsVisibility()
    {
        GifControlsPanel.Visibility = (!fullscreen && contentSourceMode == GifContentSourceMode.Gif)
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateHubPreviewVisibility();
        UpdateSettingsPaneModeState();
    }
    private void UpdateGifLoadingState(bool isLoading) { gifLoading = isLoading; GifLoadingRing.IsActive = isLoading; GifLoadingRing.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed; GifOpenFileButton.IsEnabled = !isLoading; GifScaleModeCombo.IsEnabled = !isLoading; UpdateGifTransportState(); UpdateSettingsPaneModeState(); }
    private void UpdateGifTransportState() { var hasFrames = gifFrames.Count > 0; GifPlayButton.IsEnabled = !gifLoading && hasFrames && !gifPlayer.IsPlaying; GifPauseButton.IsEnabled = !gifLoading && hasFrames && gifPlayer.IsPlaying; GifStopButton.IsEnabled = !gifLoading && hasFrames; }
    private async Task SwitchContentModeAsync(GifContentSourceMode targetMode)
    {
        if (contentSourceMode == targetMode)
        {
            return;
        }

        if (targetMode == GifContentSourceMode.Gif)
        {
            await pipelineCoordinator.StopAsync().ConfigureAwait(true);
            contentSourceMode = GifContentSourceMode.Gif;
            ConfigureHubOutputsForCurrentState();
            PushCurrentHubFrameIfNeeded();
            UpdateRenderLoopState();
            await SyncHub75DeviceSessionAsync().ConfigureAwait(true);
            StatusText.Text = gifFrames.Count > 0
                ? "Modo GIF ativo (12 FPS)."
                : "Modo GIF ativo. Carregue um GIF para iniciar.";
        }
        else
        {
            gifPlayer.Stop();
            contentSourceMode = GifContentSourceMode.Audio;
            SyncHubTransportMode();
            await pipelineCoordinator.StartAsync(
                    enableSimulator: ShouldShowHubPreviewPanel(),
                    enableHub75DeviceOutput: ShouldEnableHub75DeviceOutput(),
                    brightness: appSettings.Brightness,
                    presetId: currentPresetId,
                    rendererId: ResolveCurrentRendererId())
                .ConfigureAwait(true);
            UpdateRenderLoopState();
            PumpHubFrameOutput(force: true);
            await SyncHub75DeviceSessionAsync().ConfigureAwait(true);
        }

        suppressContentModeChanged = true;
        SelectComboOption(ContentModeCombo, contentSourceMode.ToString());
        suppressContentModeChanged = false;
        UpdateGifControlsVisibility();
        UpdateGifTransportState();
        RefreshVisibleCanvases();
    }

    private async Task ReformatLoadedGifFramesAsync()
    {
        if (decodedGifFrames.Count == 0)
        {
            return;
        }

        var wasPlaying = gifPlayer.IsPlaying;
        var formatted = await Task.Run(() =>
        {
            var buffer = new List<RgbaColor[]>(decodedGifFrames.Count);
            foreach (var decodedFrame in decodedGifFrames)
            {
                buffer.Add(gifFrameFormatter.Format(decodedFrame, gifScaleMode));
            }

            return (IReadOnlyList<RgbaColor[]>)buffer;
        }).ConfigureAwait(true);

        gifFrames = formatted;
        gifPlayer.SetFrames(gifFrames);
        lastGifFrame = gifFrames[0];

        if (contentSourceMode == GifContentSourceMode.Gif)
        {
            PushCurrentHubFrameIfNeeded();
        }

        if (wasPlaying)
        {
            gifPlayer.Play();
        }

        StatusText.Text = $"GIF reformado ({gifScaleMode}, {gifFrames.Count} frames).";
        UpdateGifTransportState();
        RefreshVisibleCanvases();
    }

    private async Task LoadGifAsync(string sourceLabel, Func<CancellationToken, Task<byte[]>> acquireBytesAsync)
    {
        if (gifLoading)
        {
            return;
        }

        gifLoadCts?.Cancel();
        gifLoadCts?.Dispose();
        gifLoadCts = new CancellationTokenSource();
        var cancellationToken = gifLoadCts.Token;
        var shouldAutoplay = gifPlayer.IsPlaying || contentSourceMode == GifContentSourceMode.Gif;

        try
        {
            UpdateGifLoadingState(true);
            StatusText.Text = $"Carregando GIF ({sourceLabel})...";
            var gifBytes = await acquireBytesAsync(cancellationToken).ConfigureAwait(true);
            if (gifBytes.Length == 0)
            {
                throw new InvalidDataException("Conteudo vazio.");
            }

            if (gifBytes.Length > GifMaxDownloadBytes)
            {
                throw new InvalidDataException("Arquivo acima de 25MB.");
            }

            var decoded = await Task.Run(() => gifDecoder.Decode(gifBytes, cancellationToken), cancellationToken).ConfigureAwait(true);
            if (decoded.Count == 0)
            {
                throw new InvalidDataException("GIF sem frames validos.");
            }

            var formatted = await Task.Run(() =>
            {
                var frameBuffer = new List<RgbaColor[]>(decoded.Count);
                foreach (var decodedFrame in decoded)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    frameBuffer.Add(gifFrameFormatter.Format(decodedFrame, gifScaleMode));
                }

                return (IReadOnlyList<RgbaColor[]>)frameBuffer;
            }, cancellationToken).ConfigureAwait(true);

            decodedGifFrames = decoded;
            gifFrames = formatted;
            gifPlayer.SetFrames(gifFrames);
            lastGifFrame = gifFrames[0];

            if (contentSourceMode == GifContentSourceMode.Gif)
            {
                ConfigureHubOutputsForCurrentState();
                PushCurrentHubFrameIfNeeded();
            }

            if (shouldAutoplay)
            {
                if (contentSourceMode != GifContentSourceMode.Gif)
                {
                    await SwitchContentModeAsync(GifContentSourceMode.Gif).ConfigureAwait(true);
                }

                gifPlayer.Play();
            }

            StatusText.Text = $"GIF carregado: {gifFrames.Count} frames, {GifTargetFps} FPS fixos.";
            UpdateGifTransportState();
            RefreshVisibleCanvases();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Carregamento de GIF cancelado.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erro ao carregar GIF: {ex.Message}";
        }
        finally
        {
            UpdateGifLoadingState(false);
        }
    }
    private static async Task<StorageFile?> PickGifFileAsync() { var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, }; picker.FileTypeFilter.Add(".gif"); if (App.MainWindow is not null) { var hwnd = WindowNative.GetWindowHandle(App.MainWindow); InitializeWithWindow.Initialize(picker, hwnd); } return await picker.PickSingleFileAsync(); }
    private void OnGifFrameReady(object? sender, RgbaColor[] frame)
    {
        if (frame.Length != LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        {
            return;
        }

        lastGifFrame = frame;
        PushCurrentHubFrameIfNeeded();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            RefreshVisibleCanvases();
            UpdateGifTransportState();
        });
    }
    private bool IsBuiltInClonePresetActive() => string.Equals(activePreset.PresetId, AudioMotionClonePresetId, StringComparison.OrdinalIgnoreCase); private string ResolveCurrentRendererId() => IsBuiltInClonePresetActive() ? RendererIds.AudioMotionClone : selectedRendererId; private bool IsCloneDisplayModeActive() => string.Equals(ResolveCurrentRendererId(), RendererIds.AudioMotionClone, StringComparison.OrdinalIgnoreCase); private RendererCapabilities GetActiveRendererCapabilities() => visualizer.GetCapabilities(ResolveCurrentRendererId()); private string ResolveSelectedRendererId(string configuredRendererId, string fallbackRendererId) { return ResolveSupportedRendererId(configuredRendererId, fallbackRendererId, GetAvailableRendererIds()); }
    private float GetAnalyzerViewportWidth() { var width = (float)MainCanvas.ActualWidth; if (width > 1f) { var dpiScale = MathF.Max(1f, MainCanvas.Dpi / 96f); return width * dpiScale; } appWindow ??= GetAppWindow(); var fallbackWidth = appWindow is null ? 1280f : Math.Max(640f, appWindow.Size.Width - 24f); var rasterScale = (float)(XamlRoot?.RasterizationScale ?? 1d); return fallbackWidth * MathF.Max(1f, rasterScale); }
    private static FrequencyScale CoerceFrequencyScale(FrequencyScale scale) => VisualizerRuntimeSettings.NormalizeFrequencyScale(scale);
    private static float CoerceFftSmoothing(float value) => VisualizerRuntimeSettings.NormalizeFftSmoothing(value);
    private static WeightingFilter CoerceWeightingFilter(WeightingFilter value) => VisualizerRuntimeSettings.NormalizeWeightingFilter(value);
    private static string ToWeightingFilterId(WeightingFilter value) { return value switch { WeightingFilter.A => "A", WeightingFilter.B => "B", WeightingFilter.C => "C", WeightingFilter.D => "D", WeightingFilter.Filter468 => "468", _ => "OFF", }; }
    private static WeightingFilter ParseWeightingFilterId(string id) { return id.ToUpperInvariant() switch { "A" => WeightingFilter.A, "B" => WeightingFilter.B, "C" => WeightingFilter.C, "D" => WeightingFilter.D, "468" => WeightingFilter.Filter468, _ => WeightingFilter.Off, }; }
    private static float CoerceFrequencyMin(float value) => VisualizerRuntimeSettings.NormalizeFrequencyMin(value); private static float CoerceFrequencyMax(float value) => VisualizerRuntimeSettings.NormalizeFrequencyMax(value); private void EnsureFrequencyRangeOrder() { if (frequencyMaxHz > frequencyMinHz) { frequencyMinHz = FindClosest(FrequencyMinOptions, frequencyMinHz); frequencyMaxHz = FindClosest(FrequencyMaxOptions, frequencyMaxHz); return; } var nextMax = FrequencyMaxOptions.FirstOrDefault(v => v > frequencyMinHz); if (nextMax > 0f) { frequencyMaxHz = nextMax; } else { frequencyMinHz = FrequencyMinOptions[^2]; frequencyMaxHz = FrequencyMaxOptions[^1]; } }
    private static float FindClosest(float[] options, float value) { var best = options[0]; var bestDistance = Math.Abs(best - value); for (var i = 1; i < options.Length; i++) { var current = options[i]; var distance = Math.Abs(current - value); if (distance < bestDistance) { best = current; bestDistance = distance; } } return best; }
    private static string FormatFrequencyId(float value) => value.ToString("0", System.Globalization.CultureInfo.InvariantCulture); private static string FormatFrequencyLabel(float value) => value >= 1000f ? $"{value / 1000f:0.#} kHz" : $"{value:0} Hz"; private static bool TryParseFrequencyOption(string value, out float frequencyHz) => float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out frequencyHz); private void RestoreWindowSize() { if (appWindow is null || appSettings.WindowWidth < 640 || appSettings.WindowHeight < 480) { return; } appWindow.Resize(new SizeInt32(appSettings.WindowWidth, appSettings.WindowHeight)); }
    private void SaveWindowSizeIntoSettings() { PersistCurrentVisualizerSettings(includePreview: true, includeWindowSize: true); }
    private void OnCaptureStatusChanged(object? sender, CaptureStatusChangedEventArgs e) { if (contentSourceMode == GifContentSourceMode.Gif) { return; } _ = DispatcherQueue.EnqueueAsync(() => { StatusText.Text = e.Message; }); }
    private void OnPipelineCoordinatorStatusChanged(object? sender, string message) { if (contentSourceMode == GifContentSourceMode.Gif) { return; } _ = DispatcherQueue.EnqueueAsync(() => { StatusText.Text = message; }); }
    private static AppWindow? GetAppWindow() { if (App.MainWindow is null) { return null; } var hwnd = WindowNative.GetWindowHandle(App.MainWindow); var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd); return AppWindow.GetFromWindowId(id); }
    private sealed record ComboOption(string Id, string Label, string? ToolTip = null) { public override string ToString() => Label; }
}




