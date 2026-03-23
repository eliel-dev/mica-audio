using App.WinUI.Services.Visualizer;
using Microsoft.UI.Xaml;
using MicaAudio.Core.Presets;

namespace App.WinUI.Views;

public partial class MainPage
{
    private bool reloadPresetsOnEditorReturn;
    private string? pendingEditorReturnPresetId;

    // DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
    // DOCS: docs/wiki/modules/settings-presets-persistence.md#studio-de-presets
    private async void OnEditVisualClicked(object sender, RoutedEventArgs e)
    {
        if (presetsById.Count == 0)
        {
            StatusText.Text = "Nenhum preset disponivel para edicao.";
            return;
        }

        try
        {
            StatusText.Text = "Abrindo editor de preset...";
            suppressHub75SessionWhileInEditor = true;
            gifPlayer.Stop();
            await pipelineCoordinator.StopAsync().ConfigureAwait(true);
            await SyncHub75DeviceSessionAsync().ConfigureAwait(true);
            visualizerEditorNavigation.OpenVisualizerPresetEditor(currentPresetId);
        }
        catch (Exception ex)
        {
            suppressHub75SessionWhileInEditor = false;
            StatusText.Text = $"Falha ao abrir o editor: {ex.Message}";
        }
    }

    internal void PrepareReturnFromEditor(string? presetId, bool reloadPresets)
    {
        suppressHub75SessionWhileInEditor = false;
        pendingEditorReturnPresetId = presetId;
        reloadPresetsOnEditorReturn = reloadPresets;
    }

    private async Task ApplyPendingEditorReturnAsync()
    {
        if (!reloadPresetsOnEditorReturn)
        {
            return;
        }

        reloadPresetsOnEditorReturn = false;
        var presetIdToSelect = string.IsNullOrWhiteSpace(pendingEditorReturnPresetId)
            ? currentPresetId
            : pendingEditorReturnPresetId;
        pendingEditorReturnPresetId = null;

        await ReloadPresetCatalogAsync().ConfigureAwait(true);

        BuildPresetNavigationOrder();
        SelectPreset(ResolveActivePreset(presetIdToSelect), showHud: false, forceApply: true);
        StatusText.Text = $"Preset atualizado: {activePreset.Name}.";
    }

    private async Task ReloadPresetCatalogAsync()
    {
        builtInPresetNameOverrides = await builtInPresetNameOverrideStore.LoadAsync().ConfigureAwait(true);
        var presets = await presetRepository.LoadOrSeedAsync().ConfigureAwait(true);
        presetsById.Clear();

        foreach (var preset in VisualizerPresetPaletteEditor.ApplyDisplayNames(presets, builtInPresetNameOverrides))
        {
            if (preset is null || string.IsNullOrWhiteSpace(preset.PresetId))
            {
                continue;
            }

            presetsById[preset.PresetId] = preset;
        }
    }
}
