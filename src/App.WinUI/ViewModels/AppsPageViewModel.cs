using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.WinUI.ViewModels;

internal sealed partial class AppsPageViewModel : ObservableObject
{
    private Func<Task>? reloadCatalogAsync;
    private Func<Task>? saveModifiersAsync;
    private Func<Task>? installAsync;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedAppName = "Selecione um app";

    [ObservableProperty]
    private string selectedAppMeta = "-";

    [ObservableProperty]
    private string selectedAppDescription = "Nenhum app selecionado.";

    [ObservableProperty]
    private string operationStatus = "Operacoes: pronto";

    [ObservableProperty]
    private int operationPercent;

    [ObservableProperty]
    private bool operationInProgress;

    public IAsyncRelayCommand ReloadCatalogCommand { get; }

    public IAsyncRelayCommand SaveModifiersCommand { get; }

    public IAsyncRelayCommand InstallCommand { get; }

    public AppsPageViewModel()
    {
        ReloadCatalogCommand = new AsyncRelayCommand(async () =>
        {
            if (reloadCatalogAsync is not null)
            {
                await reloadCatalogAsync().ConfigureAwait(false);
            }
        });

        SaveModifiersCommand = new AsyncRelayCommand(async () =>
        {
            if (saveModifiersAsync is not null)
            {
                await saveModifiersAsync().ConfigureAwait(false);
            }
        });

        InstallCommand = new AsyncRelayCommand(async () =>
        {
            if (installAsync is not null)
            {
                await installAsync().ConfigureAwait(false);
            }
        });
    }

    public void ConfigureCommands(Func<Task>? reloadCatalog, Func<Task>? saveModifiers, Func<Task>? install)
    {
        reloadCatalogAsync = reloadCatalog;
        saveModifiersAsync = saveModifiers;
        installAsync = install;

        ReloadCatalogCommand.NotifyCanExecuteChanged();
        SaveModifiersCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
    }
}


