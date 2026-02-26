using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.WinUI.ViewModels;

internal sealed partial class DevicesPageViewModel : ObservableObject
{
    private Action? refreshAction;
    private Action? generatePairingAction;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedDeviceTitle = "Nenhum dispositivo selecionado";

    [ObservableProperty]
    private string selectedDeviceSubtitle = "-";

    [ObservableProperty]
    private string selectedDeviceApp = "App ativo: -";

    [ObservableProperty]
    private string serverInfo = "Servidor: inicializando...";

    [ObservableProperty]
    private string commandStatus = "Comandos: pronto";

    [ObservableProperty]
    private int commandPercent;

    [ObservableProperty]
    private bool commandInProgress;

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand GeneratePairingCommand { get; }

    public DevicesPageViewModel()
    {
        RefreshCommand = new RelayCommand(() => refreshAction?.Invoke());
        GeneratePairingCommand = new RelayCommand(() => generatePairingAction?.Invoke());
    }

    public void ConfigureCommands(Action? refresh, Action? generatePairing)
    {
        refreshAction = refresh;
        generatePairingAction = generatePairing;

        RefreshCommand.NotifyCanExecuteChanged();
        GeneratePairingCommand.NotifyCanExecuteChanged();
    }
}


