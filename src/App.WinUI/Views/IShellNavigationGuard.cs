namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
internal interface IShellNavigationGuard
{
    Task<bool> CanLeaveAsync();
}
