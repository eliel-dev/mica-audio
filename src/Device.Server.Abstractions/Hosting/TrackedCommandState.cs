using System.Diagnostics;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-comandos-tracked
// DOCS: docs/wiki/guides/add-device-command.md#passos
// DOCS: docs/handoffs/2026-04-22-device-server-command-state-store.md
public sealed class TrackedCommandState
{
    private readonly TaskCompletionSource<CommandDispatchResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TrackedCommandState(string commandId, string deviceId, DeviceCommandType commandType, Activity? activity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        CommandId = commandId;
        DeviceId = deviceId;
        CommandType = commandType;
        Activity = activity;
    }

    public string CommandId { get; }

    public string DeviceId { get; }

    public DeviceCommandType CommandType { get; }

    public Activity? Activity { get; }

    public int LastPercent { get; set; }

    public Task<CommandDispatchResult> Task => tcs.Task;

    public bool TrySetResult(CommandDispatchResult result) => tcs.TrySetResult(result);

    public void RecordProgress(DeviceCommandProgressMessage progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        LastPercent = Math.Max(LastPercent, Math.Clamp(progress.ProgressPercent, 0, 100));
    }

    public void RecordCompletion(CommandDispatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        LastPercent = Math.Max(LastPercent, Math.Clamp(result.ProgressPercent, 0, 100));
    }

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
