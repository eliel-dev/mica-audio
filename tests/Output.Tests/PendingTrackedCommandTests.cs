using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public class PendingTrackedCommandTests
{
    [Fact]
    public void Store_ShouldAddGetRemoveAndDrain()
    {
        var store = new PendingTrackedCommandStore();
        var first = new PendingTrackedCommand("cmd-1", "device-1", DeviceCommandType.TestLed);
        var second = new PendingTrackedCommand("cmd-2", "device-1", DeviceCommandType.SetBrightness);

        store.Add(first);
        store.Add(second);

        Assert.True(store.TryGetValue("cmd-1", out var found));
        Assert.Same(first, found);
        Assert.Equal(2, store.Count);

        Assert.True(store.Remove("cmd-1", out var removed));
        Assert.Same(first, removed);

        var drained = store.Drain();
        Assert.Single(drained);
        Assert.Same(second, drained[0]);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task WaitForResultAsync_ShouldTimeoutAgainstManualTime()
    {
        var timeProvider = new ManualTimeProvider();
        var pending = new PendingTrackedCommand("cmd-1", "device-1", DeviceCommandType.TestLed);

        var waitTask = pending.WaitForResultAsync(TimeSpan.FromSeconds(5), timeProvider, CancellationToken.None);
        await Task.Yield();
        timeProvider.Advance(TimeSpan.FromSeconds(6));

        await Assert.ThrowsAsync<TimeoutException>(async () => await waitTask);
    }

    [Fact]
    public async Task WaitForResultAsync_ShouldCompleteWhenResultArrives()
    {
        var timeProvider = new ManualTimeProvider();
        var pending = new PendingTrackedCommand("cmd-1", "device-1", DeviceCommandType.TestLed);
        var waitTask = pending.WaitForResultAsync(TimeSpan.FromSeconds(5), timeProvider, CancellationToken.None);

        await Task.Yield();
        pending.TrySetResult(new CommandDispatchResult
        {
            DeviceId = "device-1",
            CommandId = "cmd-1",
            Accepted = true,
            Completed = true,
            Success = true,
            ProgressPercent = 100,
            Stage = "done",
        });

        var result = await waitTask;
        Assert.True(result.Success);
        Assert.Equal("done", result.Stage);
    }

    [Fact]
    public async Task WaitForResultAsync_ShouldHonorCancellation()
    {
        var timeProvider = new ManualTimeProvider();
        var pending = new PendingTrackedCommand("cmd-1", "device-1", DeviceCommandType.TestLed);
        using var cts = new CancellationTokenSource();

        var waitTask = pending.WaitForResultAsync(TimeSpan.FromSeconds(5), timeProvider, cts.Token);
        await Task.Yield();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waitTask);
    }
}
