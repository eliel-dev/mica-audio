namespace App.WinUI.Views;

public partial class MainPage : IDisposable
{
    public void Dispose()
    {
        visualizerRuntimeDebounceTimer?.Stop();
        gifLoadCts?.Cancel();
        gifLoadCts?.Dispose();
        gifLoadCts = null;

        gifPlayer.FrameReady -= OnGifFrameReady;
        capture.StatusChanged -= OnCaptureStatusChanged;
        pipelineCoordinator.StatusChanged -= OnPipelineCoordinatorStatusChanged;

        gifPlayer.Dispose();
        DisposeHubFrameRenderer();
        GC.SuppressFinalize(this);
    }
}
