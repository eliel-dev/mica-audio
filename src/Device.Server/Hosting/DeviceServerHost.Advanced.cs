using System.Diagnostics;
using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#pontos-de-alteracao-frequente
public sealed partial class DeviceServerHost
{
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(5);

    // DOCS: docs/wiki/guides/add-device-command.md#passos
    private async Task<CommandDispatchResult> SendTrackedCommandCoreAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var commandId = Guid.NewGuid().ToString("N");
        var commandTypeName = commandType.ToString();
        var stopwatch = Stopwatch.StartNew();
        using var activity = DeviceServerObservability.StartCommandActivity("device.command.tracked", deviceId, commandId, commandTypeName);
        if (TryGetAppId(parameters, out var appId))
        {
            activity?.SetTag(DeviceServerObservability.AppIdKey, appId);
        }

        using var scope = DeviceServerObservability.PushScope(
            activity,
            (DeviceServerObservability.ComponentKey, DeviceServerObservability.Component),
            (DeviceServerObservability.MicaComponentKey, DeviceServerObservability.Component),
            (DeviceServerObservability.OperationKey, "device.command.tracked"),
            (DeviceServerObservability.DeviceIdKey, deviceId),
            (DeviceServerObservability.CommandIdKey, commandId),
            (DeviceServerObservability.CommandTypeKey, commandTypeName),
            (DeviceServerObservability.AppIdKey, appId));

        DeviceSession? state;
        lock (gate)
        {
            devices.TryGetValue(deviceId, out state);
        }

