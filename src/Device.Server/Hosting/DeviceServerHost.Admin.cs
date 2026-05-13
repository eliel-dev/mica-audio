using System.Buffers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
public sealed partial class DeviceServerHost
{
    private static readonly TimeSpan AdminPairingCodeDefaultTtl = TimeSpan.FromMinutes(10);

    private IResult HandleAdminDevices(HttpContext ctx)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        return Results.Ok(new AdminDevicesResponse
        {
            Devices = GetDevicesSnapshot(),
        });
    }

    private async Task<IResult> HandleAdminCreatePairingCodeAsync(HttpContext ctx)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        AdminCreatePairingCodeRequest req;
        try
        {
            req = await JsonSerializer.DeserializeAsync<AdminCreatePairingCodeRequest>(ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false)
                  ?? new AdminCreatePairingCodeRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        var ttl = req.TtlSeconds <= 0
            ? AdminPairingCodeDefaultTtl
            : TimeSpan.FromSeconds(Math.Clamp(req.TtlSeconds, 1, 3600));
        return Results.Ok(CreatePairingCode(ttl));
    }

    private IResult HandleAdminRemoveDevice(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        return RemoveDevice(deviceId)
            ? Results.NoContent()
            : Results.NotFound(new { error = "device_not_found" });
    }

    private async Task<IResult> HandleAdminTrackedCommandAsync(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        AdminTrackedCommandRequest req;
        try
        {
            req = await JsonSerializer.DeserializeAsync<AdminTrackedCommandRequest>(ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false)
                  ?? new AdminTrackedCommandRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        var timeout = TimeSpan.FromMilliseconds(Math.Clamp(req.TimeoutMs, 1, 600_000));
        var result = await SendCommandTrackedAsync(
                deviceId,
                req.CommandType,
                req.Parameters,
                timeout,
                ctx.RequestAborted)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }

    private async Task<IResult> HandleAdminPanelsBatchAsync(
        HttpContext ctx,
        string deviceId,
        string panelsSessionId,
        ulong batchSequence)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (!int.TryParse(ctx.Request.Query["frameCount"], out var frameCount) || frameCount <= 0)
        {
            return Results.BadRequest(new { error = "frame_count_invalid" });
        }

        if (!int.TryParse(ctx.Request.Query["durationMs"], out var durationMs) || durationMs <= 0)
        {
            return Results.BadRequest(new { error = "duration_invalid" });
        }

        if (ctx.Request.ContentLength > runtimeConfig.MaxJsonBodyBytes * 64)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        await using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms, ctx.RequestAborted).ConfigureAwait(false);
        var payload = ms.ToArray();
        if (payload.Length == 0)
        {
            return Results.BadRequest(new { error = "payload_empty" });
        }

        var registration = RegisterPanelsBatch(
            deviceId,
            panelsSessionId,
            batchSequence,
            payload,
            frameCount,
            durationMs,
            NormalizeOptional(ctx.Request.ContentType) ?? "image/webp");

        return Results.Ok(new AdminPanelsBatchRegistrationResponse
        {
            PanelsSessionId = registration.PanelsSessionId,
            BatchSequence = registration.BatchSequence,
            FileSizeBytes = registration.FileSizeBytes,
            Sha256 = registration.Sha256,
            ContentType = registration.ContentType,
            FrameCount = registration.FrameCount,
            DurationMs = registration.DurationMs,
            DownloadUrl = registration.DownloadUrl,
        });
    }

    private IResult HandleAdminClearPanelsBatches(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        var panelsSessionId = NormalizeOptional(ctx.Request.Query["panelsSessionId"].ToString());
        ClearPanelsBatches(deviceId, panelsSessionId);
        return Results.NoContent();
    }

    private async Task HandleAdminEventsWebSocketAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!await TryAcceptAdminWebSocketAsync(ctx).ConfigureAwait(false))
        {
            return;
        }

        var ws = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var connection = new AdminEventConnection(ws);
        lock (adminEventConnectionsGate)
        {
            adminEventConnections.Add(connection);
        }

        await connection.TrySendAsync(JsonSerializer.SerializeToUtf8Bytes(new AdminEventMessage
        {
            Type = "heartbeat",
            Utc = timeProvider.GetUtcNow(),
        }, JsonOptions), ctx.RequestAborted).ConfigureAwait(false);

        try
        {
            await DrainAdminEventsSocketAsync(ws, ctx.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            lock (adminEventConnectionsGate)
            {
                adminEventConnections.Remove(connection);
            }

            connection.Dispose();
        }
    }

    private async Task HandleAdminFramesWebSocketAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!await TryAcceptAdminWebSocketAsync(ctx).ConfigureAwait(false))
        {
            return;
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var receiveBuffer = ArrayPool<byte>.Shared.Rent(8192);
        var messageBuffer = ArrayPool<byte>.Shared.Rent(runtimeConfig.MaxWebSocketMessageBytes);
        try
        {
            while (!ctx.RequestAborted.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var messageLength = 0;
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, 0, receiveBuffer.Length), ctx.RequestAborted).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if (messageLength + result.Count > runtimeConfig.MaxWebSocketMessageBytes)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message_too_big", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }

                    if (result.Count > 0)
                    {
                        receiveBuffer.AsSpan(0, result.Count).CopyTo(messageBuffer.AsSpan(messageLength));
                        messageLength += result.Count;
                    }
                }
                while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    continue;
                }

                TryDispatchAdminFrameEnvelope(messageBuffer.AsSpan(0, messageLength));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(receiveBuffer);
            ArrayPool<byte>.Shared.Return(messageBuffer);
        }
    }

    private async Task BroadcastAdminEventAsync(AdminEventMessage message)
    {
        AdminEventConnection[] targets;
        lock (adminEventConnectionsGate)
        {
            targets = adminEventConnections.ToArray();
        }

        if (targets.Length == 0)
        {
            return;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var dead = new List<AdminEventConnection>();
        foreach (var target in targets)
        {
            if (!await target.TrySendAsync(payload, CancellationToken.None).ConfigureAwait(false))
            {
                dead.Add(target);
            }
        }

        if (dead.Count == 0)
        {
            return;
        }

        lock (adminEventConnectionsGate)
        {
            foreach (var connection in dead)
            {
                adminEventConnections.Remove(connection);
            }
        }

        foreach (var connection in dead)
        {
            connection.Dispose();
        }
    }

    private async Task<bool> TryAcceptAdminWebSocketAsync(HttpContext ctx)
    {
        if (!runtimeConfig.IsAdminApiEnabled)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsJsonAsync(new { error = "admin_api_disabled" }, ctx.RequestAborted).ConfigureAwait(false);
            return false;
        }

        if (!TryAuthenticateAdmin(ctx))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "admin_unauthorized" }, ctx.RequestAborted).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private bool TryRejectAdminRequest(HttpContext ctx, out IResult rejected)
    {
        if (!runtimeConfig.IsAdminApiEnabled)
        {
            rejected = Results.Json(new { error = "admin_api_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            return true;
        }

        if (!TryAuthenticateAdmin(ctx))
        {
            rejected = Results.Json(new { error = "admin_unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            return true;
        }

        rejected = null!;
        return false;
    }

    private bool TryAuthenticateAdmin(HttpContext ctx)
    {
        var token = ctx.Request.Headers["X-Mica-Admin-Token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = auth[7..].Trim();
            }
        }

        return TokensMatchConstantTime(runtimeConfig.AdminToken, token);
    }

    private bool TryDispatchAdminFrameEnvelope(ReadOnlySpan<byte> envelope)
    {
        if (envelope.Length < 3)
        {
            return false;
        }

        var mode = envelope[0];
        var deviceIdLength = envelope[1] | (envelope[2] << 8);
        if (deviceIdLength < 0 || 3 + deviceIdLength > envelope.Length)
        {
            return false;
        }

        var deviceId = deviceIdLength == 0
            ? string.Empty
            : Encoding.UTF8.GetString(envelope.Slice(3, deviceIdLength));
        var payloadOffset = 3 + deviceIdLength;
        var payloadSpan = envelope[payloadOffset..];
        if (payloadSpan.Length == 0)
        {
            return false;
        }

        var payload = payloadSpan.ToArray();
        switch (mode)
        {
            case 0:
                BroadcastFrame(payload);
                return true;
            case 1 when !string.IsNullOrWhiteSpace(deviceId):
                // Mark this device as having an active WinUI client compositor so
                // PanelCompositorHostedService yields rendering for the next 200 ms.
                RecordAdminFrameForDevice(deviceId);
                SendFrame(deviceId, payload);
                return true;
            default:
                return false;
        }
    }

    private static async Task DrainAdminEventsSocketAsync(WebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
            }
            while (!result.EndOfMessage);
        }
    }
}
