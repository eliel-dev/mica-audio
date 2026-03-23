using System.Diagnostics;
using Analyzer.Dsp.Analysis;
using App.WinUI.Services;
using App.WinUI.Services.Visualizer;
using App.WinUI.Views.Controls;
using App.WinUI.Views.Controls.Renderers;
using Audio.Loopback.Capture;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Output.Led;
using Visual.Win2D.Engine;
using Windows.Foundation;
using Windows.UI;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
// DOCS: docs/wiki/modules/visual-win2d.md#studio-de-presets
internal sealed class VisualizerStudioPage : Page, IShellNavigationGuard, IDisposable
{
    private const string AudioMotionClonePresetId = "audiomotion-clone";
    private const string DefaultPaletteName = "Gradiente personalizado";
    private const double DefaultPresetListWidth = 296d;
    private const double MinimumPresetListHeight = 280d;
    private const double MaximumPresetListHeight = 760d;
    private const double MinimumPreviewHeight = 280d;
    private const double MaximumPreviewHeight = 520d;
    private const double CompactLayoutBreakpoint = 1280d;

    private static readonly IReadOnlyList<RgbaColor> SwatchColors =
    [
        new(15, 23, 60),
        new(28, 58, 146),
        new(43, 106, 225),
        new(67, 156, 224),
        new(85, 190, 255),
        new(94, 214, 185),
        new(46, 184, 118),
        new(84, 222, 142),
        new(156, 242, 193),
        new(255, 196, 37),
        new(255, 218, 10),
        new(255, 148, 33),
        new(255, 98, 58),
        new(223, 74, 36),
        new(244, 118, 188),
        new(186, 65, 231),
        new(135, 90, 226),
        new(95, 24, 186),
        new(245, 245, 245),
        new(12, 12, 16),
    ];

    private readonly PresetRepository presetRepository;
    private readonly BuiltInPresetNameOverrideStore builtInNameOverrideStore;
    private readonly SettingsRepository settingsRepository;
    private readonly AppSettingsDomainService settingsDomainService;
    private readonly ILoopbackCapture capture;
    private readonly SimulatorLedOutput simulatorLedOutput;
    private readonly NullLedOutput nullLedOutput;
    private readonly VisualizerEditorNavigationCoordinator navigationCoordinator;
    private readonly VisualizerEngine visualizer = new();
    private readonly AudioPipelineCoordinator pipelineCoordinator;
    private readonly IReadOnlyList<GradientPaletteTemplate> quickGradientPresets;
    private readonly Dictionary<string, PresetDefinition> rawPresetsById = new(StringComparer.OrdinalIgnoreCase);

    private readonly Button backButton = new();
    private readonly TextBlock routeText = new();
    private readonly Button saveButton = new();
    private readonly TextBlock statusText = new();
    private readonly TextBlock selectionHintText = new();
    private readonly ListView presetList = new();
    private readonly Button cloneButton = new();
    private readonly ComboBox compactPresetPicker = new();
    private readonly TextBox nameTextBox = new();
    private readonly ScrollViewer contentScrollViewer = new();
    private readonly Grid contentGrid = new();
    private readonly Border presetRail = new();
    private readonly Border compactPresetStrip = new();
    private readonly Grid workspaceGrid = new();
    private readonly Grid editorGrid = new();
    private readonly StackPanel editorPrimaryColumn = new();
    private readonly StackPanel editorSecondaryColumn = new();
    private readonly Border previewSurface = new();
    private readonly CanvasControl previewCanvas = new();
    private readonly TextBlock previewTitleText = new();
    private readonly TextBlock previewModeHintText = new();
    private readonly Button hubPreviewModeButton = new();
    private readonly Button canvasPreviewModeButton = new();
    private readonly PaletteGradientStripControl gradientStrip = new();
    private readonly TextBlock stopSummaryText = new();
    private readonly Slider positionSlider = new();
    private readonly TextBlock positionValueText = new();
    private readonly Grid swatchGrid = new();
    private readonly StackPanel quickPresetButtonsPanel = new();
    private readonly Button removeStopButton = new();

    private DispatcherQueueTimer? renderTimer;
    private IAnalyzer analyzer = new SpectrumAnalyzer(new AnalyzerConfig());
    private AppSettings appSettings = new();
    private VisualizerRuntimeSettings runtimeSettings = VisualizerRuntimeSettings.Default;
    private IReadOnlyDictionary<string, string> builtInNameOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PaletteStop> workingStops = Array.Empty<PaletteStop>();
    private StudioPresetItem[] presetItems = [];
    private PresetDefinition selectedPreset = null!;
    private PresetDefinition editablePreset = null!;
    private long lastRenderQpc;
    private float lastPreviewViewportWidth;
    private string pendingPresetId = AudioMotionClonePresetId;
    private string workingDisplayName = string.Empty;
    private string workingPaletteName = DefaultPaletteName;
    private string returnPresetId = AudioMotionClonePresetId;
    private string? transientClonePresetId;
    private int selectedStopIndex;
    private bool hasPendingChanges;
    private bool isCompactLayout;
    private bool isLoaded;
    private bool suppressUiEvents;
    private PreviewSurfaceMode previewSurfaceMode = PreviewSurfaceMode.Hub75;

    internal VisualizerStudioPage(
        PresetRepository presetRepository,
        BuiltInPresetNameOverrideStore builtInNameOverrideStore,
        SettingsRepository settingsRepository,
        AppSettingsDomainService settingsDomainService,
        ILoopbackCapture capture,
        SimulatorLedOutput simulatorLedOutput,
        NullLedOutput nullLedOutput,
        VisualizerEditorNavigationCoordinator navigationCoordinator)
    {
        this.presetRepository = presetRepository;
        this.builtInNameOverrideStore = builtInNameOverrideStore;
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;
        this.capture = capture;
        this.simulatorLedOutput = simulatorLedOutput;
        this.nullLedOutput = nullLedOutput;
        this.navigationCoordinator = navigationCoordinator;
        quickGradientPresets = VisualizerPresetPaletteEditor.GetQuickGradientPresets();

        pipelineCoordinator = new AudioPipelineCoordinator(
            capture,
            simulatorLedOutput,
            nullLedOutput,
            nullLedOutput,
            analyzer);

        KeyboardAccelerators.Add(BuildAccelerator(Windows.System.VirtualKey.S, Windows.System.VirtualKeyModifiers.Control, OnSaveAcceleratorInvoked));
        KeyboardAccelerators.Add(BuildAccelerator(Windows.System.VirtualKey.Escape, Windows.System.VirtualKeyModifiers.None, OnBackAcceleratorInvoked));

        Content = BuildLayout();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnPageSizeChanged;
    }

