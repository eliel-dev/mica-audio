using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App.WinUI.ViewModels;

internal sealed partial class DevicesPageViewModel : ObservableObject
{
    private Action? refreshAction;
    private Action? generatePairingAction;

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial string SelectedDeviceTitle { get; set; }

    [ObservableProperty]
    public partial string SelectedDeviceSubtitle { get; set; }

    [ObservableProperty]
    public partial string SelectedDeviceApp { get; set; }

    [ObservableProperty]
    public partial string ServerInfo { get; set; }

    [ObservableProperty]
    public partial string CommandStatus { get; set; }

    [ObservableProperty]
    public partial int CommandPercent { get; set; }

    [ObservableProperty]
    public partial bool CommandInProgress { get; set; }

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand GeneratePairingCommand { get; }

    public DevicesPageViewModel()
    {
        SearchText = string.Empty;
        SelectedDeviceTitle = "Nenhum dispositivo selecionado";
        SelectedDeviceSubtitle = "-";
        SelectedDeviceApp = "App ativo: -";
        ServerInfo = "Servidor: inicializando...";
        CommandStatus = "Comandos: pronto";

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


