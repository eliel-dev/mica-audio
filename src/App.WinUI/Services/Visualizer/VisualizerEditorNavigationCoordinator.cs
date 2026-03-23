namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
internal sealed class VisualizerEditorNavigationCoordinator
{
    public event EventHandler<OpenVisualizerPresetEditorRequest>? OpenVisualizerPresetEditorRequested;

    public event EventHandler<ReturnToVisualizerRequest>? ReturnToVisualizerRequested;

    public void OpenVisualizerPresetEditor(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            return;
        }

        OpenVisualizerPresetEditorRequested?.Invoke(this, new OpenVisualizerPresetEditorRequest(presetId));
    }

    public void ReturnToVisualizer(string? presetId, bool reloadPresets = true)
        => ReturnToVisualizerRequested?.Invoke(this, new ReturnToVisualizerRequest(presetId, reloadPresets));
}

internal sealed record OpenVisualizerPresetEditorRequest(string PresetId);

internal sealed record ReturnToVisualizerRequest(string? PresetId, bool ReloadPresets);
