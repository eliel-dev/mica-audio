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
// DOCS: docs/wiki/modules/device-server-protocol.md#atualizacao-2026-04-admin-api-e-winui-remote
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

    /// <summary>
    /// Uploads the active panel definition for the device so MicaAudio.Server
    /// can keep autonomous widgets (Clock today, more later) alive after the
    /// WinUI client disconnects. The JSON payload must match the shape of
    /// Panels.Composition.Models.PanelDefinition.
    /// </summary>
    public async Task UploadPanelAsync(string deviceId, string panelJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("deviceId is required.", nameof(deviceId));
        }

        ArgumentNullException.ThrowIfNull(panelJson);

        var path = $"/api/v1/admin/devices/{Uri.EscapeDataString(deviceId.Trim())}/panel";
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new StringContent(panelJson, Encoding.UTF8, "application/json"),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the stored panel for the device (e.g. when the user picks a
    /// panel that requires the WinUI client). Returns true when a panel was
    /// removed, false when none was stored.
    /// </summary>
    public async Task<bool> DeletePanelAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        var path = $"/api/v1/admin/devices/{Uri.EscapeDataString(deviceId.Trim())}/panel";
        using var response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Returns the panel currently stored on the server for the given device,
    /// or <see langword="null"/> when none is stored. The WinUI uses this on
    /// startup to reconcile its own UI state with what the server is actually
    /// rendering on the device (server-as-source-of-truth).
    /// </summary>
    public async Task<ServerPanelSnapshot?> GetServerPanelAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        var path = $"/api/v1/admin/devices/{Uri.EscapeDataString(deviceId.Trim())}/panel";
        using var response = await httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        // Body shape (from DeviceServerHost.PanelStore.cs):
        //   { deviceId, panel: <PanelDefinition>, capability: "ServerCapable" | ... }
        // Re-serialize the panel sub-tree so callers receive raw JSON they can
        // deserialize into their own PanelDefinition shape.
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        var snapshot = new ServerPanelSnapshot
        {
            DeviceId = root.TryGetProperty("deviceId", out var idElem) ? (idElem.GetString() ?? deviceId) : deviceId,
            Capability = root.TryGetProperty("capability", out var capElem) ? (capElem.GetString() ?? string.Empty) : string.Empty,
        };

        if (root.TryGetProperty("panel", out var panelElem) && panelElem.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            snapshot.PanelJson = panelElem.GetRawText();
            if (panelElem.TryGetProperty("panelId", out var pidElem))
            {
                snapshot.PanelId = pidElem.GetString() ?? string.Empty;
            }
            if (panelElem.TryGetProperty("name", out var nameElem))
            {
                snapshot.PanelName = nameElem.GetString() ?? string.Empty;
            }
            if (panelElem.TryGetProperty("widgets", out var widgetsElem) && widgetsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                snapshot.WidgetCount = widgetsElem.GetArrayLength();
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Uploads a raw media file (GIF/PNG/JPG/BMP) so the server can render
    /// gifhub75 widgets after the WinUI client disconnects.
    /// </summary>
    public async Task UploadMediaAsync(
        string deviceId,
        string mediaId,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("deviceId is required.", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new ArgumentException("mediaId is required.", nameof(mediaId));
        }

        ArgumentNullException.ThrowIfNull(bytes);

        var path = $"/api/v1/admin/devices/{Uri.EscapeDataString(deviceId.Trim())}/media/{Uri.EscapeDataString(mediaId.Trim())}";
        using var content = new ByteArrayContent(bytes);
        using var response = await httpClient.PutAsync(path, content, cancellationToken).ConfigureAwait(false);
        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a previously uploaded media file. Returns true when the file
    /// existed and was deleted, false when it was already absent.
    /// </summary>
    public async Task<bool> DeleteMediaAsync(
        string deviceId,
        string mediaId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(mediaId))
        {
            return false;
        }

        var path = $"/api/v1/admin/devices/{Uri.EscapeDataString(deviceId.Trim())}/media/{Uri.EscapeDataString(mediaId.Trim())}";
        using var response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureRemoteSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // ── Panel catalog ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the full panel catalog from the server as raw JSON.
    /// Returns null when the server is unreachable.
    /// </summary>
    public async Task<string?> GetPanelCatalogJsonAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("/api/v1/admin/panels", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates or updates a panel in the server catalog.
    /// Returns true on success.
    /// </summary>
    public async Task<bool> UpsertCatalogPanelAsync(string panelId, string panelJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(panelId) || string.IsNullOrWhiteSpace(panelJson))
        {
            return false;
        }

        try
        {
            var path = $"/api/v1/admin/panels/{Uri.EscapeDataString(panelId.Trim())}";
            using var request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = new StringContent(panelJson, Encoding.UTF8, "application/json"),
            };
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes a panel from the server catalog.
    /// Returns true when it existed and was removed.
    /// </summary>
    public async Task<bool> DeleteCatalogPanelAsync(string panelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return false;
        }

        try
        {
            var path = $"/api/v1/admin/panels/{Uri.EscapeDataString(panelId.Trim())}";
            using var response = await httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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
