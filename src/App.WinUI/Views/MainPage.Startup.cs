using Analyzer.Dsp.Analysis;
using App.WinUI.Services;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Views;

public partial class MainPage
{
    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-startup-estavel-e-observabilidade-real
    private readonly MainPageUiBootstrapGuard uiBootstrapGuard = new();

    private static readonly Dictionary<string, PresetDefinition> BuiltInPresets =
        DefaultPresets
            .Create()
            .ToDictionary(preset => preset.PresetId, StringComparer.OrdinalIgnoreCase);

    internal bool IsInitializingUi => uiBootstrapGuard.IsActive;

    internal static string ResolveSupportedRendererId(
        string? configuredRendererId,
        string? fallbackRendererId,
        IEnumerable<string> availableRendererIds)
    {
        ArgumentNullException.ThrowIfNull(availableRendererIds);

        var availableIds = availableRendererIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedFallback = string.IsNullOrWhiteSpace(fallbackRendererId)
            ? RendererIds.AudioMotionClone
            : fallbackRendererId;
        var preferred = string.IsNullOrWhiteSpace(configuredRendererId)
            ? normalizedFallback
            : configuredRendererId;

        if (availableIds.Count == 0)
        {
            return preferred;
        }

        if (availableIds.Contains(preferred))
        {
            return preferred;
        }

        if (availableIds.Contains(normalizedFallback))
        {
            return normalizedFallback;
        }

        if (availableIds.Contains(RendererIds.AudioMotionClone))
        {
            return RendererIds.AudioMotionClone;
        }

        return availableIds.First();
    }

    internal static PresetDefinition BuildSafeAnalyzerPreset(
        PresetDefinition? preset,
        string? selectedRendererId,
        int displayBandCount,
        IEnumerable<string> availableRendererIds,
        string? fallbackPresetId = null)
    {
        var fallback = ResolveBuiltInFallbackPreset(fallbackPresetId);
        var source = ResolvePresetForRuntime(preset, fallback);

        return new PresetDefinition
        {
            SchemaVersion = source.SchemaVersion > 0 ? source.SchemaVersion : fallback.SchemaVersion,
            PresetId = string.IsNullOrWhiteSpace(source.PresetId) ? fallback.PresetId : source.PresetId,
            Name = string.IsNullOrWhiteSpace(source.Name) ? fallback.Name : source.Name,
            RendererId = ResolveSupportedRendererId(
                selectedRendererId,
                source.RendererId,
                availableRendererIds),
            DisplayBandCount = Math.Clamp(
                displayBandCount > 0
                    ? displayBandCount
                    : (source.DisplayBandCount > 0 ? source.DisplayBandCount : fallback.DisplayBandCount),
                8,
                128),
            ScaleMode = source.ScaleMode,
            FpsTarget = source.FpsTarget > 0 ? source.FpsTarget : fallback.FpsTarget,
            Layout = source.Layout,
            EnableGlow = source.EnableGlow,
            RendererParameters = CloneRendererParameters(source.RendererParameters, fallback.RendererParameters),
            Palette = ClonePalette(source.Palette, fallback.Palette),
        };
    }

