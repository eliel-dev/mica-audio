using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.WinUI.ViewModels;

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
internal sealed partial class PanelsPageViewModel : ObservableObject
{
    private Func<Task>? createPanelAsync;
    private Func<Task>? duplicatePanelAsync;
    private Func<Task>? savePanelAsync;
    private Func<Task>? loadPanelAsync;
    private Func<Task>? stopPlaybackAsync;
    private Func<Task>? deletePanelAsync;

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string SelectedPanelName { get; set; }

    public IAsyncRelayCommand CreatePanelCommand { get; }

    public IAsyncRelayCommand DuplicatePanelCommand { get; }

    public IAsyncRelayCommand SavePanelCommand { get; }

    public IAsyncRelayCommand LoadPanelCommand { get; }

    public IAsyncRelayCommand StopPlaybackCommand { get; }

    public IAsyncRelayCommand DeletePanelCommand { get; }

    public PanelsPageViewModel()
    {
        StatusText = "Paineis: pronto";
        SelectedPanelName = "Nenhum painel selecionado";

        CreatePanelCommand = new AsyncRelayCommand(() => InvokeAsync(createPanelAsync));
        DuplicatePanelCommand = new AsyncRelayCommand(() => InvokeAsync(duplicatePanelAsync));
        SavePanelCommand = new AsyncRelayCommand(() => InvokeAsync(savePanelAsync));
        LoadPanelCommand = new AsyncRelayCommand(() => InvokeAsync(loadPanelAsync));
        StopPlaybackCommand = new AsyncRelayCommand(() => InvokeAsync(stopPlaybackAsync));
        DeletePanelCommand = new AsyncRelayCommand(() => InvokeAsync(deletePanelAsync));
    }

    public void ConfigureCommands(
        Func<Task>? createPanel,
        Func<Task>? duplicatePanel,
        Func<Task>? savePanel,
        Func<Task>? loadPanel,
        Func<Task>? stopPlayback,
        Func<Task>? deletePanel)
    {
        createPanelAsync = createPanel;
        duplicatePanelAsync = duplicatePanel;
        savePanelAsync = savePanel;
        loadPanelAsync = loadPanel;
        stopPlaybackAsync = stopPlayback;
        deletePanelAsync = deletePanel;
    }

    private static Task InvokeAsync(Func<Task>? callback)
        => callback is null ? Task.CompletedTask : callback();
}
