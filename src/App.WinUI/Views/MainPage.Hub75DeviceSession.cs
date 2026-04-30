namespace App.WinUI.Views;

public partial class MainPage
{
    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-toggle-hub75-como-gate-e-runtime-em-background
    private async Task SyncHub75DeviceSessionAsync()
    {
        if (ShouldIgnoreUiMutation())
        {
            return;
        }

        try
        {
            if (ShouldEnableHub75DeviceOutput())
            {
                await panelsPlaybackService.SuspendAsync().ConfigureAwait(false);
                await hub75VisualizerSessionService.SetHub75ModeAsync(enabled: true).ConfigureAwait(false);
                return;
            }

            await hub75VisualizerSessionService.SetHub75ModeAsync(enabled: false).ConfigureAwait(false);
            await panelsPlaybackService.ResumeSuspendedAsync().ConfigureAwait(false);
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
