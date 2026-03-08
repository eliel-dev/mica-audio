using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.WinUI.ViewModels;

internal sealed partial class AppsPageViewModel : ObservableObject
{
    private Func<Task>? reloadCatalogAsync;
    private Func<Task>? saveModifiersAsync;
    private Func<Task>? installAsync;

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial string SelectedAppName { get; set; }

    [ObservableProperty]
    public partial string SelectedAppMeta { get; set; }

    [ObservableProperty]
    public partial string SelectedAppDescription { get; set; }

    [ObservableProperty]
    public partial string OperationStatus { get; set; }

    [ObservableProperty]
    public partial int OperationPercent { get; set; }

    [ObservableProperty]
    public partial bool OperationInProgress { get; set; }

    public IAsyncRelayCommand ReloadCatalogCommand { get; }

    public IAsyncRelayCommand SaveModifiersCommand { get; }

    public IAsyncRelayCommand InstallCommand { get; }

    public AppsPageViewModel()
    {
        SearchText = string.Empty;
        SelectedAppName = "Selecione um app";
        SelectedAppMeta = "-";
        SelectedAppDescription = "Nenhum app selecionado.";
        OperationStatus = "Operacoes: pronto";

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


