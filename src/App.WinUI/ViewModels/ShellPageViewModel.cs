using CommunityToolkit.Mvvm.ComponentModel;

namespace App.WinUI.ViewModels;

internal sealed partial class ShellPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string CurrentTag { get; set; }

    [ObservableProperty]
    public partial string ServerFooterText { get; set; }

    public ShellPageViewModel()
    {
        CurrentTag = string.Empty;
        ServerFooterText = "Servidor: inicializando...";
    }
}


