using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal sealed class DeviceCommandDispatcher
{
    private readonly IDeviceOperationsRuntime runtime;
    private readonly TimeSpan timeout;

    public DeviceCommandDispatcher(IDeviceOperationsRuntime runtime, TimeSpan timeout)
    {
        this.runtime = runtime;
        this.timeout = timeout;
    }

    public static CommandDispatchResult CreateInvalidDeviceResult()
    {
        return new CommandDispatchResult
        {
            DeviceId = string.Empty,
            Accepted = false,
            Completed = true,
            Success = false,
            ProgressPercent = 0,
            Stage = "invalid",
            Message = "Nenhum dispositivo selecionado.",
            ErrorCode = "invalid_device",
        };
    }

    public async Task<CommandDispatchResult> DispatchAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        DeviceCommandSessionContext? sessionContext,
        TimeSpan? timeoutOverride,
        CancellationToken cancellationToken)
    {
        try
        {
            return await runtime
                .SendCommandTrackedAsync(deviceId, commandType, parameters, sessionContext, timeoutOverride.GetValueOrDefault(timeout), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "cancelled",
                Message = "Operacao cancelada.",
                ErrorCode = "cancelled",
            };
        }
        catch (Exception ex)
        {
            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "error",
                Message = ex.Message,
                ErrorCode = "exception",
            };
        }
    }
}