    internal void PrepareSession(string presetId)
    {
        pendingPresetId = string.IsNullOrWhiteSpace(presetId) ? AudioMotionClonePresetId : presetId;
        returnPresetId = pendingPresetId;

        if (!isLoaded)
        {
            return;
        }

        _ = DispatcherQueue.EnqueueAsync(async () =>
        {
            await LoadSessionAsync().ConfigureAwait(true);
            await StartPreviewAsync().ConfigureAwait(true);
        });
    }

    public async Task<bool> CanLeaveAsync()
    {
        return await ConfirmPendingChangesAsync(
                "Salvar alteracoes do Studio?",
                "Voce tem ajustes pendentes. Salve, descarte ou continue editando antes de sair do Studio.")
            .ConfigureAwait(true);
    }

    private Grid BuildLayout()
    {
        Background = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 22, 24, 30));

        backButton.Content = "Voltar ao visualizador";
        backButton.Style = ResolveStyle("AppChromeButtonStyle");
        backButton.Click += OnBackClicked;

        routeText.Text = "Visualizador / Studio de preset";
        routeText.Style = ResolveStyle("AppMutedTextStyle");
        routeText.FontSize = 12d;
        routeText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;

        saveButton.Content = "Salvar";
        saveButton.Style = ResolveStyle("AppChromeButtonStyle");
        saveButton.Click += OnSaveClicked;

        cloneButton.Content = "Salvar como novo";
        cloneButton.Style = ResolveStyle("AppChromeButtonStyle");
        cloneButton.Click += OnCloneClicked;
        ToolTipService.SetToolTip(cloneButton, "Criar um novo preset a partir do selecionado.");

        statusText.Style = ResolveStyle("AppStatusTextStyle");
        statusText.Text = "Carregando preset...";
        statusText.TextWrapping = TextWrapping.Wrap;

        selectionHintText.Style = ResolveStyle("AppMutedTextStyle");
        selectionHintText.Text = "Edite no Canvas, confira a leitura HUB75 e salve.";
        selectionHintText.TextWrapping = TextWrapping.Wrap;

        presetList.SelectionMode = ListViewSelectionMode.Single;
        presetList.SelectionChanged += OnPresetListSelectionChanged;
        presetList.SelectedValuePath = nameof(StudioPresetItem.Id);
        presetList.DisplayMemberPath = nameof(StudioPresetItem.Label);
        presetList.Style = ResolveStyle("AppListViewStyle");
        presetList.MinHeight = MinimumPresetListHeight;
        presetList.MaxHeight = 520d;

        compactPresetPicker.Style = ResolveStyle("AppChromeComboBoxStyle");
        compactPresetPicker.MinWidth = 280d;
        compactPresetPicker.SelectedValuePath = nameof(StudioPresetItem.Id);
        compactPresetPicker.DisplayMemberPath = nameof(StudioPresetItem.Label);
        compactPresetPicker.SelectionChanged += OnCompactPresetPickerSelectionChanged;

        nameTextBox.Style = ResolveStyle("AppChromeTextBoxStyle");
        nameTextBox.TextChanged += OnNameTextChanged;

        previewCanvas.ClearColor = Color.FromArgb(255, 0, 0, 0);
        previewCanvas.Draw += OnPreviewCanvasDraw;
        previewCanvas.SizeChanged += OnPreviewCanvasSizeChanged;

        previewTitleText.Style = ResolveStyle("AppHeaderTextStyle");
        previewTitleText.TextWrapping = TextWrapping.Wrap;

        previewModeHintText.Style = ResolveStyle("AppMutedTextStyle");
        previewModeHintText.TextWrapping = TextWrapping.Wrap;

        hubPreviewModeButton.Content = "Painel HUB75";
        hubPreviewModeButton.Style = ResolveStyle("AppChromeButtonStyle");
        hubPreviewModeButton.Click += OnHubPreviewModeClicked;

        canvasPreviewModeButton.Content = "Canvas";
        canvasPreviewModeButton.Style = ResolveStyle("AppChromeButtonStyle");
        canvasPreviewModeButton.Click += OnCanvasPreviewModeClicked;

        gradientStrip.HorizontalAlignment = HorizontalAlignment.Stretch;
        gradientStrip.StopSelected += OnGradientStopSelected;
        gradientStrip.TrackInvoked += OnGradientTrackInvoked;

        stopSummaryText.Style = ResolveStyle("AppSettingsSectionTitleTextStyle");

        positionSlider.Minimum = 0d;
        positionSlider.Maximum = 100d;
        positionSlider.StepFrequency = 1d;
        positionSlider.SmallChange = 1d;
        positionSlider.ValueChanged += OnPositionSliderChanged;

        positionValueText.Style = ResolveStyle("AppSettingsValueTextStyle");
        positionValueText.VerticalAlignment = VerticalAlignment.Center;

        removeStopButton.Content = "Remover ponto";
        removeStopButton.Style = ResolveStyle("AppChromeButtonStyle");
        removeStopButton.HorizontalAlignment = HorizontalAlignment.Left;
        removeStopButton.Click += OnRemoveStopClicked;

        BuildQuickPresetButtons();
        RefreshPreviewModeButtons();

        presetRail.Padding = new Thickness(16d);
        presetRail.CornerRadius = new CornerRadius(18d);
        presetRail.Background = ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 24, 32));
        presetRail.BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 76, 82, 96));
        presetRail.BorderThickness = new Thickness(1d);
        presetRail.Child = BuildPresetRail();

        compactPresetStrip.Padding = new Thickness(14d);
        compactPresetStrip.CornerRadius = new CornerRadius(16d);
        compactPresetStrip.Background = ResolveBrush("AppSurfacePanelBrush", Color.FromArgb(255, 20, 24, 32));
        compactPresetStrip.BorderBrush = ResolveBrush("AppSurfaceStrokeBrush", Color.FromArgb(255, 76, 82, 96));
        compactPresetStrip.BorderThickness = new Thickness(1d);
        compactPresetStrip.Child = BuildCompactPresetStrip();

        previewSurface.Background = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
        previewSurface.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 34, 38, 48));
        previewSurface.BorderThickness = new Thickness(1d);
        previewSurface.CornerRadius = new CornerRadius(20d);
        previewSurface.MinHeight = MinimumPreviewHeight;
        previewSurface.Height = 380d;
        previewSurface.Child = previewCanvas;

        workspaceGrid.RowSpacing = 20d;
        workspaceGrid.RowDefinitions.Clear();
        workspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspaceGrid.Children.Clear();
        workspaceGrid.Children.Add(BuildPreviewWorkbench());
        workspaceGrid.Children.Add(BuildEditorWorkbench());
        Grid.SetRow((FrameworkElement)workspaceGrid.Children[1], 1);

        contentGrid.Margin = new Thickness(0d, 18d, 0d, 0d);
        contentGrid.RowSpacing = 18d;
        contentGrid.ColumnSpacing = 24d;
        contentGrid.RowDefinitions.Clear();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Clear();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DefaultPresetListWidth) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        contentGrid.Children.Clear();
        contentGrid.Children.Add(compactPresetStrip);
        contentGrid.Children.Add(presetRail);
        contentGrid.Children.Add(workspaceGrid);
        Grid.SetRow(compactPresetStrip, 0);
        Grid.SetColumnSpan(compactPresetStrip, 2);
        Grid.SetRow(presetRail, 1);
        Grid.SetRow(workspaceGrid, 1);
        Grid.SetColumn(workspaceGrid, 1);

        contentScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        contentScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        contentScrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
        contentScrollViewer.VerticalScrollMode = ScrollMode.Enabled;
        contentScrollViewer.Content = contentGrid;

        var headerGrid = new Grid
        {
            ColumnSpacing = 18d,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        headerGrid.Children.Add(backButton);
        headerGrid.Children.Add(new StackPanel
        {
            Spacing = 4d,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                routeText,
                new TextBlock
                {
                    Text = "Studio de preset",
                    Style = ResolveStyle("AppHeaderTextStyle"),
                },
                selectionHintText,
                statusText,
            },
        });
        headerGrid.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10d,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                cloneButton,
                saveButton,
            },
        });
        Grid.SetColumn((FrameworkElement)headerGrid.Children[1], 1);
        Grid.SetColumn((FrameworkElement)headerGrid.Children[2], 2);

        var root = new Grid
        {
            Background = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 22, 24, 30)),
            Margin = new Thickness(18d, 16d, 18d, 18d),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) },
            },
        };

        root.Children.Add(headerGrid);
        root.Children.Add(contentScrollViewer);
        Grid.SetRow(contentScrollViewer, 1);
        return root;
    }

    private Grid BuildPresetRail()
    {
        var grid = new Grid
        {
            RowSpacing = 14d,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) },
            },
        };
        grid.Children.Add(new StackPanel
        {
            Spacing = 4d,
            Children =
            {
                new TextBlock
                {
                    Text = "Presets",
                    Style = ResolveStyle("AppSettingsSectionTitleTextStyle"),
                },
                new TextBlock
                {
                    Text = "Selecione o preset que voce quer editar.",
                    Style = ResolveStyle("AppMutedTextStyle"),
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        });
        grid.Children.Add(presetList);
        Grid.SetRow(presetList, 1);
        return grid;
    }

    private Grid BuildCompactPresetStrip()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12d,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) },
            },
        };
        grid.Children.Add(new TextBlock
        {
            Text = "Preset",
            Style = ResolveStyle("AppSettingsSectionTitleTextStyle"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        grid.Children.Add(compactPresetPicker);
        Grid.SetColumn(compactPresetPicker, 1);
        return grid;
    }

    private Border BuildPreviewWorkbench()
    {
        var grid = new Grid
        {
            RowSpacing = 18d,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) },
            },
        };
        var header = new Grid
        {
            ColumnSpacing = 12d,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        header.Children.Add(new StackPanel
        {
            Spacing = 4d,
            Children =
            {
                new TextBlock
                {
                    Text = "Preview",
                    Style = ResolveStyle("AppMutedTextStyle"),
                },
                previewTitleText,
                previewModeHintText,
            },
        });
        header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8d,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                hubPreviewModeButton,
                canvasPreviewModeButton,
            },
        });
        Grid.SetColumn((FrameworkElement)header.Children[1], 1);
        grid.Children.Add(header);
        grid.Children.Add(previewSurface);
        Grid.SetRow(previewSurface, 1);
        return CreateSectionBorder(grid);
    }

    private Border BuildEditorWorkbench()
    {
        var positionGrid = new Grid
        {
            ColumnSpacing = 12d,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        positionGrid.Children.Add(positionSlider);
        positionGrid.Children.Add(positionValueText);
        Grid.SetColumn(positionValueText, 1);

        editorPrimaryColumn.Spacing = 14d;
        editorPrimaryColumn.Children.Clear();
        editorPrimaryColumn.Children.Add(new StackPanel
        {
            Spacing = 6d,
            Children =
            {
                new TextBlock
                {
                    Text = "Nome do preset",
                    Style = ResolveStyle("AppSettingsSectionTitleTextStyle"),
                },
                nameTextBox,
            },
        });
        editorPrimaryColumn.Children.Add(new StackPanel
        {
            Spacing = 8d,
            Children =
            {
                new TextBlock
                {
                    Text = "Gradiente",
                    Style = ResolveStyle("AppSettingsSectionTitleTextStyle"),
                },
                new TextBlock
                {
                    Text = "Clique para adicionar ou selecionar pontos.",
                    Style = ResolveStyle("AppMutedTextStyle"),
                    TextWrapping = TextWrapping.Wrap,
                },
                gradientStrip,
            },
        });

        editorSecondaryColumn.Spacing = 14d;
        editorSecondaryColumn.Children.Clear();
        editorSecondaryColumn.Children.Add(stopSummaryText);
        editorSecondaryColumn.Children.Add(new TextBlock
        {
            Text = "Cor",
            Style = ResolveStyle("AppMutedTextStyle"),
        });
        editorSecondaryColumn.Children.Add(swatchGrid);
        editorSecondaryColumn.Children.Add(new TextBlock
        {
            Text = "Posicao",
            Style = ResolveStyle("AppMutedTextStyle"),
        });
        editorSecondaryColumn.Children.Add(positionGrid);
        editorSecondaryColumn.Children.Add(removeStopButton);
        editorSecondaryColumn.Children.Add(new TextBlock
        {
            Text = "Bases rapidas",
            Style = ResolveStyle("AppMutedTextStyle"),
        });
        editorSecondaryColumn.Children.Add(quickPresetButtonsPanel);

        editorGrid.RowSpacing = 18d;
        editorGrid.ColumnSpacing = 24d;
        editorGrid.Children.Clear();
        editorGrid.Children.Add(editorPrimaryColumn);
        editorGrid.Children.Add(editorSecondaryColumn);
        return CreateSectionBorder(editorGrid);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        isLoaded = true;

        try
        {
            await LoadSessionAsync().ConfigureAwait(true);
            EnsureRenderTimer();
            renderTimer!.Start();
            await StartPreviewAsync().ConfigureAwait(true);
            UpdateAdaptiveLayout(ActualWidth, ActualHeight);
            previewCanvas.Invalidate();
        }
        catch (Exception ex)
        {
            statusText.Text = $"Falha ao abrir Studio: {ex.Message}";
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAdaptiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        isLoaded = false;
        renderTimer?.Stop();
        lastRenderQpc = 0;

        try
        {
            await pipelineCoordinator.StopAsync().ConfigureAwait(true);
        }
        catch
        {
        }
    }

    private async Task LoadSessionAsync()
    {
        appSettings = settingsDomainService.Migrate(await settingsRepository.LoadAsync().ConfigureAwait(true));
        runtimeSettings = settingsDomainService.GetVisualizerRuntimeSettings(appSettings);
        var loadedPresets = await presetRepository.LoadOrSeedAsync().ConfigureAwait(true);
        builtInNameOverrides = await builtInNameOverrideStore.LoadAsync().ConfigureAwait(true);

        rawPresetsById.Clear();
        foreach (var preset in loadedPresets)
        {
            if (preset is null || string.IsNullOrWhiteSpace(preset.PresetId))
            {
                continue;
            }

            rawPresetsById[preset.PresetId] = preset;
        }

        transientClonePresetId = null;
        var targetPresetId = ResolveInitialPresetId();
        returnPresetId = targetPresetId;
        RebuildPresetItems();
        LoadPreset(targetPresetId, resetDirty: true);
    }

    private string ResolveInitialPresetId()
    {
        if (!string.IsNullOrWhiteSpace(pendingPresetId) && rawPresetsById.ContainsKey(pendingPresetId))
        {
            var resolved = pendingPresetId;
            pendingPresetId = AudioMotionClonePresetId;
            return resolved;
        }

        pendingPresetId = AudioMotionClonePresetId;

        if (rawPresetsById.ContainsKey(AudioMotionClonePresetId))
        {
            return AudioMotionClonePresetId;
        }

        return rawPresetsById.Count > 0 ? rawPresetsById.Keys.First() : AudioMotionClonePresetId;
    }

    private void RebuildPresetItems()
    {
        presetItems = rawPresetsById.Values
            .Select(preset => new StudioPresetItem(
                preset.PresetId,
                BuildPresetListLabelValue(preset),
                string.Equals(returnPresetId, preset.PresetId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(static item => item.SortLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        suppressUiEvents = true;
        presetList.ItemsSource = presetItems;
        presetList.SelectedValue = selectedPreset?.PresetId;
        compactPresetPicker.ItemsSource = presetItems;
        compactPresetPicker.SelectedValue = selectedPreset?.PresetId;
        suppressUiEvents = false;
    }

    private void LoadPreset(string presetId, bool resetDirty)
    {
        if (!rawPresetsById.TryGetValue(presetId, out var preset))
        {
            return;
        }

        selectedPreset = preset;
        workingDisplayName = ResolveDisplayName(selectedPreset);

        var displayPresets = rawPresetsById.Values
            .Select(item => VisualizerPresetPaletteEditor.ApplyDisplayName(item, ResolveDisplayName(item)))
            .ToDictionary(static item => item.PresetId, StringComparer.OrdinalIgnoreCase);
        editablePreset = VisualizerPresetPaletteEditor.ResolveEditablePreset(displayPresets[presetId], displayPresets);

        workingStops = VisualizerPresetPaletteEditor.NormalizeStops(editablePreset.Palette.Stops);
        workingPaletteName = string.IsNullOrWhiteSpace(editablePreset.Palette?.Name)
            ? DefaultPaletteName
            : editablePreset.Palette.Name;
        selectedStopIndex = ResolveInitialStopIndex(workingStops);
        if (resetDirty)
        {
            hasPendingChanges = false;
        }

        RefreshEditorState();
        RebuildPreviewAnalyzer();
        previewCanvas.Invalidate();
    }

    private void RefreshEditorState()
    {
        suppressUiEvents = true;
        presetList.SelectedValue = selectedPreset?.PresetId;
        compactPresetPicker.SelectedValue = selectedPreset?.PresetId;
        gradientStrip.Stops = workingStops;
        gradientStrip.SelectedIndex = selectedStopIndex;
        nameTextBox.Text = workingDisplayName;
        positionSlider.Value = workingStops.Count == 0
            ? 0d
            : workingStops[selectedStopIndex].Offset * 100d;
        suppressUiEvents = false;

        stopSummaryText.Text = $"Ponto de cor {selectedStopIndex + 1} de {workingStops.Count}";
        positionValueText.Text = $"{Math.Round(positionSlider.Value):0}%";
        removeStopButton.IsEnabled = workingStops.Count > VisualizerPresetPaletteEditor.MinimumStopCount;
        cloneButton.IsEnabled = selectedPreset is not null;
        previewTitleText.Text = selectedPreset is null ? "Sem preset selecionado" : ResolveDisplayName(selectedPreset);
        previewModeHintText.Text = ResolvePreviewModeHint();
        statusText.Text = ResolveStudioStatus();

        BuildSwatchGrid(workingStops[selectedStopIndex].Color);
        RefreshPreviewModeButtons();
        RebuildPresetItems();
    }

    private void EnsureRenderTimer()
    {
        if (renderTimer is not null)
        {
            return;
        }

        renderTimer = DispatcherQueue.CreateTimer();
        renderTimer.Interval = TimeSpan.FromMilliseconds(1000d / 60d);
        renderTimer.Tick += (_, _) => previewCanvas.Invalidate();
    }

    private async Task StartPreviewAsync()
    {
        if (!TryBuildPreviewPreset(out var previewPreset))
        {
            return;
        }

        pipelineCoordinator.SetAnalyzer(analyzer);
        pipelineCoordinator.SetCurrentVisualizerIdentity(previewPreset.PresetId, previewPreset.RendererId);
        pipelineCoordinator.SetHubTransportMode(ResolveHubTransportMode(previewPreset));
        await pipelineCoordinator.StartAsync(
                enableSimulator: previewSurfaceMode == PreviewSurfaceMode.Hub75,
                enableHub75DeviceOutput: false,
                brightness: appSettings.Brightness,
                presetId: previewPreset.PresetId,
                rendererId: previewPreset.RendererId)
            .ConfigureAwait(true);
    }

    private void RebuildPreviewAnalyzer()
    {
        if (!TryBuildPreviewPreset(out var previewPreset))
        {
            return;
        }

        analyzer = new SpectrumAnalyzer(
            VisualizerAnalyzerConfigFactory.Build(
                previewPreset,
                runtimeSettings,
                GetAnalyzerViewportWidth(),
                previewPreset.RendererId));
        pipelineCoordinator.SetAnalyzer(analyzer);
        pipelineCoordinator.SetCurrentVisualizerIdentity(previewPreset.PresetId, previewPreset.RendererId);
        pipelineCoordinator.SetHubTransportMode(ResolveHubTransportMode(previewPreset));
    }

    private bool TryBuildPreviewPreset(out PresetDefinition previewPreset)
    {
        previewPreset = null!;

        if (selectedPreset is null
            || editablePreset is null
            || workingStops.Count < VisualizerPresetPaletteEditor.MinimumStopCount)
        {
            return false;
        }

        var previewName = VisualizerPresetPaletteEditor.IsBuiltInPreset(selectedPreset)
            ? VisualizerPresetPaletteEditor.ResolveEditablePresetNameFromDisplayName(workingDisplayName)
            : workingDisplayName;
        var namedPreset = VisualizerPresetPaletteEditor.ApplyDisplayName(editablePreset, previewName);
        var stagedPreset = VisualizerPresetPaletteEditor.ApplyPalette(namedPreset, workingPaletteName, workingStops);
        previewPreset = MainPage.BuildSafeAnalyzerPreset(
            stagedPreset,
            stagedPreset.RendererId,
            runtimeSettings.DisplayBandCount,
            visualizer.Renderers.Select(renderer => renderer.RendererId),
            AudioMotionClonePresetId);
        return true;
    }

    private float GetAnalyzerViewportWidth()
    {
        var width = (float)previewCanvas.ActualWidth;
        if (width > 1f)
        {
            var dpiScale = MathF.Max(1f, previewCanvas.Dpi / 96f);
            return width * dpiScale;
        }

        var rasterScale = (float)(XamlRoot?.RasterizationScale ?? 1d);
        return 1280f * MathF.Max(1f, rasterScale);
    }

    private async void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (!await CanLeaveAsync().ConfigureAwait(true))
        {
            return;
        }

        navigationCoordinator.ReturnToVisualizer(ResolveReturnPresetId(), reloadPresets: true);
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        await SaveCurrentPresetAsync().ConfigureAwait(true);
    }

    private async void OnCloneClicked(object sender, RoutedEventArgs e)
    {
        if (selectedPreset is null)
        {
            return;
        }

        if (hasPendingChanges
            && !await ConfirmPendingChangesBeforePresetSwitchAsync().ConfigureAwait(true))
        {
            return;
        }

        var displaySource = VisualizerPresetPaletteEditor.ApplyDisplayName(selectedPreset, ResolveDisplayName(selectedPreset));
        var clonePreset = VisualizerPresetPaletteEditor.CreateClonedPreset(displaySource, rawPresetsById.Values, builtInNameOverrides);
        rawPresetsById[clonePreset.PresetId] = clonePreset;
        transientClonePresetId = clonePreset.PresetId;
        returnPresetId = clonePreset.PresetId;
        RebuildPresetItems();
        LoadPreset(clonePreset.PresetId, resetDirty: false);
        hasPendingChanges = true;
        RefreshEditorState();
        await StartPreviewAsync().ConfigureAwait(true);
        statusText.Text = $"Novo preset pronto - {ResolveDisplayName(clonePreset)}";
    }

    private async void OnPresetListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressUiEvents || presetList.SelectedValue is not string presetId)
        {
            return;
        }

        await SelectPresetAsync(presetId).ConfigureAwait(true);
    }

    private async void OnCompactPresetPickerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressUiEvents || compactPresetPicker.SelectedValue is not string presetId)
        {
            return;
        }

        await SelectPresetAsync(presetId).ConfigureAwait(true);
    }

    private async Task SelectPresetAsync(string presetId)
    {
        if (selectedPreset is not null
            && string.Equals(selectedPreset.PresetId, presetId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (hasPendingChanges
            && !await ConfirmPendingChangesBeforePresetSwitchAsync().ConfigureAwait(true))
        {
            RefreshEditorState();
            return;
        }

        LoadPreset(presetId, resetDirty: true);
        await StartPreviewAsync().ConfigureAwait(true);
    }

    private void OnNameTextChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressUiEvents)
        {
            return;
        }

        var nextName = nameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nextName))
        {
            nextName = ResolveDisplayName(selectedPreset);
        }

        if (string.Equals(workingDisplayName, nextName, StringComparison.Ordinal))
        {
            return;
        }

        workingDisplayName = nextName;
        MarkDirty();
    }

    private void OnGradientStopSelected(int stopIndex)
    {
        selectedStopIndex = Math.Clamp(stopIndex, 0, Math.Max(0, workingStops.Count - 1));
        RefreshEditorState();
    }

    private void OnGradientTrackInvoked(float offset)
    {
        var result = VisualizerPresetPaletteEditor.AddStop(workingStops, offset);
        workingStops = result.Stops;
        selectedStopIndex = result.SelectedIndex;
        MarkDirty();
    }

    private void OnSwatchClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RgbaColor swatchColor })
        {
            return;
        }

        workingStops = VisualizerPresetPaletteEditor.UpdateStopColor(workingStops, selectedStopIndex, swatchColor);
        MarkDirty();
    }

    private void OnPositionSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressUiEvents)
        {
            return;
        }

        var result = VisualizerPresetPaletteEditor.UpdateStopOffset(
            workingStops,
            selectedStopIndex,
            (float)(e.NewValue / 100d));
        workingStops = result.Stops;
        selectedStopIndex = result.SelectedIndex;
        MarkDirty();
    }

    private void OnRemoveStopClicked(object sender, RoutedEventArgs e)
    {
        var result = VisualizerPresetPaletteEditor.RemoveStop(workingStops, selectedStopIndex);
        workingStops = result.Stops;
        selectedStopIndex = result.SelectedIndex;
        MarkDirty();
    }

    private void OnQuickPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GradientPaletteTemplate template })
        {
            return;
        }

        workingStops = VisualizerPresetPaletteEditor.ReplaceStops(workingStops, template.Stops);
        workingPaletteName = template.Label;
        selectedStopIndex = ResolveInitialStopIndex(workingStops);
        MarkDirty();
    }

    private void OnHubPreviewModeClicked(object sender, RoutedEventArgs e)
    {
        if (previewSurfaceMode == PreviewSurfaceMode.Hub75)
        {
            return;
        }

        previewSurfaceMode = PreviewSurfaceMode.Hub75;
        ConfigureHubPreviewOutputs();
        RefreshEditorState();
        previewCanvas.Invalidate();
    }

    private void OnCanvasPreviewModeClicked(object sender, RoutedEventArgs e)
    {
        if (previewSurfaceMode == PreviewSurfaceMode.Canvas)
        {
            return;
        }

        previewSurfaceMode = PreviewSurfaceMode.Canvas;
        ConfigureHubPreviewOutputs();
        RefreshEditorState();
        previewCanvas.Invalidate();
    }

    private void OnPreviewCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var now = Stopwatch.GetTimestamp();
        var deltaSeconds = lastRenderQpc == 0
            ? 1f / 60f
            : (float)(now - lastRenderQpc) / Stopwatch.Frequency;
        lastRenderQpc = now;

        if (!TryBuildPreviewPreset(out var previewPreset))
        {
            args.DrawingSession.Clear(Color.FromArgb(255, 0, 0, 0));
            return;
        }

        if (previewSurfaceMode == PreviewSurfaceMode.Hub75)
        {
            Hub75PreviewHelper.DrawFrame(
                args.DrawingSession,
                (float)sender.ActualWidth,
                (float)sender.ActualHeight,
                simulatorLedOutput.GetFrameSnapshot());
            return;
        }

        var frame = pipelineCoordinator.LatestFrame;
        if (frame is null)
        {
            args.DrawingSession.Clear(Color.FromArgb(255, 0, 0, 0));
            return;
        }

        visualizer.Render(
            args.DrawingSession,
            (float)sender.ActualWidth,
            (float)sender.ActualHeight,
            frame,
            previewPreset,
            deltaSeconds);
    }

    private void OnPreviewCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = (float)e.NewSize.Width;
        if (width <= 1f || MathF.Abs(width - lastPreviewViewportWidth) < 1f)
        {
            return;
        }

        lastPreviewViewportWidth = width;
        RebuildPreviewAnalyzer();
    }

    private void UpdateAdaptiveLayout(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 1d || viewportHeight <= 1d)
        {
            return;
        }

        isCompactLayout = viewportWidth < CompactLayoutBreakpoint;
        ApplyAdaptiveLayout();
        var availableHeight = Math.Max(0d, viewportHeight - 180d);
        presetList.MaxHeight = Math.Clamp(availableHeight, MinimumPresetListHeight, MaximumPresetListHeight);
        previewSurface.Height = isCompactLayout
            ? Math.Clamp(availableHeight * 0.38d, MinimumPreviewHeight, 420d)
            : Math.Clamp(availableHeight * 0.56d, MinimumPreviewHeight, MaximumPreviewHeight);
    }

    private void MarkDirty()
    {
        hasPendingChanges = true;
        RefreshEditorState();
        previewCanvas.Invalidate();
    }

    private async Task<bool> SaveCurrentPresetAsync()
    {
        try
        {
            if (selectedPreset is null)
            {
                return false;
            }

            var normalizedDisplayName = string.IsNullOrWhiteSpace(workingDisplayName)
                ? ResolveDisplayName(selectedPreset)
                : workingDisplayName.Trim();
            var savePresetId = selectedPreset.PresetId;

            if (VisualizerPresetPaletteEditor.IsBuiltInPreset(selectedPreset))
            {
                var mutableOverrides = new Dictionary<string, string>(builtInNameOverrides, StringComparer.OrdinalIgnoreCase);
                var defaultName = VisualizerPresetPaletteEditor.ResolveDefaultBuiltInName(selectedPreset.PresetId);
                if (string.Equals(normalizedDisplayName, defaultName, StringComparison.OrdinalIgnoreCase))
                {
                    mutableOverrides.Remove(selectedPreset.PresetId);
                }
                else
                {
                    mutableOverrides[selectedPreset.PresetId] = normalizedDisplayName;
                }

                await builtInNameOverrideStore.SaveAsync(mutableOverrides).ConfigureAwait(true);
                builtInNameOverrides = mutableOverrides;

                if (HasPaletteChanged())
                {
                    var renamedEditablePreset = VisualizerPresetPaletteEditor.ApplyDisplayName(
                        editablePreset,
                        VisualizerPresetPaletteEditor.ResolveEditablePresetNameFromDisplayName(normalizedDisplayName));
                    var savedEditablePreset = VisualizerPresetPaletteEditor.ApplyPalette(
                        renamedEditablePreset,
                        workingPaletteName,
                        workingStops);
                    rawPresetsById[savedEditablePreset.PresetId] = savedEditablePreset;
                    savePresetId = savedEditablePreset.PresetId;
                }
            }
            else
            {
                var renamedPreset = VisualizerPresetPaletteEditor.ApplyDisplayName(editablePreset, normalizedDisplayName);
                var savedPreset = VisualizerPresetPaletteEditor.ApplyPalette(
                    renamedPreset,
                    workingPaletteName,
                    workingStops);
                rawPresetsById[savedPreset.PresetId] = savedPreset;
                savePresetId = savedPreset.PresetId;
            }

            await presetRepository.SaveAllAsync(rawPresetsById.Values).ConfigureAwait(true);
            transientClonePresetId = null;
            returnPresetId = savePresetId;
            pendingPresetId = savePresetId;
            await LoadSessionAsync().ConfigureAwait(true);
            await StartPreviewAsync().ConfigureAwait(true);
            statusText.Text = $"Studio salvo: {ResolveDisplayName(selectedPreset)}.";
            return true;
        }
        catch (Exception ex)
        {
            statusText.Text = $"Falha ao salvar no Studio: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> ConfirmPendingChangesBeforePresetSwitchAsync()
    {
        var decision = await PromptPendingChangesAsync(
                "Salvar antes de trocar?",
                "Voce tem alteracoes pendentes. Salve, descarte ou continue editando antes de trocar de visualizacao.")
            .ConfigureAwait(true);

        if (decision == PendingChangesDecision.ContinueEditing)
        {
            return false;
        }

        if (decision == PendingChangesDecision.Save)
        {
            return await SaveCurrentPresetAsync().ConfigureAwait(true);
        }

        DiscardTransientCloneIfNeeded();
        hasPendingChanges = false;
        return true;
    }

    private async Task<bool> ConfirmPendingChangesAsync(string title, string message)
    {
        if (!hasPendingChanges)
        {
            return true;
        }

        var decision = await PromptPendingChangesAsync(title, message).ConfigureAwait(true);
        if (decision == PendingChangesDecision.ContinueEditing)
        {
            return false;
        }

        if (decision == PendingChangesDecision.Save)
        {
            return await SaveCurrentPresetAsync().ConfigureAwait(true);
        }

        DiscardTransientCloneIfNeeded();
        hasPendingChanges = false;
        return true;
    }

    private async Task<PendingChangesDecision> PromptPendingChangesAsync(string title, string message)
    {
        if (XamlRoot is null)
        {
            return PendingChangesDecision.Discard;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = "Salvar",
            SecondaryButtonText = "Descartar",
            CloseButtonText = "Continuar editando",
            DefaultButton = ContentDialogButton.Primary,
        };

        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => PendingChangesDecision.Save,
            ContentDialogResult.Secondary => PendingChangesDecision.Discard,
            _ => PendingChangesDecision.ContinueEditing,
        };
    }

    private void DiscardTransientCloneIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(transientClonePresetId))
        {
            return;
        }

        rawPresetsById.Remove(transientClonePresetId);
        if (string.Equals(returnPresetId, transientClonePresetId, StringComparison.OrdinalIgnoreCase))
        {
            returnPresetId = AudioMotionClonePresetId;
        }

        if (selectedPreset is not null
            && string.Equals(selectedPreset.PresetId, transientClonePresetId, StringComparison.OrdinalIgnoreCase))
        {
            var fallbackPresetId = rawPresetsById.ContainsKey(AudioMotionClonePresetId)
                ? AudioMotionClonePresetId
                : rawPresetsById.Keys.FirstOrDefault() ?? AudioMotionClonePresetId;
            transientClonePresetId = null;
            RebuildPresetItems();
            LoadPreset(fallbackPresetId, resetDirty: true);
            return;
        }

        transientClonePresetId = null;
        RebuildPresetItems();
    }

    private string ResolveDisplayName(PresetDefinition preset)
        => VisualizerPresetPaletteEditor.ResolveDisplayName(preset, builtInNameOverrides);

    private string BuildPresetListLabelValue(PresetDefinition preset)
    {
        var displayName = ResolveDisplayName(preset);
        return string.Equals(returnPresetId, preset.PresetId, StringComparison.OrdinalIgnoreCase)
            ? $"{displayName} - volta"
            : displayName;
    }

    private string ResolveStudioStatus()
    {
        var targetName = ResolvePresetListActiveName();
        return hasPendingChanges
            ? $"Pendente - volta com {targetName}"
            : $"Volta com {targetName}";
    }

    private string ResolvePreviewModeHint()
    {
        return previewSurfaceMode == PreviewSurfaceMode.Hub75
            ? "Mesmo preview HUB75 shipping do Visualizador."
            : "Canvas Win2D fiel ao preset em edicao.";
    }

    private RendererHubTransportMode ResolveHubTransportMode(PresetDefinition previewPreset)
        => visualizer.GetCapabilities(previewPreset.RendererId).HubTransportMode;

    private void ConfigureHubPreviewOutputs()
    {
        pipelineCoordinator.ConfigureHubOutputs(
            enableSimulator: previewSurfaceMode == PreviewSurfaceMode.Hub75,
            enableHub75DeviceOutput: false,
            brightness: appSettings.Brightness);
    }

    private void RefreshPreviewModeButtons()
    {
        var selectedBackground = ResolveBrush("BrandAccentBrush", Color.FromArgb(255, 38, 201, 138));
        var selectedForeground = ResolveBrush("AppSurfaceBaseBrush", Color.FromArgb(255, 11, 15, 20));
        var idleBackground = ResolveBrush("AppSurfaceElevatedBrush", Color.FromArgb(255, 30, 34, 42));
        var idleForeground = ResolveBrush("AppTextPrimaryBrush", Color.FromArgb(255, 245, 247, 250));

        ApplyPreviewModeButton(hubPreviewModeButton, previewSurfaceMode == PreviewSurfaceMode.Hub75, selectedBackground, selectedForeground, idleBackground, idleForeground);
        ApplyPreviewModeButton(canvasPreviewModeButton, previewSurfaceMode == PreviewSurfaceMode.Canvas, selectedBackground, selectedForeground, idleBackground, idleForeground);
    }

    private static void ApplyPreviewModeButton(Button button, bool isSelected, Brush selectedBackground, Brush selectedForeground, Brush idleBackground, Brush idleForeground)
    {
        button.Background = isSelected ? selectedBackground : idleBackground;
        button.Foreground = isSelected ? selectedForeground : idleForeground;
    }

    private void ApplyAdaptiveLayout()
    {
        if (contentGrid.ColumnDefinitions.Count < 2)
        {
            return;
        }

        compactPresetStrip.Visibility = isCompactLayout ? Visibility.Visible : Visibility.Collapsed;
        presetRail.Visibility = isCompactLayout ? Visibility.Collapsed : Visibility.Visible;
        contentGrid.ColumnDefinitions[0].Width = isCompactLayout
            ? new GridLength(0d)
            : new GridLength(DefaultPresetListWidth);
        contentGrid.ColumnSpacing = isCompactLayout ? 0d : 24d;

        editorGrid.ColumnDefinitions.Clear();
        editorGrid.RowDefinitions.Clear();
        if (isCompactLayout)
        {
            editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(editorPrimaryColumn, 0);
            Grid.SetRow(editorPrimaryColumn, 0);
            Grid.SetColumn(editorSecondaryColumn, 0);
            Grid.SetRow(editorSecondaryColumn, 1);
        }
        else
        {
            editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25d, GridUnitType.Star) });
            editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320d) });
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(editorPrimaryColumn, 0);
            Grid.SetRow(editorPrimaryColumn, 0);
            Grid.SetColumn(editorSecondaryColumn, 1);
            Grid.SetRow(editorSecondaryColumn, 0);
        }
    }

    private static KeyboardAccelerator BuildAccelerator(Windows.System.VirtualKey key, Windows.System.VirtualKeyModifiers modifiers, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers,
        };
        accelerator.Invoked += handler;
        return accelerator;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private async void OnSaveAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await SaveCurrentPresetAsync().ConfigureAwait(true);
    }

    private async void OnBackAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!await CanLeaveAsync().ConfigureAwait(true))
        {
            return;
        }

        navigationCoordinator.ReturnToVisualizer(ResolveReturnPresetId(), reloadPresets: true);
    }

    private string ResolvePresetListActiveName()
    {
        if (rawPresetsById.TryGetValue(returnPresetId, out var activePresetForReturn))
        {
            return ResolveDisplayName(activePresetForReturn);
        }

        return selectedPreset is null ? "AudioMotion Clone" : ResolveDisplayName(selectedPreset);
    }

    private bool HasPaletteChanged()
    {
        if (editablePreset?.Palette is null)
        {
            return true;
        }

        if (!string.Equals(workingPaletteName, editablePreset.Palette.Name, StringComparison.Ordinal))
        {
            return true;
        }

        var normalizedStops = VisualizerPresetPaletteEditor.NormalizeStops(workingStops);
        var existingStops = VisualizerPresetPaletteEditor.NormalizeStops(editablePreset.Palette.Stops);
        if (normalizedStops.Count != existingStops.Count)
        {
            return true;
        }

        for (var index = 0; index < normalizedStops.Count; index++)
        {
            if (Math.Abs(normalizedStops[index].Offset - existingStops[index].Offset) > 0.0001f
                || !normalizedStops[index].Color.Equals(existingStops[index].Color))
            {
                return true;
            }
        }

        return false;
    }

    private void BuildSwatchGrid(RgbaColor selectedColor)
    {
        swatchGrid.Children.Clear();
        swatchGrid.ColumnDefinitions.Clear();
        swatchGrid.RowDefinitions.Clear();

        const int columns = 5;
        var rowCount = (int)Math.Ceiling(SwatchColors.Count / (double)columns);

        for (var column = 0; column < columns; column++)
        {
            swatchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var row = 0; row < rowCount; row++)
        {
            swatchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < SwatchColors.Count; index++)
        {
            var swatchColor = SwatchColors[index];
            var isSelected = swatchColor.Equals(selectedColor);
            var button = new Button
            {
                Width = 40d,
                Height = 40d,
                Margin = new Thickness(0d, 0d, 8d, 8d),
                Tag = swatchColor,
                Style = ResolveStyle("AppIconButtonStyle"),
                BorderThickness = isSelected ? new Thickness(2d) : new Thickness(1d),
                BorderBrush = new SolidColorBrush(isSelected
                    ? Color.FromArgb(255, 255, 255, 255)
                    : Color.FromArgb(255, 62, 67, 78)),
                Content = new Border
                {
                    CornerRadius = new CornerRadius(8d),
                    Background = new SolidColorBrush(ToColor(swatchColor)),
                },
            };
            button.Click += OnSwatchClicked;

            swatchGrid.Children.Add(button);
            Grid.SetColumn(button, index % columns);
            Grid.SetRow(button, index / columns);
        }
    }

    private void BuildQuickPresetButtons()
    {
        quickPresetButtonsPanel.Children.Clear();

        foreach (var quickPreset in quickGradientPresets)
        {
            var previewStrip = new Border
            {
                Height = 18d,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(9d),
                Background = CreateBrush(quickPreset.Stops),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 74, 80, 92)),
                BorderThickness = new Thickness(1d),
            };

            var label = new TextBlock
            {
                Text = quickPreset.Label,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var contentGrid = new Grid
            {
                ColumnSpacing = 12d,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(136d) },
                    new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) },
                },
            };
            contentGrid.Children.Add(previewStrip);
            contentGrid.Children.Add(label);
            Grid.SetColumn(label, 1);

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0d, 0d, 0d, 6d),
                Tag = quickPreset,
                Style = ResolveStyle("AppChromeButtonStyle"),
                Content = contentGrid,
            };
            button.Click += OnQuickPresetClicked;
            quickPresetButtonsPanel.Children.Add(button);
        }
    }

    private string ResolveReturnPresetId()
    {
        if (!string.IsNullOrWhiteSpace(returnPresetId) && rawPresetsById.ContainsKey(returnPresetId))
        {
            return returnPresetId;
        }

        if (selectedPreset is not null && rawPresetsById.ContainsKey(selectedPreset.PresetId))
        {
            return selectedPreset.PresetId;
        }

        return AudioMotionClonePresetId;
    }

    private static int ResolveInitialStopIndex(IReadOnlyList<PaletteStop> stops)
    {
        if (stops.Count == 0)
        {
            return 0;
        }

        var bestIndex = 0;
        var bestDistance = float.MaxValue;

        for (var index = 0; index < stops.Count; index++)
        {
            var distance = Math.Abs(stops[index].Offset - 0.5f);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static Border CreateSectionBorder(UIElement child)
    {
        return new Border
        {
            Padding = new Thickness(16d),
            CornerRadius = new CornerRadius(14d),
            Background = new SolidColorBrush(Color.FromArgb(255, 48, 50, 58)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 70, 74, 86)),
            BorderThickness = new Thickness(1d),
            Child = child,
        };
    }

    private static LinearGradientBrush CreateBrush(IReadOnlyList<PaletteStop> stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0.5d),
            EndPoint = new Point(1d, 0.5d),
        };

        foreach (var stop in stops)
        {
            brush.GradientStops.Add(new GradientStop
            {
                Offset = stop.Offset,
                Color = ToColor(stop.Color),
            });
        }

        return brush;
    }

    private static Color ToColor(RgbaColor color)
        => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static Style? ResolveStyle(string key)
        => Application.Current.Resources.TryGetValue(key, out var value) ? value as Style : null;

    private static Brush ResolveBrush(string key, Color fallbackColor)
        => Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(fallbackColor);

    private enum PendingChangesDecision
    {
        Save,
        Discard,
        ContinueEditing,
    }

    private enum PreviewSurfaceMode
    {
        Hub75,
        Canvas,
    }

    private sealed record StudioPresetItem(string Id, string Label, bool IsActive)
    {
        public string SortLabel => Label
            .Replace(" - ativo", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" - volta", string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
