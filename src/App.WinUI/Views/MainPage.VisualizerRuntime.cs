using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Microsoft.UI.Dispatching;
using Visual.Win2D.Engine;

namespace App.WinUI.Views; // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-visualizador-fluido-com-debounce
// DOCS: docs/wiki/modules/settings-presets-persistence.md#modulo-settings-presets-e-persistencia

internal sealed class VisualizerRuntimeApplyState
{
    public VisualizerRuntimeSettings Pending { get; private set; } = VisualizerRuntimeSettings.Default;

    public VisualizerRuntimeSettings Applied { get; private set; } = VisualizerRuntimeSettings.Default;

    public bool HasApplied { get; private set; }

    public void SetPending(VisualizerRuntimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Pending = settings;
    }

    public void MarkApplied(VisualizerRuntimeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Pending = settings;
        Applied = settings;
        HasApplied = true;
    }

    public bool ShouldApply(bool forceRebuild = false)
        => forceRebuild || !HasApplied || !AreEquivalent(Pending, Applied);

    internal static bool AreEquivalent(VisualizerRuntimeSettings left, VisualizerRuntimeSettings right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(left.ActivePresetId, right.ActivePresetId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.SelectedRendererId, right.SelectedRendererId, StringComparison.OrdinalIgnoreCase)
            && left.LinearBoost == right.LinearBoost
            && left.DisplayBandCount == right.DisplayBandCount
            && left.FftSize == right.FftSize
            && left.FftSmoothing == right.FftSmoothing
            && left.WeightingFilter == right.WeightingFilter
            && left.FrequencyScale == right.FrequencyScale
            && left.FrequencyMinHz == right.FrequencyMinHz
            && left.FrequencyMaxHz == right.FrequencyMaxHz;
    }
}

public partial class MainPage
{
    private const int VisualizerRuntimeDebounceMs = 150;

    private readonly VisualizerRuntimeApplyState visualizerRuntimeState = new();

    private DispatcherQueueTimer? visualizerRuntimeDebounceTimer;

    private enum AnalyzerRebuildOutcome
    {
        TargetApplied,
        FallbackApplied,
        PreviousPreserved,
    }

    private void EnsureVisualizerRuntimeDebounceTimer()
    {
        if (visualizerRuntimeDebounceTimer is not null)
        {
            return;
        }

        visualizerRuntimeDebounceTimer = DispatcherQueue.CreateTimer();
        visualizerRuntimeDebounceTimer.Interval = TimeSpan.FromMilliseconds(VisualizerRuntimeDebounceMs);
        visualizerRuntimeDebounceTimer.Tick += (_, _) =>
        {
            visualizerRuntimeDebounceTimer?.Stop();
            _ = ApplyPendingVisualizerRuntime(
                forceRebuild: false,
                persistSettings: true,
                refreshOutputs: true,
                updateStatusOnFailure: true);
        };
    }

    private void StopVisualizerRuntimeDebounceTimer()
        => visualizerRuntimeDebounceTimer?.Stop();

    private void StagePendingVisualizerRuntime()
        => visualizerRuntimeState.SetPending(BuildVisualizerRuntimeSettings());

    private void ScheduleVisualizerRuntimeApply()
    {
        if (ShouldIgnoreUiMutation())
        {
            return;
        }

        StagePendingVisualizerRuntime();
        EnsureVisualizerRuntimeDebounceTimer();
        visualizerRuntimeDebounceTimer!.Stop();
        visualizerRuntimeDebounceTimer.Start();
    }

    private AnalyzerRebuildOutcome? ApplyPendingVisualizerRuntimeImmediately(
        bool forceRebuild,
        bool persistSettings,
        bool refreshOutputs,
        bool updateStatusOnFailure)
    {
        StopVisualizerRuntimeDebounceTimer();
        return ApplyPendingVisualizerRuntime(
            forceRebuild,
            persistSettings,
            refreshOutputs,
            updateStatusOnFailure);
    }

    private AnalyzerRebuildOutcome? ApplyPendingVisualizerRuntime(
        bool forceRebuild,
        bool persistSettings,
        bool refreshOutputs,
        bool updateStatusOnFailure)
    {
        StagePendingVisualizerRuntime();
        if (!visualizerRuntimeState.ShouldApply(forceRebuild))
        {
            return null;
        }

        var targetSettings = visualizerRuntimeState.Pending;
        var outcome = TryRebuildAnalyzer(targetSettings, updateStatusOnFailure);

        if (outcome == AnalyzerRebuildOutcome.TargetApplied)
        {
            visualizerRuntimeState.MarkApplied(targetSettings);
        }
        else if (outcome == AnalyzerRebuildOutcome.FallbackApplied || !visualizerRuntimeState.HasApplied)
        {
            visualizerRuntimeState.MarkApplied(BuildFallbackVisualizerRuntimeSettings(targetSettings));
        }

        if (refreshOutputs)
        {
            SyncHubTransportMode();
            PumpHubFrameOutput(force: true);
            MainCanvas.Invalidate();

            if (ShouldShowHubPreview())
            {
                InvalidateHubPreviews();
            }
        }

        if (persistSettings && outcome == AnalyzerRebuildOutcome.TargetApplied)
        {
            PersistCurrentVisualizerSettings();
        }

        return outcome;
    }

    private PresetDefinition BuildRuntimePreset(VisualizerRuntimeSettings runtimeSettings)
    {
        ArgumentNullException.ThrowIfNull(runtimeSettings);

        return BuildSafeAnalyzerPreset(
            ResolveActivePreset(runtimeSettings.ActivePresetId),
            runtimeSettings.SelectedRendererId,
            runtimeSettings.DisplayBandCount,
            GetAvailableRendererIds(),
            AudioMotionClonePresetId);
    }

    private VisualizerRuntimeSettings GetRenderRuntimeSettings()
        => visualizerRuntimeState.HasApplied
            ? visualizerRuntimeState.Applied
            : visualizerRuntimeState.Pending;

    private static VisualizerRuntimeSettings BuildFallbackVisualizerRuntimeSettings(VisualizerRuntimeSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return VisualizerRuntimeSettings.From(new AppSettings
        {
            ActivePresetId = AudioMotionClonePresetId,
            SelectedRendererId = RendererIds.AudioMotionClone,
            LinearBoost = source.LinearBoost,
            BarCount = source.DisplayBandCount,
            FftSize = VisualizerRuntimeDefaults.DefaultFftSize,
            FftSmoothing = source.FftSmoothing,
            WeightingFilter = source.WeightingFilter,
            FrequencyScale = source.FrequencyScale,
            FrequencyMinHz = source.FrequencyMinHz,
            FrequencyMaxHz = source.FrequencyMaxHz,
        });
    }
}
