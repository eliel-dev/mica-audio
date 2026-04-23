using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Device.Client.Remote;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed partial class RemoteDeviceServerClient : IDeviceServerClient, IDeviceServerClientRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient;
    private readonly RemoteDeviceServerClientOptions options;
    private readonly ILogger<RemoteDeviceServerClient> logger;
    private readonly bool ownsHttpClient;
    private readonly object lifecycleGate = new();

    private CancellationTokenSource? eventsCts;
    private Task? eventsTask;

    public RemoteDeviceServerClient(
        HttpClient httpClient,
        RemoteDeviceServerClientOptions options,
        ILogger<RemoteDeviceServerClient>? logger = null,
        bool ownsHttpClient = false)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? NullLogger<RemoteDeviceServerClient>.Instance;
        this.ownsHttpClient = ownsHttpClient;
        ConfigureHttpClient(httpClient, options);
    }

    public event EventHandler? DevicesChanged;

    public event EventHandler<string>? LogMessage;

    public event EventHandler<DeviceLogMessage>? DeviceLogReceived;

    public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    public string GetServerBaseAddress() => NormalizeBaseAddress(options.BaseAddress).ToString().TrimEnd('/');

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (lifecycleGate)
        {
            if (eventsTask is not null)
            {
                return;
            }

            eventsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            eventsTask = Task.Run(() => RunEventsLoopAsync(eventsCts.Token), CancellationToken.None);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (lifecycleGate)
        {
            cts = eventsCts;
            task = eventsTask;
            eventsCts = null;
            eventsTask = null;
        }

        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    public async Task<PairingCodeInfo> CreatePairingCodeAsync(TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var request = new AdminCreatePairingCodeRequest
        {
            TtlSeconds = Math.Max(1, (int)Math.Ceiling(ttl.TotalSeconds)),
        };

        using var response = await httpClient.PostAsJsonAsync("/api/v1/admin/pairing-codes", request, JsonOptions, cancellationToken).ConfigureAwait(false);
        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadRequiredJsonAsync<PairingCodeInfo>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeviceSnapshot>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/v1/admin/devices", cancellationToken).ConfigureAwait(false);
        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var devices = await ReadRequiredJsonAsync<AdminDevicesResponse>(response, cancellationToken).ConfigureAwait(false);
        return devices.Devices;
    }

    public async Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        using var response = await httpClient.DeleteAsync($"/api/v1/admin/devices/{Uri.EscapeDataString(deviceId.Trim())}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var request = new AdminTrackedCommandRequest
        {
            CommandType = commandType,
            Parameters = parameters is null ? null : new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase),
            TimeoutMs = Math.Max(1, (int)Math.Ceiling(timeout.TotalMilliseconds)),
        };

        using var response = await httpClient
            .PostAsJsonAsync($"/api/v1/admin/devices/{Uri.EscapeDataString(deviceId.Trim())}/commands/tracked", request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await ReadRequiredJsonAsync<CommandDispatchResult>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PanelsBatchRegistration> RegisterPanelsBatchAsync(
        string deviceId,
        string panelsSessionId,
        ulong batchSequence,
        byte[] payload,
        int frameCount,
        int durationMs,
        string contentType = "image/webp",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var path = $"/api/v1/admin/panels/batches/{Uri.EscapeDataString(deviceId.Trim())}/{Uri.EscapeDataString(panelsSessionId.Trim())}/{batchSequence}";
        var query = $"?frameCount={frameCount}&durationMs={durationMs}";
        using var request = new HttpRequestMessage(HttpMethod.Post, path + query)
        {
            Content = new ByteArrayContent(payload),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(NormalizeContentType(contentType));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var remote = await ReadRequiredJsonAsync<AdminPanelsBatchRegistrationResponse>(response, cancellationToken).ConfigureAwait(false);
        return new PanelsBatchRegistration(
            remote.PanelsSessionId,
            remote.BatchSequence,
            remote.FileSizeBytes,
            remote.Sha256,
            remote.ContentType,
            remote.FrameCount,
            remote.DurationMs,
            remote.DownloadUrl);
    }

    public async Task ClearPanelsBatchesAsync(string deviceId, string? panelsSessionId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        var path = $"/api/v1/admin/panels/batches/{Uri.EscapeDataString(deviceId.Trim())}";
        if (!string.IsNullOrWhiteSpace(panelsSessionId))
        {
            path += $"?panelsSessionId={Uri.EscapeDataString(panelsSessionId.Trim())}";
        }

        using var response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunEventsLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var ws = CreateAdminWebSocket();
                await ws.ConnectAsync(BuildWebSocketUri("/ws/v1/admin/events"), cancellationToken).ConfigureAwait(false);
                LogMessage?.Invoke(this, "Conectado ao stream remoto de eventos do servidor.");
                await ReceiveEventsAsync(ws, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogRemoteEventConnectionFailed(logger, ex);
                LogMessage?.Invoke(this, $"Falha no stream remoto de eventos: {ex.Message}");
            }

            try
            {
                await Task.Delay(options.EventReconnectDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ReceiveEventsAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                if (result.Count > 0)
                {
                    ms.Write(buffer, 0, result.Count);
                }
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            var message = JsonSerializer.Deserialize<AdminEventMessage>(json, JsonOptions);
            if (message is not null)
            {
                DispatchEvent(message);
            }
        }
    }

    private void DispatchEvent(AdminEventMessage message)
    {
        switch (message.Type)
        {
            case "devices_changed":
                DevicesChanged?.Invoke(this, EventArgs.Empty);
                break;
            case "device_log" when message.DeviceLog is not null:
                DeviceLogReceived?.Invoke(this, message.DeviceLog);
                break;
            case "command_progress" when message.CommandProgress is not null:
                CommandProgressChanged?.Invoke(this, message.CommandProgress);
                break;
            case "heartbeat":
                break;
        }
    }

    internal ClientWebSocket CreateAdminWebSocket()
    {
        var ws = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(options.AdminToken))
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {options.AdminToken}");
        }

        return ws;
    }

    internal Uri BuildWebSocketUri(string path)
    {
        var baseAddress = NormalizeBaseAddress(options.BaseAddress);
        var scheme = string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? "wss"
            : "ws";
        var builder = new UriBuilder(baseAddress)
        {
            Scheme = scheme,
            Path = path.TrimStart('/'),
            Query = string.Empty,
        };
        return builder.Uri;
    }

    internal static void ConfigureHttpClient(HttpClient httpClient, RemoteDeviceServerClientOptions options)
    {
        httpClient.BaseAddress = NormalizeBaseAddress(options.BaseAddress);
        httpClient.DefaultRequestHeaders.Remove("X-Mica-Admin-Token");
        httpClient.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrWhiteSpace(options.AdminToken))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AdminToken);
        }
    }

    private static async Task EnsureRemoteSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException(
            $"Remote device server returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
            null,
            response.StatusCode);
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
        return value ?? throw new InvalidOperationException($"Remote device server returned an empty {typeof(T).Name} payload.");
    }

    private static Uri NormalizeBaseAddress(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new Uri("http://127.0.0.1:5272");
        }

        return uri;
    }

    private static string NormalizeContentType(string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? "image/webp" : contentType.Trim();

    [LoggerMessage(EventId = 1300, Level = LogLevel.Warning, Message = "Remote device event stream connection failed.")]
    private static partial void LogRemoteEventConnectionFailed(ILogger logger, Exception exception);
}
