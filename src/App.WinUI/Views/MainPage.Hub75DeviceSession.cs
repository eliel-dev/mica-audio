using App.WinUI.Services.Devices;

namespace App.WinUI.Views;

public partial class MainPage
{
    private async Task SyncHub75DeviceSessionAsync()
    {
        if (ShouldIgnoreUiMutation())
        {
            return;
        }

        var services = App.Services;
        if (services?.GetService(typeof(Hub75VisualizerSessionService)) is not Hub75VisualizerSessionService sessionService)
        {
            return;
        }

        try
        {
            await sessionService.SetHub75ModeAsync(hubPreviewEnabled).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Falha ao sincronizar sessao HUB75: {ex.Message}");
        }
    }
}
