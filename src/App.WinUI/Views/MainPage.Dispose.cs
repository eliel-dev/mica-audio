namespace App.WinUI.Views;

public partial class MainPage : IDisposable
{
    public void Dispose()
    {
        gifLoadCts?.Cancel();
        gifLoadCts?.Dispose();
        gifLoadCts = null;

        gifPlayer.FrameReady -= OnGifFrameReady;
        capture.StatusChanged -= OnCaptureStatusChanged;
        pipelineCoordinator.StatusChanged -= OnPipelineCoordinatorStatusChanged;

        gifPlayer.Dispose();
        gifHttpClient.Dispose();
        DisposeHubFrameRenderer();
        GC.SuppressFinalize(this);
    }
}
