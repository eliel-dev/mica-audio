using App.WinUI.Services.Gif;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Views;

public partial class MainPage
{
    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-menu-de-configuracao-fluent-2
    private void OnRendererSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShouldIgnoreUiMutation())
        {
            return;
        }

        if (IsBuiltInClonePresetActive())
        {
            selectedRendererId = RendererIds.AudioMotionClone;
            viewModel.SelectedRendererId = selectedRendererId;
            ApplyRendererControlState();
            return;
        }

        if (RendererCombo.SelectedItem is not ComboOption option)
        {
            return;
        }

        selectedRendererId = option.Id;
        viewModel.SelectedRendererId = selectedRendererId;
        ApplyRendererControlState();
        lastCloneViewportWidth = GetAnalyzerViewportWidth();
        _ = ApplyPendingVisualizerRuntimeImmediately(
            forceRebuild: false,
            persistSettings: true,
            refreshOutputs: true,
            updateStatusOnFailure: true);
    }

    private async void OnContentModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressContentModeChanged)
        {
            return;
        }

        if (ContentModeCombo.SelectedItem is not ComboOption option
            || !Enum.TryParse<GifContentSourceMode>(option.Id, ignoreCase: true, out var mode))
        {
            return;
        }

        await SwitchContentModeAsync(mode).ConfigureAwait(true);
    }

    private void OnResetSettingsClicked(object sender, RoutedEventArgs e)
    {
        ApplyVisualizerRuntimeSettings(settingsDomainService.GetVisualizerRuntimeSettings(new AppSettings()));

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
        UpdateFftSmoothingText();
        ApplyBandCountBounds();
        UpdateCurrentPresetIndex();
        UpdateSettingsPaneVisualState();
        lastCloneViewportWidth = GetAnalyzerViewportWidth();

        var outcome = ApplyPendingVisualizerRuntimeImmediately(
            forceRebuild: false,
            persistSettings: true,
            refreshOutputs: true,
            updateStatusOnFailure: true);

        if (outcome == AnalyzerRebuildOutcome.TargetApplied)
        {
            StatusText.Text = "Configurações restauradas.";
        }
    }

    private void OnBarCountChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        var capabilities = GetActiveRendererCapabilities();
        if (ShouldIgnoreUiMutation()
            || suppressBarCountChanged
            || !capabilities.Controls.SupportsBarCount
            || capabilities.BarCountMode == RendererBarCountMode.Fixed)
        {
            return;
        }

        var minBands = Math.Max(8, activePreset.DisplayBandCount);
        displayBandCount = (int)Math.Clamp(Math.Round(e.NewValue), minBands, 128);
        ApplyBandCountBounds();
        ScheduleVisualizerRuntimeApply();
    }

    private void OnFftSmoothingChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (ShouldIgnoreUiMutation() || suppressFftSmoothingChanged)
        {
            return;
        }

        fftSmoothing = CoerceFftSmoothing((float)e.NewValue);
        UpdateFftSmoothingText();
        ScheduleVisualizerRuntimeApply();
    }

    private void OnWeightingFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShouldIgnoreUiMutation() || suppressWeightingFilterChanged)
        {
            return;
        }

        if (WeightingFilterCombo.SelectedItem is not ComboOption option)
        {
            return;
        }

        weightingFilter = ParseWeightingFilterId(option.Id);
        ScheduleVisualizerRuntimeApply();
    }

    private void OnFrequencyScaleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShouldIgnoreUiMutation() || suppressFrequencyScaleChanged)
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
        ScheduleVisualizerRuntimeApply();
    }

    private void OnFrequencyMinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShouldIgnoreUiMutation() || suppressFrequencyMinChanged)
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
        ScheduleVisualizerRuntimeApply();
    }

    private void OnFrequencyMaxChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShouldIgnoreUiMutation() || suppressFrequencyMaxChanged)
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
        ScheduleVisualizerRuntimeApply();
    }

    private void PopulateRendererCombo()
    {
        RendererCombo.ItemsSource = visualizer.Renderers
            .OrderBy(static renderer => renderer.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(static renderer => new ComboOption(renderer.RendererId, renderer.DisplayName))
            .ToList();
    }

    private void PopulateContentModeCombo()
    {
        ContentModeCombo.ItemsSource = new List<ComboOption>
        {
            new(GifContentSourceMode.Audio.ToString(), "Áudio", "Usa a captura ao vivo como fonte do visualizador."),
            new(GifContentSourceMode.Gif.ToString(), "GIF", "Reproduz os frames carregados no visualizador e no preview HUB75."),
        };
        ContentModeCombo.IsEnabled = true;
    }

    private void PopulateWeightingFilterCombo()
    {
        WeightingFilterCombo.ItemsSource = new List<ComboOption>
        {
            new("OFF", "Off", "Não aplica ponderação psicoacústica."),
            new("A", "A", "Atenua graves e agudos extremos, aproximando a audição em nível moderado."),
            new("B", "B", "Intermediário entre A e C, com menos atenuação nos graves."),
            new("C", "C", "Curva quase plana, com leve atenuação de graves profundos."),
            new("D", "D", "Curva usada historicamente para ruído de aeronaves."),
            new("468", "468", "Ponderação ITU-R 468, enfatiza a faixa mais sensível para ruído percebido."),
        };
    }

    private void PopulateFrequencyScaleCombo()
    {
        FrequencyScaleCombo.ItemsSource = new List<ComboOption>
        {
            new(nameof(FrequencyScale.Logarithmic), "Logarítmica", "Distribui as barras de forma exponencial. Realça graves e organiza agudos com menos barras."),
            new(nameof(FrequencyScale.Mel), "Mel", "Escala perceptual baseada na audição humana. Traz leitura mais natural nas médias frequências."),
            new(nameof(FrequencyScale.Bark), "Bark", "Escala psicoacústica por bandas críticas. Destaca zonas de percepção do ouvido."),
        };
    }

    private void PopulateFrequencyRangeCombos()
    {
        FrequencyMinCombo.ItemsSource = FrequencyMinOptions
            .Select(static value => new ComboOption(FormatFrequencyId(value), FormatFrequencyLabel(value)))
            .ToList();
        FrequencyMaxCombo.ItemsSource = FrequencyMaxOptions
            .Select(static value => new ComboOption(FormatFrequencyId(value), FormatFrequencyLabel(value)))
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
        if (FrequencyScaleCombo.SelectedItem is ComboOption option
            && !string.IsNullOrWhiteSpace(option.ToolTip))
        {
            ToolTipService.SetToolTip(FrequencyScaleCombo, option.ToolTip);
            return;
        }

        ToolTipService.SetToolTip(FrequencyScaleCombo, "Define como as barras são distribuídas no espectro.");
    }

    private void ApplyBandCountBounds()
    {
        var capabilities = GetActiveRendererCapabilities();
        var minBands = Math.Max(8, activePreset.DisplayBandCount);
        var maxBands = 128;
        var barCountLocked = !capabilities.Controls.SupportsBarCount
            || capabilities.BarCountMode == RendererBarCountMode.Fixed;
        var sliderValue = displayBandCount <= 0 ? minBands : displayBandCount;

        if (barCountLocked)
        {
            var fixedCount = Math.Max(1, capabilities.FixedVisualElementCount ?? minBands);
            minBands = fixedCount;
            maxBands = fixedCount;
            sliderValue = fixedCount;
        }
        else
        {
            displayBandCount = Math.Clamp(sliderValue, minBands, maxBands);
            sliderValue = displayBandCount;
        }

        suppressBarCountChanged = true;
        BarCountSlider.Minimum = minBands;
        BarCountSlider.Maximum = maxBands;
        BarCountSlider.IsEnabled = !barCountLocked;
        BarCountSlider.Value = sliderValue;
        BarCountValueText.Text = sliderValue.ToString(global::System.Globalization.CultureInfo.CurrentCulture);
        suppressBarCountChanged = false;
    }

    private void ApplyRendererControlState()
    {
        var capabilities = GetActiveRendererCapabilities();
        var usesFixedRenderer = IsBuiltInClonePresetActive();
        var barCountLocked = !capabilities.Controls.SupportsBarCount
            || capabilities.BarCountMode == RendererBarCountMode.Fixed;

        RendererCombo.IsEnabled = !usesFixedRenderer;
        RendererHintText.Text = ResolveRendererSelectionHint(usesFixedRenderer);

        BarCountPanel.Visibility = Visibility.Visible;
        CloneBarsHintText.Visibility = barCountLocked ? Visibility.Visible : Visibility.Collapsed;
        if (barCountLocked)
        {
            CloneBarsHintText.Text = !string.IsNullOrWhiteSpace(capabilities.UnsupportedControlsHint)
                ? capabilities.UnsupportedControlsHint
                : "A quantidade de barras é controlada pela própria visualização.";
        }

        ApplyBandCountBounds();
        UpdateSettingsPaneVisualState();
    }

    private void UpdateFftSmoothingText()
    {
        FftSmoothingValueText.Text = fftSmoothing.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
