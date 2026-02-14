using Microsoft.UI.Dispatching;

namespace App.WinUI.Services;

internal static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this DispatcherQueue dispatcherQueue, Action callback)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    callback();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue action on DispatcherQueue."));
        }

        return tcs.Task;
    }
}
