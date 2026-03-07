using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/guides/add-device-command.md#passos
internal sealed class PendingTrackedCommand
{
    private readonly TaskCompletionSource<CommandDispatchResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PendingTrackedCommand(string commandId, string deviceId, DeviceCommandType commandType)
    {
        CommandId = commandId;
        DeviceId = deviceId;
        CommandType = commandType;
    }

    public string CommandId { get; }

    public string DeviceId { get; }

    public DeviceCommandType CommandType { get; }

    public int LastPercent { get; set; }

    public Task<CommandDispatchResult> Task => tcs.Task;

    public bool TrySetResult(CommandDispatchResult result) => tcs.TrySetResult(result);

    public async Task<CommandDispatchResult> WaitForResultAsync(
        TimeSpan timeout,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (ReferenceEquals(timeProvider, TimeProvider.System))
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Pending command timed out.");
            }
        }

        var deadline = timeProvider.GetUtcNow() + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tcs.Task.IsCompleted)
            {
                return await tcs.Task.ConfigureAwait(false);
            }

            if (timeProvider.GetUtcNow() >= deadline)
            {
                throw new TimeoutException("Pending command timed out.");
            }

            await System.Threading.Tasks.Task.Yield();
        }
    }
}
