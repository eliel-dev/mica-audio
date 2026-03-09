using System.Diagnostics;
using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/guides/add-device-command.md#passos
internal sealed class PendingTrackedCommand
{
    private readonly TaskCompletionSource<CommandDispatchResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PendingTrackedCommand(string commandId, string deviceId, DeviceCommandType commandType, Activity? activity = null)
    {
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
        if (Activity is null)
        {
            return;
        }

        Activity.SetTag("command.progress", LastPercent);
        if (!string.IsNullOrWhiteSpace(progress.Stage))
        {
            Activity.SetTag("command.stage", progress.Stage);
        }

        if (progress.Success.HasValue)
        {
            Activity.SetTag("command.success.reported", progress.Success.Value);
        }

        var tags = new ActivityTagsCollection
        {
            { DeviceServerObservability.CommandIdKey, CommandId },
            { DeviceServerObservability.DeviceIdKey, DeviceId },
            { "command.progress", LastPercent },
            { "command.stage", progress.Stage },
            { "command.message", progress.Message },
        };

        if (progress.Success.HasValue)
        {
            tags.Add("command.success.reported", progress.Success.Value);
        }

        Activity.AddEvent(new ActivityEvent("command.progress", tags: tags));
    }

    public void RecordCompletion(CommandDispatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        LastPercent = Math.Max(LastPercent, Math.Clamp(result.ProgressPercent, 0, 100));
        if (Activity is null)
        {
            return;
        }

        Activity.SetTag("command.progress", LastPercent);
        Activity.SetTag("command.stage", result.Stage);
        Activity.SetTag("command.success", result.Success);
        Activity.SetTag(DeviceServerObservability.CommandIdKey, result.CommandId);

        var tags = new ActivityTagsCollection
        {
            { DeviceServerObservability.CommandIdKey, result.CommandId },
            { DeviceServerObservability.DeviceIdKey, result.DeviceId },
            { "command.progress", LastPercent },
            { "command.stage", result.Stage },
            { "command.success", result.Success },
            { "command.error_code", result.ErrorCode },
            { "command.message", result.Message },
        };

        Activity.AddEvent(new ActivityEvent("command.completed", tags: tags));
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