        if (state is null || !state.IsControlPlaneOnline)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "device_offline");
            DeviceServerObservability.RecordCommandFailure(deviceId, commandTypeName, "offline");
            DeviceServerObservability.RecordCommandDuration(stopwatch.Elapsed, deviceId, commandTypeName, success: false, stage: "offline");
            Serilog.Log.Warning("Tracked command rejected because device is offline. deviceId={DeviceId} commandType={CommandType}", deviceId, commandTypeName);
            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = commandId,
                Accepted = false,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "offline",
                Message = "Dispositivo offline.",
                ErrorCode = "device_offline",
            };
        }

        var pending = new PendingTrackedCommand(commandId, deviceId, commandType, activity);

        lock (gate)
        {
            pendingTrackedCommands.Add(pending);
        }

        PublishCommandProgress(new DeviceCommandProgressMessage
        {
            DeviceId = deviceId,
            CommandId = commandId,
            ProgressPercent = 0,
            Stage = "queued",
            Message = "Comando enfileirado.",
        });
        Serilog.Log.Information("Tracked command queued. deviceId={DeviceId} commandId={CommandId} commandType={CommandType}", deviceId, commandId, commandTypeName);

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
            await PublishCommandOverMqttAsync(state.Record.DeviceId, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (gate)
            {
                pendingTrackedCommands.Remove(commandId, out _);
            }

            DeviceServerObservability.SetException(activity, ex);
            DeviceServerObservability.RecordCommandFailure(deviceId, commandTypeName, "send-error");
            DeviceServerObservability.RecordCommandDuration(stopwatch.Elapsed, deviceId, commandTypeName, success: false, stage: "send-error");
            Serilog.Log.Warning(ex, "Tracked command failed before dispatch ACK flow. deviceId={DeviceId} commandId={CommandId}", deviceId, commandId);
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
        Serilog.Log.Information("Tracked command sent to MQTT. deviceId={DeviceId} commandId={CommandId}", deviceId, commandId);

        try
        {
            var result = await pending.WaitForResultAsync(timeout, timeProvider, cancellationToken).ConfigureAwait(false);
            activity?.SetTag("command.stage", result.Stage);
            activity?.SetTag("command.success", result.Success);
            if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                activity?.SetTag("command.error_code", result.ErrorCode);
            }

            if (!result.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.ErrorCode ?? result.Stage);
                DeviceServerObservability.RecordCommandFailure(deviceId, commandTypeName, result.Stage ?? "failed");
                Serilog.Log.Warning(
                    "Tracked command completed with failure. deviceId={DeviceId} commandId={CommandId} stage={Stage} errorCode={ErrorCode}",
                    deviceId,
                    commandId,
                    result.Stage,
                    result.ErrorCode);
            }
            else
            {
                Serilog.Log.Information(
                    "Tracked command completed successfully. deviceId={DeviceId} commandId={CommandId} stage={Stage}",
                    deviceId,
                    commandId,
                    result.Stage);
            }

            DeviceServerObservability.RecordCommandDuration(stopwatch.Elapsed, deviceId, commandTypeName, result.Success, result.Stage ?? "completed");
            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new CommandDispatchResult
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
            pending.RecordCompletion(result);
            activity?.SetStatus(ActivityStatusCode.Error, result.ErrorCode);
            DeviceServerObservability.RecordCommandFailure(deviceId, commandTypeName, result.Stage);
            DeviceServerObservability.RecordCommandDuration(stopwatch.Elapsed, deviceId, commandTypeName, success: false, stage: result.Stage);
            Serilog.Log.Warning("Tracked command cancelled. deviceId={DeviceId} commandId={CommandId}", deviceId, commandId);
            return result;
        }
        catch (TimeoutException)
        {
            PublishCommandProgress(new DeviceCommandProgressMessage
            {
                DeviceId = deviceId,
                CommandId = commandId,
                ProgressPercent = pending.LastPercent,
                Stage = "timeout",
                Message = "Sem resposta do dispositivo.",
                Success = false,
            });

            var result = new CommandDispatchResult
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
            pending.RecordCompletion(result);
            activity?.SetStatus(ActivityStatusCode.Error, result.ErrorCode);
            DeviceServerObservability.RecordCommandTimeout(deviceId, commandTypeName);
            DeviceServerObservability.RecordCommandFailure(deviceId, commandTypeName, result.Stage);
            DeviceServerObservability.RecordCommandDuration(stopwatch.Elapsed, deviceId, commandTypeName, success: false, stage: result.Stage);
            Serilog.Log.Warning("Tracked command timed out. deviceId={DeviceId} commandId={CommandId}", deviceId, commandId);
            return result;
        }
        finally
        {
            lock (gate)
            {
                pendingTrackedCommands.Remove(commandId, out _);
            }
        }
    }

    private async Task<IResult> HandleCommandAckTrackedAsync(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, AuthContext.HttpApi, out var state))
        {
            return Results.Unauthorized();
        }

        if (IsRequestBodyTooLarge(ctx))
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

        MarkLegacyControlPlaneTraffic(state, "command-ack");
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

    private async Task<bool> HandleIncomingWsTextAsync(DeviceSession state, string json)
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

                MarkLegacyControlPlaneTraffic(state, "ws-text");
                return TryApplyCommandProgress(state, msg);
            }

            var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(json, JsonOptions);
            if (telemetry is null)
            {
                return false;
            }

            MarkLegacyControlPlaneTraffic(state, "ws-text");
            return TryApplyTelemetry(state, telemetry, telemetry.IpAddress);
        }
        catch
        {
            return false;
        }
    }

    private bool TryHandleCommandEventPayload(DeviceSession state, byte[] payload)
    {
        try
        {
            var progress = JsonSerializer.Deserialize<DeviceCommandProgressMessage>(payload, JsonOptions);
            return progress is not null && TryApplyCommandProgress(state, progress);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryHandleTelemetryPayload(DeviceSession state, byte[] payload, string? remoteIp)
    {
        try
        {
            var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(payload, JsonOptions);
            return telemetry is not null && TryApplyTelemetry(state, telemetry, remoteIp);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryHandleStatsPayload(DeviceSession state, byte[] payload, string? remoteIp)
    {
        try
        {
            var stats = JsonSerializer.Deserialize<DeviceStatsMessage>(payload, JsonOptions);
            return stats is not null && TryApplyStats(state, stats, remoteIp);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryHandlePresencePayload(DeviceSession state, byte[] payload, string? remoteIp)
    {
        try
        {
            var presence = JsonSerializer.Deserialize<DevicePresenceMessage>(payload, JsonOptions);
            if (presence is null || string.IsNullOrWhiteSpace(presence.State))
            {
                return false;
            }

            if (string.Equals(presence.State, "online", StringComparison.OrdinalIgnoreCase))
            {
                state.MarkControlPlaneConnected(remoteIp);
            }
            else if (string.Equals(presence.State, "offline", StringComparison.OrdinalIgnoreCase))
            {
                state.MarkControlPlaneDisconnected();
            }
            else
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryHandleLogPayload(DeviceSession state, byte[] payload)
    {
        try
        {
            var log = JsonSerializer.Deserialize<DeviceLogMessage>(payload, JsonOptions);
            if (log is null || string.IsNullOrWhiteSpace(log.Message))
            {
                return false;
            }

            var normalized = NormalizeDeviceLog(state, log);
            if (normalized is null)
            {
                return false;
            }

            state.Touch();
            PublishDeviceLog(normalized);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryApplyCommandProgress(DeviceSession state, DeviceCommandProgressMessage message)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.CommandId))
        {
            return false;
        }

        state.Touch();
        var normalized = new DeviceCommandProgressMessage
        {
            DeviceId = string.IsNullOrWhiteSpace(message.DeviceId) ? state.Record.DeviceId : message.DeviceId,
            CommandId = message.CommandId,
            ProgressPercent = Math.Clamp(message.ProgressPercent, 0, 100),
            Stage = message.Stage,
            Message = message.Message,
            Success = message.Success,
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

    private static bool TryApplyTelemetry(DeviceSession state, DeviceTelemetryMessage telemetry, string? remoteIp)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(telemetry);

        state.MarkTelemetry(
            string.IsNullOrWhiteSpace(telemetry.IpAddress) ? remoteIp : telemetry.IpAddress,
            telemetry.Rssi,
            telemetry.FirmwareVersion,
            telemetry.ActiveAppId,
            telemetry.ActiveAppName,
            telemetry.BoardModel,
            telemetry.PanelType,
            telemetry.UptimeSeconds,
            telemetry.LoopHealthyPercent,
            telemetry.LoopLoadPercent,
            telemetry.ChipTemperatureCelsius,
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

    private static bool TryApplyStats(DeviceSession state, DeviceStatsMessage stats, string? remoteIp)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stats);

        state.MarkStats(
            remoteIp,
            stats.ChipModel,
            stats.ChipRevision,
            stats.ChipCores,
            stats.CpuFreqMHz,
            stats.SdkVersion,
            stats.HeapTotalBytes,
            stats.PsramTotalBytes,
            stats.FlashTotalBytes,
            stats.SketchSizeBytes,
            stats.FreeSketchBytes);
        return true;
    }

    private static DeviceLogMessage? NormalizeDeviceLog(DeviceSession state, DeviceLogMessage message)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(message);

        if (message.Sequence == 0)
        {
            return null;
        }

        var level = NormalizeLogLevel(message.Level);
        var category = NormalizeLogCategory(message.Category);
        if (string.IsNullOrWhiteSpace(level) || string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        return new DeviceLogMessage
        {
            DeviceId = string.IsNullOrWhiteSpace(message.DeviceId) ? state.Record.DeviceId : message.DeviceId,
            Sequence = message.Sequence,
            Level = level,
            Category = category,
            EventCode = NormalizeOptional(message.EventCode),
            Message = message.Message.Trim(),
            UptimeSeconds = message.UptimeSeconds,
            TelemetrySequence = message.TelemetrySequence,
        };
    }

    private static string? NormalizeLogLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "info";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "info" or "warning" or "error" => normalized,
            _ => null,
        };
    }

    private static string? NormalizeLogCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "device";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "device" or "wifi" or "mqtt" or "portal" or "ws" or "stream" or "command" => normalized,
            _ => null,
        };
    }

    private void TryCompletePending(string commandId, CommandDispatchResult result)
    {
        PendingTrackedCommand? pending;
        lock (gate)
        {
            pendingTrackedCommands.TryGetValue(commandId, out pending);
        }

        if (pending?.TrySetResult(result) == true)
        {
            pending.RecordCompletion(result);
        }
    }

    private void MarkLegacyControlPlaneTraffic(DeviceSession state, string transport)
    {
        ArgumentNullException.ThrowIfNull(state);

        bool firstObservedLegacyTraffic;
        lock (gate)
        {
            firstObservedLegacyTraffic = state.MarkLegacyControlPlaneTraffic();
        }

        if (firstObservedLegacyTraffic)
        {
            Log($"Firmware legado detectado para {state.Record.DeviceId}: control plane via {transport} sem MQTT.");
        }
    }

    private void PublishCommandProgress(DeviceCommandProgressMessage progress)
    {
        if (string.IsNullOrWhiteSpace(progress.CommandId))
        {
            CommandProgressChanged?.Invoke(this, progress);
            return;
        }

        lock (gate)
        {
            if (pendingTrackedCommands.TryGetValue(progress.CommandId, out var pending) && pending is not null)
            {
                pending.RecordProgress(progress);
            }
        }

        CommandProgressChanged?.Invoke(this, progress);
    }

    private void PublishDeviceLog(DeviceLogMessage log)
    {
        DeviceLogReceived?.Invoke(this, log);
    }

    private static bool TryGetAppId(IReadOnlyDictionary<string, string>? parameters, out string? appId)
    {
        appId = null;
        if (parameters is null)
        {
            return false;
        }

        if (!parameters.TryGetValue("appId", out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        appId = rawValue.Trim();
        return true;
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
            DeviceCommandType.UpdateFirmware => "update_firmware",
            _ => "unknown",
        };
    }
}
