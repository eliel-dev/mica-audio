using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceCommandTrackerTests
{
    [Fact]
    public void TryQueue_ShouldRejectSecondCommandForSameDevice()
    {
        var tracker = new DeviceCommandTracker();

        Assert.True(tracker.TryQueue("device-1", "instalar app", out var firstBusy));
        Assert.Null(firstBusy);

        Assert.False(tracker.TryQueue("device-1", "ativar app", out var busyResult));
        Assert.NotNull(busyResult);
        Assert.Equal("busy", busyResult!.ErrorCode);
    }

    [Fact]
    public void ApplyProgress_ShouldIgnoreDifferentCommandId()
    {
        var tracker = new DeviceCommandTracker();
        Assert.True(tracker.TryQueue("device-1", "instalar app", out _));

        Assert.True(tracker.ApplyProgress(new DeviceCommandProgressMessage
        {
            DeviceId = "device-1",
            CommandId = "cmd-1",
            ProgressPercent = 35,
            Stage = "running",
            Message = "primeiro update",
        }, out _, out _));

        Assert.False(tracker.ApplyProgress(new DeviceCommandProgressMessage
        {
            DeviceId = "device-1",
            CommandId = "cmd-2",
            ProgressPercent = 80,
            Stage = "running",
            Message = "nao deve sobrescrever",
        }, out _, out _));

        var snapshot = tracker.GetSnapshot();
        Assert.True(snapshot.CommandInProgress);
        Assert.Equal(35, snapshot.CommandPercent);
        Assert.Contains("running", snapshot.CommandStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Complete_ShouldPreserveHighestObservedPercent()
    {
        var tracker = new DeviceCommandTracker();
        Assert.True(tracker.TryQueue("device-1", "configurar app", out _));

        Assert.True(tracker.ApplyProgress(new DeviceCommandProgressMessage
        {
            DeviceId = "device-1",
            CommandId = "cmd-1",
            ProgressPercent = 72,
            Stage = "running",
            Message = "etapa principal",
        }, out _, out _));

        tracker.Complete("device-1", new CommandDispatchResult
        {
            DeviceId = "device-1",
            Accepted = true,
            Completed = true,
            Success = false,
            ProgressPercent = 10,
            Stage = "error",
            ErrorCode = "send_error",
            Message = "falhou",
        });

        var snapshot = tracker.GetSnapshot();
        Assert.False(snapshot.CommandInProgress);
        Assert.Equal(72, snapshot.CommandPercent);
        Assert.Equal("Comandos: erro de rede", snapshot.CommandStatus);
        Assert.Equal("device-1", snapshot.LastCommandDeviceId);
        Assert.Empty(snapshot.CommandByDevice);
    }
}
