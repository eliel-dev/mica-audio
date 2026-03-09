using CommunityToolkit.Mvvm.ComponentModel;

namespace App.WinUI.ViewModels;

internal sealed partial class MonitoringPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string SourceStatus { get; set; }

    [ObservableProperty]
    public partial string SourceHint { get; set; }

    [ObservableProperty]
    public partial string LastRefreshText { get; set; }

    [ObservableProperty]
    public partial string SensorCountText { get; set; }

    public MonitoringPageViewModel()
    {
        SourceStatus = "HWiNFO64: carregando...";
        SourceHint = "Preparando a leitura dos sensores locais do PC.";
        LastRefreshText = "Ultima leitura: -";
        SensorCountText = "Sensores: 0 | Leituras: 0";
    }
}
