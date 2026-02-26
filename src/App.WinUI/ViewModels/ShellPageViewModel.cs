using CommunityToolkit.Mvvm.ComponentModel;

namespace App.WinUI.ViewModels;

internal sealed partial class ShellPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string currentTag = string.Empty;

    [ObservableProperty]
    private string serverFooterText = "Servidor: inicializando...";
}


