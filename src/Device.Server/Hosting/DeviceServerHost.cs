using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using System.Globalization;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
public sealed partial class DeviceServerHost : IDeviceServerHost
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string PairRatePolicy = "pairing";
    private const string CommandAckRatePolicy = "command-ack";
    private const string WebSocketHandshakeRatePolicy = "ws-handshake";
    private static readonly TimeSpan SocketDetachGracePeriod = TimeSpan.FromMilliseconds(500);


    private enum AuthContext
    {
        HttpApi,
        WebSocket,
    }

    private readonly object gate = new();
    private readonly Dictionary<string, DeviceState> devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> pairingCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PairingAttemptWindow> pairingAttemptsByIp = new(StringComparer.OrdinalIgnoreCase);

    private ServerConfig config = new();
    private TimeSpan deviceOfflineTimeout = TimeSpan.FromSeconds(15);
    private WebApplication? app;
    private CancellationTokenSource? appCts;
    public event EventHandler? DevicesChanged;
    public event EventHandler<string>? LogMessage;

    public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    public async Task StartAsync(ServerConfig config, CancellationToken cancellationToken = default)
    {
        // DOCS: docs/wiki/modules/device-server-protocol.md#fluxo-de-execucao
        lock (gate)
        {
            if (app is not null)
            {
                return;
            }

            this.config = config;
            appCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        var runtimeConfig = this.config;
        deviceOfflineTimeout = TimeSpan.FromSeconds(Math.Clamp(runtimeConfig.DeviceFreshThresholdSeconds, 5, 120));
        var allowedCidrs = ParseAllowedCidrs(runtimeConfig.AllowedCidrs);
        var maxJsonBodyBytes = NormalizeMaxJsonBodyBytes(runtimeConfig.MaxJsonBodyBytes);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = maxJsonBodyBytes;
        });
        builder.WebHost.UseUrls($"http://{runtimeConfig.ListenHost}:{runtimeConfig.Port}");
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PairRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: BuildRateLimitPartitionKey(context.Connection.RemoteIpAddress),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, runtimeConfig.PairRequestsPerMinute),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(CommandAckRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: BuildRateLimitPartitionKey(context.Connection.RemoteIpAddress),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, runtimeConfig.CommandAckRequestsPerSecond),
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(WebSocketHandshakeRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: BuildRateLimitPartitionKey(context.Connection.RemoteIpAddress),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, runtimeConfig.WebSocketHandshakesPerMinute),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });

        var localApp = builder.Build();
        localApp.UseRateLimiter();
        localApp.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            await next().ConfigureAwait(false);
        });
        localApp.UseWebSockets();

        localApp.Use(async (ctx, next) =>
        {
            if (!IsRequestAllowed(runtimeConfig, allowedCidrs, ctx.Connection.RemoteIpAddress))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { error = "network_not_allowed" }).ConfigureAwait(false);
                return;
            }

            await next().ConfigureAwait(false);
        });

        localApp.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));

        localApp.MapGet("/api/v1/server/info", (HttpContext ctx) =>
        {
            var host = ResolveHost(ctx);
            return Results.Ok(new ServerInfoResponse
            {
                HttpBase = $"http://{host}:{this.config.Port}",
                MdnsService = this.config.MdnsServiceName,
                MaxDevices = this.config.MaxDevices,
                WsPath = "/ws/v1/stream",
            });
        });

        localApp.MapPost("/api/v1/pair", (Delegate)HandlePairAsync)
            .RequireRateLimiting(PairRatePolicy);
        localApp.MapGet("/api/v1/device/config", (Delegate)HandleDeviceConfig);
        localApp.MapPost("/api/v1/device/command-ack", (Delegate)HandleCommandAckAsync)
            .RequireRateLimiting(CommandAckRatePolicy);
        localApp.Map("/ws/v1/stream", (RequestDelegate)HandleWebSocketAsync)
            .RequireRateLimiting(WebSocketHandshakeRatePolicy);

        await localApp.StartAsync(appCts!.Token).ConfigureAwait(false);
        lock (gate)
        {
            app = localApp;
        }

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        if (runtimeConfig.AllowedCidrs.Length > 0 && allowedCidrs.Count == 0)
        {
            Log("Servidor iniciado sem CIDR valido em AllowedCidrs; aplicando regra padrao de rede privada.");
        }

        Log($"Servidor de dispositivos ativo em http://{runtimeConfig.ListenHost}:{runtimeConfig.Port}");
    }

    public async Task StopAsync()
    {
        WebApplication? localApp;
        CancellationTokenSource? localCts;
        DeviceState[] statesToDispose;
        PendingTrackedCommand[] pendingToCancel;

        lock (gate)
        {
            localApp = app;
            localCts = appCts;
            app = null;
            appCts = null;
            pendingToCancel = pendingTrackedCommands.Values.ToArray();
            pendingTrackedCommands.Clear();
        }

        foreach (var pending in pendingToCancel)
        {
            pending.TrySetResult(new CommandDispatchResult
            {
                DeviceId = pending.DeviceId,
                CommandId = pending.CommandId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = pending.LastPercent,
                Stage = "server-stopped",
                Message = "Servidor interrompido durante execucao do comando.",
                ErrorCode = "server_stopped",
            });
        }

        if (localApp is null)
        {
            return;
        }

        try
        {
            localCts?.Cancel();
            await localApp.StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore shutdown races
        }
        finally
        {
            localCts?.Dispose();
            lock (gate)
            {
                statesToDispose = devices.Values.ToArray();
                devices.Clear();
                pairingCodes.Clear();
                pairingAttemptsByIp.Clear();
            }

            foreach (var state in statesToDispose)
            {
                state.Dispose();
            }

            NotifyDevicesChanged();
        }

        Log("Servidor de dispositivos parado");
    }
    public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString(CultureInfo.InvariantCulture);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        lock (gate)
        {
            CleanupPairingCodesLocked();
            pairingCodes[code] = expiresAt;
        }

        Log($"Codigo de pareamento gerado (expira em {ttl.TotalSeconds:0}s).");
        return new PairingCodeInfo { Code = code, ExpiresAtUtc = expiresAt };
    }

    public IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot()
    {
        lock (gate)
        {
            return devices.Values.Select(d => d.ToSnapshot(deviceOfflineTimeout)).OrderByDescending(d => d.LastSeenUtc).ToArray();
        }
    }

    public IReadOnlyList<DeviceRecord> GetDeviceRecords()
    {
        lock (gate)
        {
            return devices.Values.Select(d => d.Record).OrderByDescending(d => d.LastSeenUtc).ToArray();
        }
    }

    public void SeedDevices(IEnumerable<DeviceRecord> devices)
    {
        lock (gate)
        {
            foreach (var record in devices)
            {
                if (string.IsNullOrWhiteSpace(record.DeviceId) || string.IsNullOrWhiteSpace(record.Token))
                {
                    continue;
                }

                this.devices[record.DeviceId] = new DeviceState(record);
            }
        }

        NotifyDevicesChanged();
    }

    public async Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default)
    {
        var result = await SendTrackedCommandCoreAsync(deviceId, commandType, null, DefaultCommandTimeout, cancellationToken).ConfigureAwait(false);
        return result.Accepted;
    }

    // DOCS: docs/wiki/guides/add-device-command.md#passos
    public Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return SendCommandTrackedAsync(deviceId, commandType, parameters: null, timeout, cancellationToken);
    }

    // DOCS: docs/wiki/guides/add-device-command.md#passos
    public Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout.GetValueOrDefault(DefaultCommandTimeout);
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            effectiveTimeout = DefaultCommandTimeout;
        }

        return SendTrackedCommandCoreAsync(deviceId, commandType, parameters, effectiveTimeout, cancellationToken);
    }

    public bool RemoveDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        DeviceState? removedState = null;
        try
        {
            lock (gate)
            {
                if (!devices.Remove(deviceId, out removedState))
                {
                    return false;
                }
            }

            NotifyDevicesChanged();
            Log($"Device removido: {deviceId}");
            return true;
        }
        finally
        {
            removedState?.Dispose();
        }
    }

    public void BroadcastFrame(byte[] framePayload)
    {
        DeviceState[] targets;
        lock (gate)
        {
            targets = devices.Values.Where(d => d.Socket is { State: WebSocketState.Open }).ToArray();
        }

        foreach (var target in targets)
        {
            target.QueueFrame(framePayload);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task<IResult> HandlePairAsync(HttpContext ctx)
    {
        var remoteIpKey = BuildRateLimitPartitionKey(ctx.Connection.RemoteIpAddress);
        if (!TryRegisterPairingAttempt(remoteIpKey, out var retryAfterSeconds))
        {
            return Results.Json(new { error = "pairing_rate_limited", retryAfterSeconds }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (IsRequestBodyTooLarge(ctx, config.MaxJsonBodyBytes))
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        PairDeviceRequest req;
        try
        {
            req = await JsonSerializer.DeserializeAsync<PairDeviceRequest>(ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false)
                ?? new PairDeviceRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        if (string.IsNullOrWhiteSpace(req.PairingCode) || !TryConsumePairingCode(req.PairingCode))
        {
            return Results.BadRequest(new { error = "invalid_or_expired_pairing_code" });
        }

        DeviceState state;
        lock (gate)
        {
            if (devices.Count >= config.MaxDevices)
            {
                return Results.BadRequest(new { error = "max_devices_reached" });
            }

            var id = $"mp-{Guid.NewGuid():N}";
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
            var record = new DeviceRecord
            {
                DeviceId = id,
                Token = token,
                Name = string.IsNullOrWhiteSpace(req.DeviceName) ? ResolveDefaultDeviceName(req.BoardModel) : req.DeviceName.Trim(),
                Profile = NormalizeFirmwareProfile(req.Profile),
                FirmwareVersion = req.FirmwareVersion,
                LastKnownIp = ctx.Connection.RemoteIpAddress?.ToString(),
                LastSeenUtc = DateTimeOffset.UtcNow,
                BoardModel = NormalizeOptional(req.BoardModel),
                PanelType = NormalizeOptional(req.PanelType),
            };

            state = new DeviceState(record);
            devices[id] = state;
            pairingAttemptsByIp.Remove(remoteIpKey);
        }

        NotifyDevicesChanged();
        Log($"Device pareado: {state.Record.DeviceId}");

        var host = ResolveHost(ctx);
        return Results.Ok(new PairDeviceResponse
        {
            DeviceId = state.Record.DeviceId,
            Token = state.Record.Token,
            WsPath = "/ws/v1/stream",
            HttpBase = $"http://{host}:{config.Port}",
            MdnsService = config.MdnsServiceName,
        });
    }

    private IResult HandleDeviceConfig(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, AuthContext.HttpApi, out var state))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new DeviceConfigResponse
        {
            DeviceId = state.Record.DeviceId,
            Name = state.Record.Name,
            MatrixWidth = 128,
            MatrixHeight = 64,
            StreamMode = "bins128",
            MdnsService = config.MdnsServiceName,
        });
    }

    private async Task<IResult> HandleCommandAckAsync(HttpContext ctx)
    {
        return await HandleCommandAckTrackedAsync(ctx).ConfigureAwait(false);
    }

    private async Task HandleWebSocketAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!TryAuthenticate(ctx, AuthContext.WebSocket, out var state))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var ws = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        state.MarkAuthenticated();
        state.AttachSocket(ws, ctx.Connection.RemoteIpAddress?.ToString());
        NotifyDevicesChanged();

        var sendToken = state.SendToken;
        var sendTask = Task.Run(() => SendLoopAsync(state, ws, sendToken));
        try
        {
            await ReceiveLoopAsync(state, ws, config.MaxWebSocketMessageBytes, ctx.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            if (state.DetachSocket(ws))
            {
                NotifyDevicesChanged();
            }

            await sendTask.ConfigureAwait(false);
        }
    }

    private static async Task SendLoopAsync(DeviceState state, WebSocket ws, CancellationToken sendToken)
    {
        while (true)
        {
            byte[] payload;
            try
            {
                payload = await state.Outgoing.Reader.ReadAsync(sendToken).ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            if (ws.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                await ws.SendAsync(payload, WebSocketMessageType.Binary, true, sendToken).ConfigureAwait(false);
            }
            catch
            {
                // ignore transient socket errors
            }
        }
    }

    private async Task ReceiveLoopAsync(DeviceState state, WebSocket ws, int maxMessageSize, CancellationToken cancellationToken)
    {
        var effectiveMaxMessageSize = NormalizeMaxWebSocketMessageBytes(maxMessageSize);
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            using var messageBuffer = new MemoryStream();
            WebSocketReceiveResult? finalResult = null;

            do
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                if (result.Count > 0)
                {
                    messageBuffer.Write(buffer, 0, result.Count);
                }

                if (messageBuffer.Length > effectiveMaxMessageSize)
                {
                    Log($"Mensagem WS excedeu {effectiveMaxMessageSize} bytes de {state.Record.DeviceId}. Encerrando conexao.");
                    try
                    {
                        if (ws.State == WebSocketState.Open)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message_too_big", CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // ignore close races
                    }

                    return;
                }

                finalResult = result;
            }
            while (finalResult is not null && !finalResult.EndOfMessage);

            if (finalResult is null || finalResult.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);
            var handled = await HandleIncomingWsTextAsync(state, json).ConfigureAwait(false);
            if (!handled)
            {
                Log($"Mensagem WS invalida recebida de {state.Record.DeviceId} (bytes={messageBuffer.Length}).");
            }

            NotifyDevicesChanged();
        }
    }

    private bool TryAuthenticate(HttpContext ctx, AuthContext authContext, out DeviceState state)
    {
        state = null!;

        var deviceId = ctx.Request.Headers["X-Device-Id"].ToString();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = ctx.Request.Query["deviceId"].ToString();
        }

        var token = ctx.Request.Headers["X-Device-Token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var auth = ctx.Request.Headers.Authorization.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = auth[7..].Trim();
            }
        }

        if (authContext == AuthContext.WebSocket
            && string.IsNullOrWhiteSpace(token)
            && config.AllowLegacyWebSocketQueryToken)
        {
            var legacyQueryToken = ctx.Request.Query["token"].ToString();
            if (!string.IsNullOrWhiteSpace(legacyQueryToken))
            {
                token = legacyQueryToken;
                Log($"Autenticacao WS via query-string em uso por {deviceId}. Migre para header X-Device-Token.");
            }
        }

        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        lock (gate)
        {
            if (!devices.TryGetValue(deviceId, out var foundState) || foundState is null)
            {
                return false;
            }

            state = foundState;
            return TokensMatchConstantTime(state.Record.Token, token);
        }
    }

    private static bool TokensMatchConstantTime(string expectedToken, string providedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken) || string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedToken),
            Encoding.UTF8.GetBytes(providedToken));
    }

    private static bool IsRequestBodyTooLarge(HttpContext ctx, long configuredMaxBodyBytes)
    {
        if (!ctx.Request.ContentLength.HasValue)
        {
            return false;
        }

        return ctx.Request.ContentLength.Value > NormalizeMaxJsonBodyBytes(configuredMaxBodyBytes);
    }

    private static long NormalizeMaxJsonBodyBytes(long configuredMaxBodyBytes)
    {
        return Math.Clamp(configuredMaxBodyBytes, 1024L, 1024L * 1024L);
    }

    private static int NormalizeMaxWebSocketMessageBytes(int configuredMaxMessageBytes)
    {
        return Math.Clamp(configuredMaxMessageBytes, 1024, 1024 * 1024);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ResolveDefaultDeviceName(string? boardModel)
    {
        var normalized = NormalizeOptional(boardModel);
        if (string.Equals(normalized, "esp32s3_devkitc1", StringComparison.OrdinalIgnoreCase))
        {
            return "ESP32-S3 DevKitC-1";
        }

        return "ESP32-S3 DevKitC-1";
    }

    private bool TryConsumePairingCode(string code)
    {
        lock (gate)
        {
            CleanupPairingCodesLocked();
            if (!pairingCodes.TryGetValue(code, out var expiresAt))
            {
                return false;
            }

            pairingCodes.Remove(code);
            return expiresAt > DateTimeOffset.UtcNow;
        }
    }

    private void CleanupPairingCodesLocked()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var expired in pairingCodes.Where(kv => kv.Value <= now).ToArray())
        {
            pairingCodes.Remove(expired.Key);
        }
    }

    private bool TryRegisterPairingAttempt(string remoteIpKey, out int retryAfterSeconds)
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            var window = TimeSpan.FromSeconds(Math.Max(10, config.PairingAttemptWindowSeconds));
            var maxAttempts = Math.Max(1, config.PairingAttemptsPerWindow);
            CleanupPairingAttemptsLocked(now, window);

            if (!pairingAttemptsByIp.TryGetValue(remoteIpKey, out var current)
                || (now - current.WindowStartUtc) >= window)
            {
                current = new PairingAttemptWindow(now, 0);
            }

            if (current.Attempts >= maxAttempts)
            {
                var wait = (current.WindowStartUtc + window) - now;
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
                return false;
            }

            pairingAttemptsByIp[remoteIpKey] = current with { Attempts = current.Attempts + 1 };
            retryAfterSeconds = 0;
            return true;
        }
    }

    private void CleanupPairingAttemptsLocked(DateTimeOffset now, TimeSpan window)
    {
        foreach (var stale in pairingAttemptsByIp.Where(kv => (now - kv.Value.WindowStartUtc) > window).ToArray())
        {
            pairingAttemptsByIp.Remove(stale.Key);
        }
    }

    private static string BuildRateLimitPartitionKey(IPAddress? remoteIp)
    {
        if (remoteIp is null)
        {
            return "unknown";
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        return remoteIp.ToString();
    }

    private static bool IsRequestAllowed(ServerConfig runtimeConfig, IReadOnlyList<CidrRange> allowedCidrs, IPAddress? remoteIp)
    {
        if (remoteIp is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        if (allowedCidrs.Count > 0)
        {
            return allowedCidrs.Any(cidr => cidr.Contains(remoteIp));
        }

        if (!runtimeConfig.RestrictToPrivateNetworks)
        {
            return true;
        }

        return IsPrivateNetworkAddress(remoteIp);
    }

    private static bool IsPrivateNetworkAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];

            var isUniqueLocal = (first & 0xFE) == 0xFC;
            var isLinkLocal = first == 0xFE && (second & 0xC0) == 0x80;
            return isUniqueLocal || isLinkLocal;
        }

        return false;
    }

    private static IReadOnlyList<CidrRange> ParseAllowedCidrs(IEnumerable<string>? cidrValues)
    {
        if (cidrValues is null)
        {
            return Array.Empty<CidrRange>();
        }

        var parsed = new List<CidrRange>();
        foreach (var raw in cidrValues)
        {
            if (TryParseCidr(raw, out var cidr))
            {
                parsed.Add(cidr);
            }
        }

        return parsed;
    }

    private static bool TryParseCidr(string? rawValue, out CidrRange cidr)
    {
        cidr = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var parts = rawValue.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var prefixLength = 32;
        if (parts.Length == 2 && !int.TryParse(parts[1], out prefixLength))
        {
            return false;
        }

        if (prefixLength < 0 || prefixLength > 32)
        {
            return false;
        }

        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var network = ConvertIpv4ToUInt32(address) & mask;
        cidr = new CidrRange(network, prefixLength);
        return true;
    }

    private static uint ConvertIpv4ToUInt32(IPAddress ipv4)
    {
        var bytes = ipv4.GetAddressBytes();
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
    }

    private string ResolveHost(HttpContext ctx)
    {
        var requestHost = ctx.Request.Host.Host;
        if (!string.IsNullOrWhiteSpace(requestHost)
            && !string.Equals(requestHost, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(requestHost, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return requestHost;
        }

        if (!string.IsNullOrWhiteSpace(config.PublicHost))
        {
            return config.PublicHost;
        }

        return ctx.Connection.LocalIpAddress?.ToString() ?? "127.0.0.1";
    }

    private void NotifyDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    private void Log(string message) => LogMessage?.Invoke(this, message);

    private readonly record struct PairingAttemptWindow(DateTimeOffset WindowStartUtc, int Attempts);

    private readonly struct CidrRange
    {
        public CidrRange(uint network, int prefixLength)
        {
            Network = network;
            PrefixLength = prefixLength;
        }

        public uint Network { get; }

        public int PrefixLength { get; }

        public bool Contains(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            var value = ConvertIpv4ToUInt32(address);
            var mask = PrefixLength == 0 ? 0u : uint.MaxValue << (32 - PrefixLength);
            return (value & mask) == Network;
        }
    }
    private static string NormalizeFirmwareProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return "dma_exp";
        }
        var normalized = profile.Trim();
        return string.Equals(normalized, "stable", StringComparison.OrdinalIgnoreCase)
            ? "dma_exp"
            : normalized;
    }
    private sealed class DeviceState : IDisposable
    {
        private CancellationTokenSource senderCts = new();
        private DateTimeOffset? socketDetachGraceUntilUtc;

        public DeviceState(DeviceRecord record)
        {
            Record = record;
            LastActivityUtc = record.LastSeenUtc != default && record.LastSeenUtc != DateTimeOffset.MinValue
                ? record.LastSeenUtc
                : record.CreatedAtUtc;
            Outgoing = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });
        }

        public DeviceRecord Record { get; private set; }

        public WebSocket? Socket { get; private set; }

        public Channel<byte[]> Outgoing { get; }

        public DateTimeOffset LastActivityUtc { get; private set; }

        public CancellationToken SendToken => senderCts.Token;

        public void MarkSeen(
            string? ip,
            int? rssi,
            string? firmwareVersion,
            string? activeAppId = null,
            string? activeAppName = null,
            string? boardModel = null,
            string? panelType = null)
        {
            var now = DateTimeOffset.UtcNow;
            LastActivityUtc = now;
            Record = new DeviceRecord
            {
                DeviceId = Record.DeviceId,
                Name = Record.Name,
                Profile = Record.Profile,
                Token = Record.Token,
                CreatedAtUtc = Record.CreatedAtUtc,
                LastSeenUtc = now,
                LastKnownIp = string.IsNullOrWhiteSpace(ip) ? Record.LastKnownIp : ip,
                LastKnownRssi = rssi ?? Record.LastKnownRssi,
                FirmwareVersion = string.IsNullOrWhiteSpace(firmwareVersion) ? Record.FirmwareVersion : firmwareVersion,
                UptimeSeconds = Record.UptimeSeconds,
                LoopLoadPercent = Record.LoopLoadPercent,
                FreeHeapBytes = Record.FreeHeapBytes,
                LargestHeapBlockBytes = Record.LargestHeapBlockBytes,
                PsramAvailable = Record.PsramAvailable,
                FreePsramBytes = Record.FreePsramBytes,
                LargestPsramBlockBytes = Record.LargestPsramBlockBytes,
                WifiConnected = Record.WifiConnected,
                WifiState = Record.WifiState,
                ProvisioningPortalActive = Record.ProvisioningPortalActive,
                AuxLedAvailable = Record.AuxLedAvailable,
                TestLedAvailable = Record.TestLedAvailable,
                LastWifiEvent = Record.LastWifiEvent,
                StreamLastSequence = Record.StreamLastSequence,
                StreamFramesReceived = Record.StreamFramesReceived,
                StreamFramesApplied = Record.StreamFramesApplied,
                StreamSequenceGapCount = Record.StreamSequenceGapCount,
                StreamInvalidFrameCount = Record.StreamInvalidFrameCount,
                TelemetrySequence = Record.TelemetrySequence,
                BrightnessCap = Record.BrightnessCap,
                BrightnessRequested = Record.BrightnessRequested,
                BrightnessApplied = Record.BrightnessApplied,
                TestLedEnabled = Record.TestLedEnabled,
                TestLedDuty = Record.TestLedDuty,
                ActiveAppId = string.IsNullOrWhiteSpace(activeAppId) ? Record.ActiveAppId : activeAppId,
                ActiveAppName = string.IsNullOrWhiteSpace(activeAppName) ? Record.ActiveAppName : activeAppName,
                BoardModel = string.IsNullOrWhiteSpace(boardModel) ? Record.BoardModel : boardModel,
                PanelType = string.IsNullOrWhiteSpace(panelType) ? Record.PanelType : panelType,
                IsRegistered = Record.IsRegistered,
                FirstSeenUtc = Record.FirstSeenUtc,
                LastTelemetryUtc = Record.LastTelemetryUtc,
                LastAuthUtc = Record.LastAuthUtc,
                ConfigState = Record.ConfigState,
            };
        }

        public void MarkAuthenticated()
        {
            var now = DateTimeOffset.UtcNow;
            Record = new DeviceRecord
            {
                DeviceId = Record.DeviceId,
                Name = Record.Name,
                Profile = Record.Profile,
                Token = Record.Token,
                CreatedAtUtc = Record.CreatedAtUtc,
                LastSeenUtc = Record.LastSeenUtc,
                LastKnownIp = Record.LastKnownIp,
                LastKnownRssi = Record.LastKnownRssi,
                FirmwareVersion = Record.FirmwareVersion,
                UptimeSeconds = Record.UptimeSeconds,
                LoopLoadPercent = Record.LoopLoadPercent,
                FreeHeapBytes = Record.FreeHeapBytes,
                LargestHeapBlockBytes = Record.LargestHeapBlockBytes,
                PsramAvailable = Record.PsramAvailable,
                FreePsramBytes = Record.FreePsramBytes,
                LargestPsramBlockBytes = Record.LargestPsramBlockBytes,
                WifiConnected = Record.WifiConnected,
                WifiState = Record.WifiState,
                ProvisioningPortalActive = Record.ProvisioningPortalActive,
                AuxLedAvailable = Record.AuxLedAvailable,
                TestLedAvailable = Record.TestLedAvailable,
                LastWifiEvent = Record.LastWifiEvent,
                StreamLastSequence = Record.StreamLastSequence,
                StreamFramesReceived = Record.StreamFramesReceived,
                StreamFramesApplied = Record.StreamFramesApplied,
                StreamSequenceGapCount = Record.StreamSequenceGapCount,
                StreamInvalidFrameCount = Record.StreamInvalidFrameCount,
                TelemetrySequence = Record.TelemetrySequence,
                BrightnessCap = Record.BrightnessCap,
                BrightnessRequested = Record.BrightnessRequested,
                BrightnessApplied = Record.BrightnessApplied,
                TestLedEnabled = Record.TestLedEnabled,
                TestLedDuty = Record.TestLedDuty,
                ActiveAppId = Record.ActiveAppId,
                ActiveAppName = Record.ActiveAppName,
                BoardModel = Record.BoardModel,
                PanelType = Record.PanelType,
                IsRegistered = true,
                FirstSeenUtc = Record.FirstSeenUtc ?? now,
                LastTelemetryUtc = Record.LastTelemetryUtc,
                LastAuthUtc = now,
                ConfigState = DeviceConfigState.KnownGood,
            };
        }

        public void MarkTelemetry(
            string? ip,
            int? rssi,
            string? firmwareVersion,
            string? activeAppId = null,
            string? activeAppName = null,
            string? boardModel = null,
            string? panelType = null,
            int? uptimeSeconds = null,
            int? loopLoadPercent = null,
            long? freeHeapBytes = null,
            long? largestHeapBlockBytes = null,
            bool? psramAvailable = null,
            long? freePsramBytes = null,
            long? largestPsramBlockBytes = null,
            bool? wifiConnected = null,
            string? wifiState = null,
            bool? provisioningPortalActive = null,
            bool? auxLedAvailable = null,
            bool? testLedAvailable = null,
            string? lastWifiEvent = null,
            uint? streamLastSequence = null,
            uint? streamFramesReceived = null,
            uint? streamFramesApplied = null,
            uint? streamSequenceGapCount = null,
            uint? streamInvalidFrameCount = null,
            uint? telemetrySequence = null,
            int? brightnessCap = null,
            int? brightnessRequested = null,
            int? brightnessApplied = null,
            bool? testLedEnabled = null,
            int? testLedDuty = null)
        {
            var now = DateTimeOffset.UtcNow;
            LastActivityUtc = now;
            Record = new DeviceRecord
            {
                DeviceId = Record.DeviceId,
                Name = Record.Name,
                Profile = Record.Profile,
                Token = Record.Token,
                CreatedAtUtc = Record.CreatedAtUtc,
                LastSeenUtc = now,
                LastKnownIp = string.IsNullOrWhiteSpace(ip) ? Record.LastKnownIp : ip,
                LastKnownRssi = rssi ?? Record.LastKnownRssi,
                FirmwareVersion = string.IsNullOrWhiteSpace(firmwareVersion) ? Record.FirmwareVersion : firmwareVersion,
                UptimeSeconds = uptimeSeconds,
                LoopLoadPercent = loopLoadPercent,
                FreeHeapBytes = freeHeapBytes,
                LargestHeapBlockBytes = largestHeapBlockBytes,
                PsramAvailable = psramAvailable,
                FreePsramBytes = freePsramBytes,
                LargestPsramBlockBytes = largestPsramBlockBytes,
                WifiConnected = wifiConnected,
                WifiState = wifiState,
                ProvisioningPortalActive = provisioningPortalActive,
                AuxLedAvailable = auxLedAvailable,
                TestLedAvailable = testLedAvailable,
                LastWifiEvent = lastWifiEvent,
                StreamLastSequence = streamLastSequence,
                StreamFramesReceived = streamFramesReceived,
                StreamFramesApplied = streamFramesApplied,
                StreamSequenceGapCount = streamSequenceGapCount,
                StreamInvalidFrameCount = streamInvalidFrameCount,
                TelemetrySequence = telemetrySequence,
                BrightnessCap = brightnessCap,
                BrightnessRequested = brightnessRequested,
                BrightnessApplied = brightnessApplied,
                TestLedEnabled = testLedEnabled,
                TestLedDuty = testLedDuty,
                ActiveAppId = string.IsNullOrWhiteSpace(activeAppId) ? Record.ActiveAppId : activeAppId,
                ActiveAppName = string.IsNullOrWhiteSpace(activeAppName) ? Record.ActiveAppName : activeAppName,
                BoardModel = string.IsNullOrWhiteSpace(boardModel) ? Record.BoardModel : boardModel,
                PanelType = string.IsNullOrWhiteSpace(panelType) ? Record.PanelType : panelType,
                IsRegistered = true,
                FirstSeenUtc = Record.FirstSeenUtc ?? now,
                LastTelemetryUtc = now,
                LastAuthUtc = Record.LastAuthUtc,
                ConfigState = Record.ConfigState,
            };
        }

        public void Touch()
        {
            LastActivityUtc = DateTimeOffset.UtcNow;
        }

        public void AttachSocket(WebSocket socket, string? ip)
        {
            senderCts.Cancel();
            senderCts.Dispose();
            senderCts = new CancellationTokenSource();
            Socket = socket;
            socketDetachGraceUntilUtc = null;
            MarkSeen(ip, Record.LastKnownRssi, Record.FirmwareVersion);
        }

        public bool DetachSocket(WebSocket socket)
        {
            if (!ReferenceEquals(Socket, socket))
            {
                return false;
            }

            senderCts.Cancel();
            Socket = null;
            socketDetachGraceUntilUtc = DateTimeOffset.UtcNow + SocketDetachGracePeriod;
            return true;
        }

        public void QueueFrame(byte[] frame)
        {
            Outgoing.Writer.TryWrite(frame);
        }

        public DeviceSnapshot ToSnapshot(TimeSpan offlineTimeout)
        {
            var now = DateTimeOffset.UtcNow;
            var onlineBySocket = Socket is { State: WebSocketState.Open } && (now - LastActivityUtc) <= offlineTimeout;

            var withinDetachGrace = false;
            if (socketDetachGraceUntilUtc.HasValue)
            {
                if (now <= socketDetachGraceUntilUtc.Value)
                {
                    withinDetachGrace = true;
                }
                else
                {
                    socketDetachGraceUntilUtc = null;
                }
            }

            var online = onlineBySocket || withinDetachGrace;

            return new DeviceSnapshot
            {
                DeviceId = Record.DeviceId,
                Name = Record.Name,
                Profile = Record.Profile,
                Status = online ? DeviceStatus.Online : DeviceStatus.Offline,
                LastSeenUtc = Record.LastSeenUtc,
                LastKnownIp = Record.LastKnownIp,
                LastKnownRssi = Record.LastKnownRssi,
                UptimeSeconds = Record.UptimeSeconds,
                LoopLoadPercent = Record.LoopLoadPercent,
                FreeHeapBytes = Record.FreeHeapBytes,
                LargestHeapBlockBytes = Record.LargestHeapBlockBytes,
                PsramAvailable = Record.PsramAvailable,
                FreePsramBytes = Record.FreePsramBytes,
                LargestPsramBlockBytes = Record.LargestPsramBlockBytes,
                WifiConnected = Record.WifiConnected,
                WifiState = Record.WifiState,
                ProvisioningPortalActive = Record.ProvisioningPortalActive,
                AuxLedAvailable = Record.AuxLedAvailable,
                TestLedAvailable = Record.TestLedAvailable,
                LastWifiEvent = Record.LastWifiEvent,
                StreamLastSequence = Record.StreamLastSequence,
                StreamFramesReceived = Record.StreamFramesReceived,
                StreamFramesApplied = Record.StreamFramesApplied,
                StreamSequenceGapCount = Record.StreamSequenceGapCount,
                StreamInvalidFrameCount = Record.StreamInvalidFrameCount,
                FirmwareVersion = Record.FirmwareVersion,
                TelemetrySequence = Record.TelemetrySequence,
                BrightnessCap = Record.BrightnessCap,
                BrightnessRequested = Record.BrightnessRequested,
                BrightnessApplied = Record.BrightnessApplied,
                TestLedEnabled = Record.TestLedEnabled,
                TestLedDuty = Record.TestLedDuty,
                ActiveAppId = Record.ActiveAppId,
                ActiveAppName = Record.ActiveAppName,
                BoardModel = Record.BoardModel,
                PanelType = Record.PanelType,
                IsRegistered = Record.IsRegistered,
                FirstSeenUtc = Record.FirstSeenUtc,
                LastTelemetryUtc = Record.LastTelemetryUtc,
                LastAuthUtc = Record.LastAuthUtc,
                ConfigState = Record.ConfigState,
            };
        }

        public void Dispose()
        {
            senderCts.Cancel();
            senderCts.Dispose();

            if (Socket is not null)
            {
                try
                {
                    Socket.Abort();
                    Socket.Dispose();
                }
                catch
                {
                    // ignore socket disposal errors
                }

                Socket = null;
            }

            Outgoing.Writer.TryComplete();
        }
    }
}
