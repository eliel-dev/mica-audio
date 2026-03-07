using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;

namespace App.WinUI.Views;

public partial class MainPage
{
    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-fase-6-core-pipeline-e-borda-de-integracao
    // DOCS: docs/wiki/modules/settings-presets-persistence.md#atualizacao-2026-03-fase-6-runtime-profile-e-persistencia
    private VisualizerRuntimeSettings BuildVisualizerRuntimeSettings()
    {
        return VisualizerRuntimeSettings.From(new AppSettings
        {
            ActivePresetId = currentPresetId,
            SelectedRendererId = selectedRendererId,
            LinearBoost = linearBoost,
            BarCount = displayBandCount,
            FftSize = fftSize,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            FrequencyScale = frequencyScale,
            FrequencyMinHz = frequencyMinHz,
            FrequencyMaxHz = frequencyMaxHz,
        });
    }

    private void ApplyVisualizerRuntimeSettings(VisualizerRuntimeSettings settings)
    {
        linearBoost = settings.LinearBoost;
        displayBandCount = settings.DisplayBandCount;
        fftSize = settings.FftSize;
        fftSmoothing = settings.FftSmoothing;
        weightingFilter = settings.WeightingFilter;
        frequencyScale = settings.FrequencyScale;
        frequencyMinHz = settings.FrequencyMinHz;
        frequencyMaxHz = settings.FrequencyMaxHz;

        viewModel.LinearBoost = linearBoost;
        viewModel.BarCount = displayBandCount;
        viewModel.FftSize = fftSize;
        viewModel.FftSmoothing = fftSmoothing;
        viewModel.WeightingFilter = weightingFilter;
        viewModel.FrequencyScale = frequencyScale;
        viewModel.FrequencyMinHz = frequencyMinHz;
        viewModel.FrequencyMaxHz = frequencyMaxHz;
    }

    private void PersistCurrentVisualizerSettings(bool includePreview = false, bool includeWindowSize = false)
    {
        if (ShouldIgnoreUiMutation())
        {
            return;
        }

        appSettings = settingsDomainService.Copy(appSettings, builder =>
        {
            builder.SetVisualizerRuntimeSettings(BuildVisualizerRuntimeSettings());

            if (includePreview)
            {
                builder.SetHub75PreviewEnabled(hubPreviewEnabled);
            }

            if (!includeWindowSize)
            {
                return;
            }

            appWindow ??= GetAppWindow();
            if (appWindow is not null)
            {
                builder.SetWindowSize(appWindow.Size.Width, appWindow.Size.Height);
            }
        });
    }
}
