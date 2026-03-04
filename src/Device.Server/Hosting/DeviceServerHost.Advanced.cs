using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#pontos-de-alteracao-frequente
public sealed partial class DeviceServerHost
{
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, PendingTrackedCommand> pendingTrackedCommands = new(StringComparer.OrdinalIgnoreCase);

    // DOCS: docs/wiki/guides/add-device-command.md#passos
    private async Task<CommandDispatchResult> SendTrackedCommandCoreAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DeviceState? state;
        lock (gate)
        {
            devices.TryGetValue(deviceId, out state);
        }

        if (state?.Socket is null || state.Socket.State != System.Net.WebSockets.WebSocketState.Open)
        {
            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = false,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "offline",
                Message = "Dispositivo offline.",
                ErrorCode = "device_offline",
            };
        }

        var commandId = Guid.NewGuid().ToString("N");
        var pending = new PendingTrackedCommand(commandId, deviceId, commandType);

        lock (gate)
        {
            pendingTrackedCommands[commandId] = pending;
        }

        PublishCommandProgress(new DeviceCommandProgressMessage
        {
            DeviceId = deviceId,
            CommandId = commandId,
            ProgressPercent = 0,
            Stage = "queued",
            Message = "Comando enfileirado.",
        });

        var commandEnvelope = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "command",
            ["commandId"] = commandId,
            ["command"] = CommandTypeToWire(commandType),
        };

        if (parameters is not null && parameters.Count > 0)
        {
            commandEnvelope["parameters"] = parameters;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(commandEnvelope, JsonOptions);

        try
        {
            await state.Socket.SendAsync(payload, System.Net.WebSockets.WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (gate)
            {
                pendingTrackedCommands.Remove(commandId);
            }

            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = commandId,
                Accepted = false,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "send-error",
                Message = ex.Message,
                ErrorCode = "send_error",
            };
        }

        PublishCommandProgress(new DeviceCommandProgressMessage
        {
            DeviceId = deviceId,
            CommandId = commandId,
            ProgressPercent = 20,
            Stage = "sent",
            Message = "Comando enviado ao dispositivo.",
        });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await pending.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new CommandDispatchResult
                {
                    DeviceId = deviceId,
                    CommandId = commandId,
                    Accepted = true,
                    Completed = true,
                    Success = false,
                    ProgressPercent = pending.LastPercent,
                    Stage = "cancelled",
                    Message = "Operacao cancelada.",
                    ErrorCode = "cancelled",
                };
            }

            PublishCommandProgress(new DeviceCommandProgressMessage
            {
                DeviceId = deviceId,
                CommandId = commandId,
                ProgressPercent = pending.LastPercent,
                Stage = "timeout",
                Message = "Sem resposta do dispositivo.",
                Success = false,
            });

            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = commandId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = pending.LastPercent,
                Stage = "timeout",
                Message = "Sem resposta do dispositivo dentro do timeout.",
                ErrorCode = "timeout",
            };
        }
        finally
        {
            lock (gate)
            {
                pendingTrackedCommands.Remove(commandId);
            }
        }
    }

    private async Task<IResult> HandleCommandAckTrackedAsync(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, AuthContext.HttpApi, out var state))
        {
            return Results.Unauthorized();
        }

        if (IsRequestBodyTooLarge(ctx, config.MaxJsonBodyBytes))
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        DeviceCommandAckRequest ack;
        try
        {
            ack = await JsonSerializer.DeserializeAsync<DeviceCommandAckRequest>(ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false)
                ?? new DeviceCommandAckRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        state.MarkSeen(ctx.Connection.RemoteIpAddress?.ToString(), state.Record.LastKnownRssi, state.Record.FirmwareVersion, state.Record.ActiveAppId, state.Record.ActiveAppName, state.Record.BoardModel, state.Record.PanelType);
        var progress = Math.Clamp(ack.ProgressPercent ?? (ack.Success ? 100 : 0), 0, 100);
        var stage = string.IsNullOrWhiteSpace(ack.Stage) ? (ack.Success ? "ack" : "ack-failed") : ack.Stage;

        if (!string.IsNullOrWhiteSpace(ack.CommandId))
        {
            PublishCommandProgress(new DeviceCommandProgressMessage
            {
                DeviceId = state.Record.DeviceId,
                CommandId = ack.CommandId,
                ProgressPercent = progress,
                Stage = stage,
                Message = ack.Message,
                Success = ack.Success,
            });

            if (!ack.Success || progress >= 100)
            {
                TryCompletePending(
                    ack.CommandId,
                    new CommandDispatchResult
                    {
                        DeviceId = state.Record.DeviceId,
                        CommandId = ack.CommandId,
                        Accepted = true,
                        Completed = true,
                        Success = ack.Success,
                        ProgressPercent = progress,
                        Stage = stage,
                        Message = ack.Message,
                        ErrorCode = ack.ErrorCode,
                    });
            }
        }

        Log($"ACK recebido de {state.Record.DeviceId}: {ack.CommandId}");
        NotifyDevicesChanged();
        return Results.Ok(new { ok = true });
    }

    private async Task<bool> HandleIncomingWsTextAsync(DeviceState state, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            if (string.Equals(type, "command_progress", StringComparison.OrdinalIgnoreCase))
            {
                var msg = JsonSerializer.Deserialize<DeviceCommandProgressMessage>(json, JsonOptions);
                if (msg is null || string.IsNullOrWhiteSpace(msg.CommandId))
                {
                    return false;
                }

                state.Touch();
                var normalized = new DeviceCommandProgressMessage
                {
                    DeviceId = string.IsNullOrWhiteSpace(msg.DeviceId) ? state.Record.DeviceId : msg.DeviceId,
                    CommandId = msg.CommandId,
                    ProgressPercent = Math.Clamp(msg.ProgressPercent, 0, 100),
                    Stage = msg.Stage,
                    Message = msg.Message,
                    Success = msg.Success,
                };

                PublishCommandProgress(normalized);

                if (normalized.Success is false)
                {
                    TryCompletePending(
                        normalized.CommandId,
                        new CommandDispatchResult
                        {
                            DeviceId = normalized.DeviceId,
                            CommandId = normalized.CommandId,
                            Accepted = true,
                            Completed = true,
                            Success = false,
                            ProgressPercent = normalized.ProgressPercent,
                            Stage = normalized.Stage,
                            Message = normalized.Message,
                            ErrorCode = "device_reported_failure",
                        });
                }
                else if (normalized.Success is true || normalized.ProgressPercent >= 100)
                {
                    TryCompletePending(
                        normalized.CommandId,
                        new CommandDispatchResult
                        {
                            DeviceId = normalized.DeviceId,
                            CommandId = normalized.CommandId,
                            Accepted = true,
                            Completed = true,
                            Success = true,
                            ProgressPercent = 100,
                            Stage = normalized.Stage,
                            Message = normalized.Message,
                        });
                }

                return true;
            }

            var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json, JsonOptions);
            if (telemetry is null)
            {
                return false;
            }

            state.MarkTelemetry(
                telemetry.IpAddress,
                telemetry.Rssi,
                telemetry.FirmwareVersion,
                telemetry.ActiveAppId,
                telemetry.ActiveAppName,
                telemetry.BoardModel,
                telemetry.PanelType,
                telemetry.UptimeSeconds,
                telemetry.LoopLoadPercent,
                telemetry.FreeHeapBytes,
                telemetry.LargestHeapBlockBytes,
                telemetry.PsramAvailable,
                telemetry.FreePsramBytes,
                telemetry.LargestPsramBlockBytes,
                telemetry.WifiConnected,
                telemetry.WifiState,
                telemetry.ProvisioningPortalActive,
                telemetry.AuxLedAvailable,
                telemetry.TestLedAvailable,
                telemetry.LastWifiEvent,
                telemetry.StreamLastSequence,
                telemetry.StreamFramesReceived,
                telemetry.StreamFramesApplied,
                telemetry.StreamSequenceGapCount,
                telemetry.StreamInvalidFrameCount,
                telemetry.TelemetrySequence,
                telemetry.BrightnessCap,
                telemetry.BrightnessRequested,
                telemetry.BrightnessApplied,
                telemetry.TestLedEnabled,
                telemetry.TestLedDuty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryCompletePending(string commandId, CommandDispatchResult result)
    {
        PendingTrackedCommand? pending;
        lock (gate)
        {
            pendingTrackedCommands.TryGetValue(commandId, out pending);
        }

        pending?.TrySetResult(result);
    }

    private void PublishCommandProgress(DeviceCommandProgressMessage progress)
    {
        lock (gate)
        {
            if (pendingTrackedCommands.TryGetValue(progress.CommandId, out var pending))
            {
                pending.LastPercent = Math.Max(pending.LastPercent, Math.Clamp(progress.ProgressPercent, 0, 100));
            }
        }

        CommandProgressChanged?.Invoke(this, progress);
    }

    private static string CommandTypeToWire(DeviceCommandType commandType)
    {
        return commandType switch
        {
            DeviceCommandType.EnterProvisioning => "enter_provisioning",
            DeviceCommandType.RevokeAndRestart => "revoke_and_restart",
            DeviceCommandType.TestLed => "test_led",
            DeviceCommandType.InstallApp => "install_app",
            DeviceCommandType.ActivateApp => "activate_app",
            DeviceCommandType.SetAppConfig => "set_app_config",
            DeviceCommandType.SetBrightness => "set_brightness",
            _ => "unknown",
        };
    }

    private sealed class PendingTrackedCommand
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
    }
}



