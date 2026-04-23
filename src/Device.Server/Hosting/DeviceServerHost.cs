using System.Buffers;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Protocol.Stream;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.FileProviders;
using MQTTnet.Server;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/handoffs/2026-04-22-device-server-panels-batch-storage.md
// DOCS: docs/handoffs/2026-04-22-device-server-pairing-store.md
// DOCS: docs/handoffs/2026-04-22-device-server-command-state-store.md
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-22-micaudio-server-docker-advertised-endpoints.md
// DOCS: docs/handoffs/2026-04-23-micaudio-visual-transport-optimization.md
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
    private readonly TimeProvider timeProvider;
    private readonly IDeviceOfficialFirmwareCatalog? firmwareCatalog;
    private readonly IPanelsBatchStore panelsBatchStore;
    private readonly IDevicePairingStore pairingStore;
    private readonly ICommandStateStore commandStateStore;
    private readonly ISessionStateStore sessionStateStore;
    private readonly IVisualUdpSender visualUdpSender;
    private readonly DeviceFrameConnectionRegistry frameConnections = new();
    private readonly object adminEventConnectionsGate = new();
    private readonly List<AdminEventConnection> adminEventConnections = new();

    private DeviceServerRuntimeConfig runtimeConfig = DeviceServerRuntimeConfig.From(new ServerConfig());
    private WebApplication? app;
    private MqttServer? mqttServer;
    private CancellationTokenSource? appCts;

    public DeviceServerHost()
        : this(TimeProvider.System, firmwareCatalog: null)
    {
    }

    public DeviceServerHost(TimeProvider timeProvider)
        : this(timeProvider, firmwareCatalog: null)
    {
    }

    public DeviceServerHost(IDeviceOfficialFirmwareCatalog? firmwareCatalog)
        : this(TimeProvider.System, firmwareCatalog)
    {
    }

    public DeviceServerHost(
        TimeProvider timeProvider,
        IDeviceOfficialFirmwareCatalog? firmwareCatalog,
        IPanelsBatchStore? panelsBatchStore = null,
        IDevicePairingStore? pairingStore = null,
        ICommandStateStore? commandStateStore = null,
        ISessionStateStore? sessionStateStore = null)
        : this(
            timeProvider,
            firmwareCatalog,
            panelsBatchStore,
            pairingStore,
            commandStateStore,
            sessionStateStore,
            visualUdpSender: null)
    {
    }

    internal DeviceServerHost(
        TimeProvider timeProvider,
        IDeviceOfficialFirmwareCatalog? firmwareCatalog,
        IPanelsBatchStore? panelsBatchStore,
        IDevicePairingStore? pairingStore,
        ICommandStateStore? commandStateStore,
        ISessionStateStore? sessionStateStore,
        IVisualUdpSender? visualUdpSender)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.timeProvider = timeProvider;
        this.firmwareCatalog = firmwareCatalog;
        this.panelsBatchStore = panelsBatchStore ?? new InMemoryPanelsBatchStore();
        this.pairingStore = pairingStore ?? new InMemoryDevicePairingStore();
        this.commandStateStore = commandStateStore ?? new InMemoryCommandStateStore();
        this.sessionStateStore = sessionStateStore ?? new InMemorySessionStateStore();
        this.visualUdpSender = visualUdpSender ?? new SocketVisualUdpSender();
    }

    public event EventHandler? DevicesChanged;

    public event EventHandler<string>? LogMessage;

    public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    public event EventHandler<DeviceLogMessage>? DeviceLogReceived;

    public async Task StartAsync(ServerConfig config, CancellationToken cancellationToken = default)
    {
        // DOCS: docs/wiki/modules/device-server-protocol.md#fluxo-de-execucao
        ArgumentNullException.ThrowIfNull(config);

        var localRuntimeConfig = DeviceServerRuntimeConfig.From(config);
        lock (gate)
        {
            if (app is not null)
            {
                return;
            }

            runtimeConfig = localRuntimeConfig;
            appCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        var builder = WebApplication.CreateSlimBuilder();
        DeviceServerObservability.ConfigureLogging(builder.Logging);
        DeviceServerObservability.ConfigureOpenTelemetry(builder.Services);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = localRuntimeConfig.MaxJsonBodyBytes;
        });
        builder.WebHost.UseUrls($"http://{localRuntimeConfig.ListenHost}:{localRuntimeConfig.Port}");
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PairRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: BuildRateLimitPartitionKey(context.Connection.RemoteIpAddress),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = localRuntimeConfig.PairRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(CommandAckRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: BuildRateLimitPartitionKey(context.Connection.RemoteIpAddress),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = localRuntimeConfig.CommandAckRequestsPerSecond,
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(WebSocketHandshakeRatePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: BuildRateLimitPartitionKey(context.Connection.RemoteIpAddress),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = localRuntimeConfig.WebSocketHandshakesPerMinute,
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
            if (!IsRequestAllowed(localRuntimeConfig, ctx.Connection.RemoteIpAddress))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { error = "network_not_allowed" }).ConfigureAwait(false);
                return;
            }

            await next().ConfigureAwait(false);
        });
        localApp.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase)
                || ctx.Request.Path.Equals("/dashboard/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDashboardAsync(ctx).ConfigureAwait(false);
                return;
            }

            await next().ConfigureAwait(false);
        });
        localApp.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot")),
            OnPrepareResponse = static context => context.Context.Response.Headers["Cache-Control"] = "no-store",
        });

        MapRoutes(localApp);

        MqttServer? localMqttServer = null;
        try
        {
            localMqttServer = CreateMqttServer(localRuntimeConfig);
            await localMqttServer.StartAsync().ConfigureAwait(false);
            await localApp.StartAsync(appCts!.Token).ConfigureAwait(false);
        }
        catch
        {
            if (localMqttServer is not null)
            {
                await StopMqttServerAsync(localMqttServer).ConfigureAwait(false);
            }

            try
            {
                await localApp.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // ignore startup cleanup races
            }

            throw;
        }

        lock (gate)
        {
            app = localApp;
            mqttServer = localMqttServer;
        }

        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        if (localRuntimeConfig.HasConfiguredAllowedCidrs && localRuntimeConfig.AllowedCidrs.Count == 0)
        {
            Log("Servidor iniciado sem CIDR valido em AllowedCidrs; aplicando regra padrao de rede privada.");
        }

        Log($"HTTP bind interno: http://{localRuntimeConfig.ListenHost}:{localRuntimeConfig.Port}");
        Log($"HTTP anunciado: {ResolveAdvertisedHttpBaseAddress(localRuntimeConfig)}");
        Log($"MQTT anunciado: mqtt://{ResolveAdvertisedMqttHost(localRuntimeConfig)}:{localRuntimeConfig.MqttPort} ({localRuntimeConfig.MqttRootTopic}/{{deviceId}})");
        Log(localRuntimeConfig.PreferLanUdpVisualTransport
            ? $"UDP visual LAN habilitado: udp://{ResolveAdvertisedMqttHost(localRuntimeConfig)}:{localRuntimeConfig.VisualUdpPort} (bins128)"
            : "UDP visual LAN desabilitado (PreferLanUdpVisualTransport=false).");
    }

    public async Task StopAsync()
    {
        WebApplication? localApp;
        MqttServer? localMqttServer;
        CancellationTokenSource? localCts;
        TrackedCommandState[] pendingToCancel;
        DeviceFrameConnection[] connectionsToDispose;
        AdminEventConnection[] adminConnectionsToDispose;

        lock (gate)
        {
            localApp = app;
            if (localApp is null)
            {
                return;
            }

            localCts = appCts;
            app = null;
            localMqttServer = mqttServer;
            mqttServer = null;
            appCts = null;
            pendingToCancel = commandStateStore.Drain();
            sessionStateStore.Drain();
            connectionsToDispose = frameConnections.Drain();
        }

        lock (adminEventConnectionsGate)
        {
            adminConnectionsToDispose = adminEventConnections.ToArray();
            adminEventConnections.Clear();
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

        try
        {
            localCts?.Cancel();
            await localApp.StopAsync().ConfigureAwait(false);
            if (localMqttServer is not null)
            {
                await StopMqttServerAsync(localMqttServer).ConfigureAwait(false);
            }
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
                pairingStore.Clear();
            }

            foreach (var connection in connectionsToDispose)
            {
                connection.Dispose();
            }

            foreach (var connection in adminConnectionsToDispose)
            {
                connection.Dispose();
            }

            NotifyDevicesChanged();
        }

        Log("Servidor de dispositivos parado");
    }

    public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString(CultureInfo.InvariantCulture);
        PairingCodeInfo pairingCode;

        lock (gate)
        {
            pairingCode = pairingStore.SaveCode(code, ttl, timeProvider.GetUtcNow());
        }

        Log($"Codigo de pareamento gerado (expira em {ttl.TotalSeconds:0}s).");
        return pairingCode;
    }

    public IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot()
    {
        lock (gate)
        {
            return sessionStateStore.CreateSnapshots(timeProvider.GetUtcNow(), runtimeConfig.DeviceOfflineTimeout);
        }
    }

    public IReadOnlyList<DeviceRecord> GetDeviceRecords()
    {
        lock (gate)
        {
            return sessionStateStore.CreateRecords();
        }
    }

    public string GetPublicHttpBaseAddress()
    {
        return ResolveAdvertisedHttpBaseAddress(runtimeConfig);
    }

    public void SeedDevices(IEnumerable<DeviceRecord> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        lock (gate)
        {
            foreach (var record in devices)
            {
                if (string.IsNullOrWhiteSpace(record.DeviceId) || string.IsNullOrWhiteSpace(record.Token))
                {
                    continue;
                }

                var session = new DeviceSessionState(record, SocketDetachGracePeriod);
                var replaced = sessionStateStore.Upsert(session);
                if (replaced is not null)
                {
                    frameConnections.RemoveAndDispose(record.DeviceId);
                }
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

        MqttServer? localMqttServer;
        lock (gate)
        {
            if (!sessionStateStore.Remove(deviceId, out _))
            {
                return false;
            }

            frameConnections.RemoveAndDispose(deviceId);
            localMqttServer = mqttServer;
        }

        if (localMqttServer is not null)
        {
            ScheduleRetainedDeviceStateCleanup(localMqttServer, deviceId);
        }

        NotifyDevicesChanged();
        Log($"Device removido: {deviceId}");
        return true;
    }

    public void BroadcastFrame(byte[] framePayload)
    {
        ArgumentNullException.ThrowIfNull(framePayload);

        (string DeviceId, DeviceFrameConnection Connection)[] targets;
        Dictionary<string, DeviceRecord> recordsByDeviceId;
        IReadOnlyList<DeviceSnapshot> snapshots;
        lock (gate)
        {
            targets = frameConnections.GetOpenConnectionEntries();
            recordsByDeviceId = sessionStateStore
                .CreateRecords()
                .ToDictionary(record => record.DeviceId, StringComparer.OrdinalIgnoreCase);
            snapshots = sessionStateStore.CreateSnapshots(timeProvider.GetUtcNow(), runtimeConfig.DeviceOfflineTimeout);
        }

        HashSet<string>? udpSentDeviceIds = null;
        foreach (var snapshot in snapshots)
        {
            if (!recordsByDeviceId.TryGetValue(snapshot.DeviceId, out var record)
                || !TrySendVisualFrameOverUdp(record, snapshot.ControlPlaneState == DeviceControlPlaneState.MqttOnline, framePayload))
            {
                continue;
            }

            udpSentDeviceIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            udpSentDeviceIds.Add(record.DeviceId);
        }

        foreach (var (deviceId, connection) in targets)
        {
            if (udpSentDeviceIds?.Contains(deviceId) == true)
            {
                continue;
            }

            connection.QueueFrame(framePayload);
        }
    }

    public void SendFrame(string deviceId, byte[] framePayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(framePayload);

        DeviceFrameConnection? target;
        DeviceSessionState? state;
        lock (gate)
        {
            sessionStateStore.TryGetValue(deviceId.Trim(), out state);
            frameConnections.TryGetValue(deviceId.Trim(), out target);
        }

        if (state is not null && TrySendVisualFrameOverUdp(state.Record, state.IsControlPlaneOnline, framePayload))
        {
            return;
        }

        if (target?.Socket is not { State: WebSocketState.Open })
        {
            return;
        }

        target.QueueFrame(framePayload);
    }

    private bool TrySendVisualFrameOverUdp(DeviceRecord record, bool isOnline, byte[] framePayload)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(framePayload);

        if (!runtimeConfig.PreferLanUdpVisualTransport
            || !isOnline
            || record.VisualUdpSupported != true
            || !string.Equals(record.VisualUdpMode, "bins128", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(record.Token)
            || !TryResolveVisualUdpEndpoint(record, runtimeConfig, out var endpointAddress, out var endpointPort)
            || !VisualUdpFrameV1.IsSupportedPayload(framePayload))
        {
            return false;
        }

        var datagramLength = VisualUdpFrameV1.GetDatagramSize(framePayload.Length);
        var rented = ArrayPool<byte>.Shared.Rent(datagramLength);
        try
        {
            if (!VisualUdpFrameV1.TryWriteDatagram(rented.AsSpan(0, datagramLength), framePayload, record.Token, out var written))
            {
                return false;
            }

            return visualUdpSender.TrySend(endpointAddress, endpointPort, rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool TryResolveVisualUdpEndpoint(
        DeviceRecord record,
        DeviceServerRuntimeConfig config,
        out IPAddress address,
        out int port)
    {
        address = IPAddress.None;
        port = 0;

        if (string.IsNullOrWhiteSpace(record.LastKnownIp) || !IPAddress.TryParse(record.LastKnownIp, out var parsedAddress))
        {
            return false;
        }

        if (parsedAddress.IsIPv4MappedToIPv6)
        {
            parsedAddress = parsedAddress.MapToIPv4();
        }

        if (!IPAddress.IsLoopback(parsedAddress) && !IsPrivateNetworkAddress(parsedAddress))
        {
            return false;
        }

        var resolvedPort = record.VisualUdpPort is >= 1 and <= 65535
            ? record.VisualUdpPort.Value
            : config.VisualUdpPort;
        if (resolvedPort is < 1 or > 65535)
        {
            return false;
        }

        address = parsedAddress;
        port = resolvedPort;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        if (visualUdpSender is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task<IResult> HandlePairAsync(HttpContext ctx)
    {
        var remoteIpKey = BuildRateLimitPartitionKey(ctx.Connection.RemoteIpAddress);
        if (!TryRegisterPairingAttempt(remoteIpKey, out var retryAfterSeconds))
        {
            return Results.Json(new { error = "pairing_rate_limited", retryAfterSeconds }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (IsRequestBodyTooLarge(ctx))
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

        DeviceSessionState state;
        lock (gate)
        {
            if (sessionStateStore.Count >= runtimeConfig.MaxDevices)
            {
                return Results.BadRequest(new { error = "max_devices_reached" });
            }

            var now = timeProvider.GetUtcNow();
            var record = DeviceRecordMutations.CreatePairedRecord(
                deviceId: $"mp-{Guid.NewGuid():N}",
                token: WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24)),
                name: string.IsNullOrWhiteSpace(req.DeviceName) ? ResolveDefaultDeviceName(req.BoardModel) : req.DeviceName.Trim(),
                profile: NormalizeFirmwareProfile(req.Profile),
                firmwareVersion: req.FirmwareVersion,
                ip: ctx.Connection.RemoteIpAddress?.ToString(),
                boardModel: NormalizeOptional(req.BoardModel),
                panelType: NormalizeOptional(req.PanelType),
                now: now);

            state = new DeviceSessionState(record, SocketDetachGracePeriod);
            var replaced = sessionStateStore.Upsert(state);
            if (replaced is not null)
            {
                frameConnections.RemoveAndDispose(record.DeviceId);
            }

            pairingStore.ResetAttempts(remoteIpKey);
        }

        NotifyDevicesChanged();
        Log($"Device pareado: {state.Record.DeviceId}");

        var httpBase = ResolveAdvertisedHttpBaseAddress(ctx);
        var mqttHost = ResolveAdvertisedMqttHost(ctx);
        return Results.Ok(new PairDeviceResponse
        {
            DeviceId = state.Record.DeviceId,
            Token = state.Record.Token,
            WsPath = "/ws/v1/stream",
            HttpBase = httpBase,
            MqttHost = mqttHost,
            MqttPort = runtimeConfig.MqttPort,
            MqttRootTopic = runtimeConfig.MqttRootTopic,
            MdnsService = runtimeConfig.MdnsServiceName,
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
            MdnsService = runtimeConfig.MdnsServiceName,
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
        DeviceFrameConnection connection;
        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            var remoteIp = ctx.Connection.RemoteIpAddress?.ToString();
            state.MarkAuthenticated(now);
            state.MarkSeen(
                now,
                remoteIp,
                state.Record.LastKnownRssi,
                state.Record.FirmwareVersion,
                state.Record.ActiveAppId,
                state.Record.ActiveAppName,
                state.Record.BoardModel,
                state.Record.PanelType);
            connection = frameConnections.GetOrCreate(state.Record.DeviceId);
            connection.AttachSocket(ws);
        }

        if (!state.IsControlPlaneOnline)
        {
            Log($"Stream WS conectado sem control plane MQTT para {state.Record.DeviceId}; firmware legado nao suportado para comandos.");
        }

        NotifyDevicesChanged();

        var sendTask = Task.Run(() => SendLoopAsync(connection, ws, connection.SendToken));
        try
        {
            await ReceiveLoopAsync(state, ws, runtimeConfig.MaxWebSocketMessageBytes, ctx.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            if (connection.DetachSocket(ws))
            {
                NotifyDevicesChanged();
            }

            await sendTask.ConfigureAwait(false);
        }
    }

    private static async Task SendLoopAsync(DeviceFrameConnection connection, WebSocket ws, CancellationToken sendToken)
    {
        while (true)
        {
            byte[] payload;
            try
            {
                payload = await connection.Outgoing.Reader.ReadAsync(sendToken).ConfigureAwait(false);
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

    private async Task ReceiveLoopAsync(DeviceSessionState state, WebSocket ws, int maxMessageSize, CancellationToken cancellationToken)
    {
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

                if (messageBuffer.Length > maxMessageSize)
                {
                    Log($"Mensagem WS excedeu {maxMessageSize} bytes de {state.Record.DeviceId}. Encerrando conexao.");
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

    private bool TryAuthenticate(HttpContext ctx, AuthContext authContext, out DeviceSessionState state)
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
            && runtimeConfig.AllowLegacyWebSocketQueryToken)
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
            if (!sessionStateStore.TryGetValue(deviceId, out var foundState) || foundState is null)
            {
                return false;
            }

            state = foundState;
            return TokensMatchConstantTime(state.Record.Token, token);
        }
    }

    private bool TryConsumePairingCode(string code)
    {
        lock (gate)
        {
            return pairingStore.TryConsumeCode(code, timeProvider.GetUtcNow());
        }
    }

    private bool TryRegisterPairingAttempt(string remoteIpKey, out int retryAfterSeconds)
    {
        lock (gate)
        {
            return pairingStore.TryRegisterAttempt(
                remoteIpKey,
                runtimeConfig.PairingAttemptsPerWindow,
                runtimeConfig.PairingAttemptWindow,
                timeProvider.GetUtcNow(),
                out retryAfterSeconds);
        }
    }

    private bool IsRequestBodyTooLarge(HttpContext ctx)
    {
        if (!ctx.Request.ContentLength.HasValue)
        {
            return false;
        }

        return ctx.Request.ContentLength.Value > runtimeConfig.MaxJsonBodyBytes;
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

    private static bool IsRequestAllowed(DeviceServerRuntimeConfig config, IPAddress? remoteIp)
    {
        if (remoteIp is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        if (config.AllowedCidrs.Count > 0)
        {
            return config.AllowedCidrs.Any(cidr => cidr.Contains(remoteIp));
        }

        if (!config.RestrictToPrivateNetworks)
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

    private string ResolveAdvertisedHttpBaseAddress(HttpContext ctx)
        => ResolveAdvertisedHttpBaseAddress(runtimeConfig, ctx);

    private static string ResolveAdvertisedHttpBaseAddress(DeviceServerRuntimeConfig config, HttpContext? ctx = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!string.IsNullOrWhiteSpace(config.PublicHttpBaseAddress))
        {
            return config.PublicHttpBaseAddress;
        }

        if (ctx is not null && TryGetRequestHost(ctx, out var requestHostValue, out _))
        {
            return $"{ResolveRequestScheme(ctx)}://{requestHostValue}";
        }

        var fallbackHost = string.IsNullOrWhiteSpace(config.PublicHost)
            ? config.ListenHost
            : config.PublicHost;
        return $"http://{fallbackHost}:{config.Port}";
    }

    private string ResolveAdvertisedMqttHost(HttpContext ctx)
        => ResolveAdvertisedMqttHost(runtimeConfig, ctx);

    private static string ResolveAdvertisedMqttHost(DeviceServerRuntimeConfig config, HttpContext? ctx = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!string.IsNullOrWhiteSpace(config.PublicHost))
        {
            return config.PublicHost;
        }

        if (!string.IsNullOrWhiteSpace(config.PublicHttpBaseAddress)
            && Uri.TryCreate(config.PublicHttpBaseAddress, UriKind.Absolute, out var publicBase))
        {
            return publicBase.Host;
        }

        if (ctx is not null && TryGetRequestHost(ctx, out _, out var requestHost))
        {
            return requestHost;
        }

        return config.ListenHost;
    }

    private static string ResolveRequestScheme(HttpContext ctx)
    {
        var forwardedProto = ctx.Request.Headers["X-Forwarded-Proto"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? ctx.Request.Scheme : forwardedProto;

        return string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? Uri.UriSchemeHttps
            : Uri.UriSchemeHttp;
    }

    private static bool TryGetRequestHost(HttpContext ctx, out string hostValue, out string host)
    {
        var forwardedHost = ctx.Request.Headers["X-Forwarded-Host"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        var requestHost = string.IsNullOrWhiteSpace(forwardedHost)
            ? ctx.Request.Host
            : HostString.FromUriComponent(forwardedHost);

        hostValue = requestHost.Value ?? string.Empty;
        host = requestHost.Host;

        if (string.IsNullOrWhiteSpace(hostValue) || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return !string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, "::", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, "[::]", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, "*", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, "+", StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyDevicesChanged()
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        _ = BroadcastAdminEventAsync(new AdminEventMessage
        {
            Type = "devices_changed",
            Devices = GetDevicesSnapshot(),
            Utc = timeProvider.GetUtcNow(),
        });
    }

    private void Log(string message)
    {
        DeviceServerObservability.LogHostMessage(message);
        LogMessage?.Invoke(this, message);
    }
}
