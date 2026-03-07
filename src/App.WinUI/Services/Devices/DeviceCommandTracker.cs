using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal readonly record struct DeviceCommandTrackerSnapshot(
    bool CommandInProgress,
    int CommandPercent,
    string CommandStatus,
    string? LastCommandDeviceId,
    IReadOnlyDictionary<string, DeviceCommandExecutionState> CommandByDevice);

internal sealed class DeviceCommandTracker
{
    private readonly object gate = new();
    private readonly Dictionary<string, DeviceCommandExecutionContext> commandByDevice = new(StringComparer.OrdinalIgnoreCase);
    private bool commandInProgress;
    private int commandPercent;
    private string commandStatus = "Comandos: pronto";
    private string? lastCommandDeviceId;

    public DeviceCommandTrackerSnapshot GetSnapshot()
    {
        lock (gate)
        {
            var snapshot = new Dictionary<string, DeviceCommandExecutionState>(StringComparer.OrdinalIgnoreCase);
            foreach (var (deviceId, context) in commandByDevice)
            {
                snapshot[deviceId] = context.ToSnapshot();
            }

            return new DeviceCommandTrackerSnapshot(
                commandInProgress,
                commandPercent,
                commandStatus,
                lastCommandDeviceId,
                snapshot);
        }
    }

    public bool TryQueue(string deviceId, string operationLabel, out CommandDispatchResult? busyResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationLabel);

        lock (gate)
        {
            if (commandByDevice.TryGetValue(deviceId, out var runningCommand))
            {
                busyResult = new CommandDispatchResult
                {
                    DeviceId = deviceId,
                    Accepted = false,
                    Completed = true,
                    Success = false,
                    ProgressPercent = Math.Clamp(runningCommand.Percent, 0, 100),
                    Stage = "busy",
                    Message = "Ja existe uma operacao em andamento para este dispositivo.",
                    ErrorCode = "busy",
                };
                return false;
            }

            commandByDevice[deviceId] = new DeviceCommandExecutionContext
            {
                DeviceId = deviceId,
                Percent = 0,
                Status = $"Comandos: 0% ({operationLabel})",
                Stage = "queued",
            };

            commandInProgress = true;
            commandPercent = 0;
            commandStatus = $"Comandos: 0% ({operationLabel})";
            lastCommandDeviceId = deviceId;
            busyResult = null;
            return true;
        }
    }

    public bool ApplyProgress(DeviceCommandProgressMessage progress, out string normalizedDeviceId, out string progressMessage)
    {
        normalizedDeviceId = string.Empty;
        progressMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(progress.DeviceId) || string.IsNullOrWhiteSpace(progress.CommandId))
        {
            return false;
        }

        normalizedDeviceId = progress.DeviceId.Trim();
        var progressPercent = 0;

        lock (gate)
        {
            if (!commandByDevice.TryGetValue(normalizedDeviceId, out var context))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(context.CommandId))
            {
                context.CommandId = progress.CommandId;
            }

            if (!string.Equals(context.CommandId, progress.CommandId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            context.Percent = Math.Clamp(Math.Max(context.Percent, progress.ProgressPercent), 0, 100);
            context.Status = DeviceOperationsText.BuildLiveCommandStatus(progress);
            context.Stage = progress.Stage;
            progressPercent = context.Percent;

            if (string.Equals(lastCommandDeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(lastCommandDeviceId))
            {
                commandPercent = context.Percent;
                commandStatus = context.Status;
                lastCommandDeviceId = normalizedDeviceId;
            }
        }

        progressMessage = !string.IsNullOrWhiteSpace(progress.Message)
            ? $"[{DeviceOperationsText.DescribeStage(progress.Stage)}] {progress.Message}"
            : $"[{DeviceOperationsText.DescribeStage(progress.Stage)}] Progresso de comando: {progressPercent}%";
        return true;
    }

    public void Complete(string deviceId, CommandDispatchResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(result);

        lock (gate)
        {
            var finalPercent = Math.Clamp(result.ProgressPercent, 0, 100);
            if (commandByDevice.TryGetValue(deviceId, out var context))
            {
                finalPercent = Math.Clamp(Math.Max(context.Percent, finalPercent), 0, 100);
                context.Percent = finalPercent;
                context.Status = DeviceOperationsText.BuildFinalCommandStatus(result);
                context.Stage = result.Stage;
                context.ErrorCode = result.ErrorCode;

                if (!string.IsNullOrWhiteSpace(result.CommandId))
                {
                    context.CommandId = result.CommandId;
                }

                commandByDevice.Remove(deviceId);
            }

            commandInProgress = commandByDevice.Count > 0;
            commandPercent = finalPercent;
            commandStatus = DeviceOperationsText.BuildFinalCommandStatus(result);
            lastCommandDeviceId = deviceId;
        }
    }
}