    private void ApplyLoadedStateToUi(out bool shouldPersistNormalizedState)
    {
        using var _ = uiBootstrapGuard.Enter();

        appWindow = GetAppWindow();
        EnsureVisibleUiState();
        RestoreWindowSize();
        BuildPresetNavigationOrder();
        PopulateRendererCombo();
        PopulateContentModeCombo();
        PopulateGifScaleModeCombo();
        PopulateWeightingFilterCombo();
        PopulateFrequencyScaleCombo();
        PopulateFrequencyRangeCombos();

        activePreset = ResolveActivePreset(appSettings.ActivePresetId);
        currentPresetId = activePreset.PresetId;
        selectedRendererId = ResolveSelectedRendererId(appSettings.SelectedRendererId, activePreset.RendererId);
        shouldPersistNormalizedState =
            !string.Equals(appSettings.ActivePresetId, currentPresetId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(appSettings.SelectedRendererId, selectedRendererId, StringComparison.OrdinalIgnoreCase);

        ApplyVisualizerRuntimeSettings(settingsDomainService.GetVisualizerRuntimeSettings(appSettings));
        UpdateCurrentPresetIndex();
        SelectComboOption(RendererCombo, selectedRendererId);

        suppressFftSmoothingChanged = true;
        FftSmoothingSlider.Value = fftSmoothing;
        suppressFftSmoothingChanged = false;

        suppressWeightingFilterChanged = true;
        SelectComboOption(WeightingFilterCombo, ToWeightingFilterId(weightingFilter));
        suppressWeightingFilterChanged = false;

        suppressFrequencyScaleChanged = true;
        SelectComboOption(FrequencyScaleCombo, frequencyScale.ToString());
        suppressFrequencyScaleChanged = false;

        suppressContentModeChanged = true;
        SelectComboOption(ContentModeCombo, GifContentSourceMode.Audio.ToString());
        suppressContentModeChanged = false;

        suppressGifScaleModeChanged = true;
        SelectComboOption(GifScaleModeCombo, GifScaleMode.Fit.ToString());
        suppressGifScaleModeChanged = false;

        UpdateFftSmoothingText();
        UpdateFrequencyScaleToolTip();
        UpdateFrequencyRangeCombos();
        ApplyRendererControlState();
        UpdateGifControlsVisibility();
        UpdateGifTransportState();
        UpdateGifLoadingState(false);
        UpdateSettingsPaneVisualState();

        viewModel.CurrentPresetId = currentPresetId;
        viewModel.SelectedRendererId = selectedRendererId;

        hub75ModeEnabled = appSettings.Hub75PreviewEnabled;
        Hub75Toggle.IsOn = hub75ModeEnabled;
        UpdateHubPreviewVisibility();

        lastCloneViewportWidth = GetAnalyzerViewportWidth();
        ApplyPendingVisualizerRuntime(
            forceRebuild: true,
            persistSettings: false,
            refreshOutputs: false,
            updateStatusOnFailure: false);
    }

    private bool ShouldIgnoreUiMutation() => uiBootstrapGuard.IsActive;

    private AnalyzerRebuildOutcome TryRebuildAnalyzer(
        VisualizerRuntimeSettings targetSettings,
        bool updateStatusOnFailure)
    {
        App.RecordStartupBreadcrumb("MainPage.RebuildAnalyzer");

        var previousAnalyzer = Volatile.Read(ref analyzer);
        if (TryCreateAnalyzer(
                BuildRuntimePreset(targetSettings),
                targetSettings,
                out var rebuiltAnalyzer,
                out _))
        {
            Volatile.Write(ref analyzer, rebuiltAnalyzer);
            pipelineCoordinator.SetAnalyzer(rebuiltAnalyzer);
            return AnalyzerRebuildOutcome.TargetApplied;
        }

        var fallbackRuntimeSettings = BuildFallbackVisualizerRuntimeSettings(targetSettings);
        if (TryCreateAnalyzer(
                BuildRuntimePreset(fallbackRuntimeSettings),
                fallbackRuntimeSettings,
                out var fallbackAnalyzer,
                out var fallbackException))
        {
            Volatile.Write(ref analyzer, fallbackAnalyzer);
            pipelineCoordinator.SetAnalyzer(fallbackAnalyzer);
            return AnalyzerRebuildOutcome.FallbackApplied;
        }

        pipelineCoordinator.SetAnalyzer(previousAnalyzer);
        if (fallbackException is not null)
        {
            App.ReportStartupFailure("MainPage.RebuildAnalyzer fallback failed", fallbackException);
        }

        if (updateStatusOnFailure)
        {
            StatusText.Text = "Falha ao reconfigurar o visualizador. Usando preset seguro.";
        }

        return AnalyzerRebuildOutcome.PreviousPreserved;
    }

    private bool TryCreateAnalyzer(
        PresetDefinition preset,
        VisualizerRuntimeSettings runtimeSettings,
        out SpectrumAnalyzer analyzerInstance,
        out Exception? exception)
    {
        try
        {
            analyzerInstance = CreateAnalyzer(preset, runtimeSettings);
            exception = null;
            return true;
        }
        catch (Exception ex)
        {
            App.ReportStartupFailure("MainPage.RebuildAnalyzer failed", ex);
            analyzerInstance = null!;
            exception = ex;
            return false;
        }
    }

    private string[] GetAvailableRendererIds()
        => visualizer.Renderers.Select(renderer => renderer.RendererId).ToArray();

    private static PresetDefinition ResolvePresetForRuntime(PresetDefinition? preset, PresetDefinition fallback)
    {
        if (preset is null)
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(preset.PresetId))
        {
            return fallback;
        }

        return preset;
    }

    private static PresetDefinition ResolveBuiltInFallbackPreset(string? presetId)
    {
        if (!string.IsNullOrWhiteSpace(presetId) && BuiltInPresets.TryGetValue(presetId, out var preset))
        {
            return preset;
        }

        return BuiltInPresets.TryGetValue(AudioMotionClonePresetId, out var clonePreset)
            ? clonePreset
            : DefaultPresets.Create()[0];
    }

    private static Dictionary<string, float> CloneRendererParameters(
        IReadOnlyDictionary<string, float>? parameters,
        IReadOnlyDictionary<string, float>? fallback)
    {
        if (parameters is { Count: > 0 })
        {
            return new Dictionary<string, float>(parameters, StringComparer.OrdinalIgnoreCase);
        }

        if (fallback is { Count: > 0 })
        {
            return new Dictionary<string, float>(fallback, StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    private static GradientPalette ClonePalette(
        GradientPalette? palette,
        GradientPalette? fallback)
    {
        var source = palette is { Stops.Count: > 0 }
            ? palette
            : fallback ?? ResolveBuiltInFallbackPreset(AudioMotionClonePresetId).Palette;

        return new GradientPalette
        {
            Stops = source.Stops.Select(stop => new PaletteStop
            {
                Offset = stop.Offset,
                Color = stop.Color,
            }).ToArray(),
        };
    }
}
