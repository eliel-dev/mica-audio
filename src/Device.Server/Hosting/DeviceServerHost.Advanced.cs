
using System.Security.Cryptography;
using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Device.Server.Hosting;

public sealed partial class DeviceServerHost
{
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OtaSessionTtl = TimeSpan.FromMinutes(2);

    private readonly Dictionary<string, PendingTrackedCommand> pendingTrackedCommands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OtaSessionState> otaDownloadSessions = new(StringComparer.OrdinalIgnoreCase);

    private OtaArtifactState? otaArtifact;

    private async Task<CommandDispatchResult> SendTrackedCommandCoreAsync(
        string deviceId,
        DeviceCommandType commandType,
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

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "command",
            commandId,
            command = CommandTypeToWire(commandType),
        }, JsonOptions);

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

    private bool SetOtaArtifactCore(string mergedBinPath, string version)
    {
        if (string.IsNullOrWhiteSpace(mergedBinPath) || !File.Exists(mergedBinPath))
        {
            return false;
        }

        string sha256;
        long size;
        using (var stream = File.OpenRead(mergedBinPath))
        {
            size = stream.Length;
            sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        lock (gate)
        {
            otaArtifact = new OtaArtifactState(
                mergedBinPath,
                string.IsNullOrWhiteSpace(version) ? DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") : version,
                sha256,
                size,
                DateTimeOffset.UtcNow);

            CleanupOtaSessionsLocked();
        }

        Log($"Artefato OTA configurado: {Path.GetFileName(mergedBinPath)} ({size} bytes)");
        return true;
    }

    private IResult HandleFirmwareLatestAsync(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, out var state))
        {
            return Results.Unauthorized();
        }

        OtaArtifactState? artifact;
        lock (gate)
        {
            artifact = otaArtifact;
        }

        if (artifact is null || !File.Exists(artifact.MergedBinPath))
        {
            return Results.NotFound(new { error = "ota_artifact_not_available" });
        }

        var sessionId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(18));
        var expiresAt = DateTimeOffset.UtcNow.Add(OtaSessionTtl);

        lock (gate)
        {
            CleanupOtaSessionsLocked();
            otaDownloadSessions[sessionId] = new OtaSessionState(state.Record.DeviceId, expiresAt);
        }

        var host = ResolveHost(ctx);
        var query = $"deviceId={Uri.EscapeDataString(state.Record.DeviceId)}&token={Uri.EscapeDataString(state.Record.Token)}&session={Uri.EscapeDataString(sessionId)}";
        var downloadUrl = $"http://{host}:{config.Port}/api/v1/device/firmware/download?{query}";

        return Results.Ok(new DeviceFirmwareLatestResponse
        {
            Version = artifact.Version,
            Sha256 = artifact.Sha256,
            SizeBytes = artifact.SizeBytes,
            DownloadUrl = downloadUrl,
            ExpiresAtUtc = expiresAt,
        });
    }

    private IResult HandleFirmwareDownloadAsync(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, out var state))
        {
            return Results.Unauthorized();
        }

        var session = ctx.Request.Query["session"].ToString();
        if (string.IsNullOrWhiteSpace(session))
        {
            Log($"OTA download rejeitado para {state.Record.DeviceId}: session ausente.");
            return Results.BadRequest(new { error = "missing_session" });
        }

        OtaArtifactState? artifact;
        lock (gate)
        {
            CleanupOtaSessionsLocked();

            if (!otaDownloadSessions.TryGetValue(session, out var sessionState)
                || !string.Equals(sessionState.DeviceId, state.Record.DeviceId, StringComparison.OrdinalIgnoreCase)
                || sessionState.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                Log($"OTA download rejeitado para {state.Record.DeviceId}: sessao invalida/expirada.");
                return Results.Unauthorized();
            }

            otaDownloadSessions.Remove(session);
            artifact = otaArtifact;
        }

        if (artifact is null || !File.Exists(artifact.MergedBinPath))
        {
            Log($"OTA download indisponivel para {state.Record.DeviceId}: artefato ausente.");
            return Results.NotFound(new { error = "ota_artifact_not_available" });
        }

        Log($"OTA download liberado para {state.Record.DeviceId}: {Path.GetFileName(artifact.MergedBinPath)}");
        return Results.File(
            artifact.MergedBinPath,
            contentType: "application/octet-stream",
            fileDownloadName: "matrixportal-s3_merged.bin",
            enableRangeProcessing: false);
    }

    private async Task<IResult> HandleOtaResultAsync(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, out var state))
        {
            return Results.Unauthorized();
        }

        var request = await JsonSerializer.DeserializeAsync<DeviceOtaResultRequest>(ctx.Request.Body, JsonOptions).ConfigureAwait(false)
            ?? new DeviceOtaResultRequest();

        state.MarkSeen(ctx.Connection.RemoteIpAddress?.ToString(), state.Record.LastKnownRssi, state.Record.FirmwareVersion);

        if (!string.IsNullOrWhiteSpace(request.CommandId))
        {
            PublishCommandProgress(new DeviceCommandProgressMessage
            {
                DeviceId = state.Record.DeviceId,
                CommandId = request.CommandId,
                ProgressPercent = request.Success ? 100 : 99,
                Stage = request.Success ? "ota-complete" : "ota-failed",
                Message = request.Message,
                Success = request.Success,
            });

            TryCompletePending(
                request.CommandId,
                new CommandDispatchResult
                {
                    DeviceId = state.Record.DeviceId,
                    CommandId = request.CommandId,
                    Accepted = true,
                    Completed = true,
                    Success = request.Success,
                    ProgressPercent = request.Success ? 100 : 99,
                    Stage = request.Success ? "ota-complete" : "ota-failed",
                    Message = request.Message,
                    ErrorCode = request.ErrorCode,
                });
        }

        Log($"OTA result de {state.Record.DeviceId}: success={request.Success} cmd={request.CommandId}");
        NotifyDevicesChanged();
        return Results.Ok(new { ok = true });
    }

    private async Task<IResult> HandleCommandAckTrackedAsync(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, out var state))
        {
            return Results.Unauthorized();
        }

        var ack = await JsonSerializer.DeserializeAsync<DeviceCommandAckRequest>(ctx.Request.Body, JsonOptions).ConfigureAwait(false)
            ?? new DeviceCommandAckRequest();

        state.MarkSeen(ctx.Connection.RemoteIpAddress?.ToString(), state.Record.LastKnownRssi, state.Record.FirmwareVersion);
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

            state.MarkSeen(telemetry.IpAddress, telemetry.Rssi, telemetry.FirmwareVersion);
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
            DeviceCommandType.StartOta => "start_ota",
            _ => "unknown",
        };
    }

    private void CleanupOtaSessionsLocked()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var session in otaDownloadSessions.Where(pair => pair.Value.ExpiresAtUtc <= now).ToArray())
        {
            otaDownloadSessions.Remove(session.Key);
        }
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

    private sealed record OtaArtifactState(string MergedBinPath, string Version, string Sha256, long SizeBytes, DateTimeOffset CreatedAtUtc);

    private sealed record OtaSessionState(string DeviceId, DateTimeOffset ExpiresAtUtc);
}
