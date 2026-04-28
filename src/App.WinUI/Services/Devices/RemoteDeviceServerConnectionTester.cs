using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md
public sealed class RemoteDeviceServerConnectionTester
{
    private readonly TimeSpan requestTimeout;

    public RemoteDeviceServerConnectionTester()
        : this(TimeSpan.FromSeconds(4))
    {
    }

    internal RemoteDeviceServerConnectionTester(TimeSpan requestTimeout)
    {
        this.requestTimeout = requestTimeout;
    }

    public async Task<RemoteDeviceServerConnectionTestResult> TestAsync(
        string baseAddress,
        string adminToken,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeBaseAddress(baseAddress, out var normalizedBaseAddress))
        {
            return RemoteDeviceServerConnectionTestResult.Fail("Endereco do servidor remoto invalido.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(requestTimeout);

        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = normalizedBaseAddress,
                Timeout = requestTimeout,
            };
            if (!string.IsNullOrWhiteSpace(adminToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.Trim());
            }

            using var health = await httpClient.GetAsync("/api/v1/health", timeoutCts.Token).ConfigureAwait(false);
            if (!health.IsSuccessStatusCode)
            {
                return RemoteDeviceServerConnectionTestResult.Fail($"Health falhou: HTTP {(int)health.StatusCode}.");
            }

            using var endpointsResponse = await httpClient.GetAsync("/api/v1/admin/visual-endpoints", timeoutCts.Token).ConfigureAwait(false);
            if (!endpointsResponse.IsSuccessStatusCode)
            {
                return new RemoteDeviceServerConnectionTestResult(
                    Success: false,
                    Message: $"Admin token falhou: HTTP {(int)endpointsResponse.StatusCode}.",
                    HealthOk: true,
                    AdminOk: false,
                    FramesWebSocketOk: false,
                    VisualEndpointCount: 0);
            }

            var endpoints = await endpointsResponse.Content
                .ReadFromJsonAsync<AdminVisualEndpointsResponse>(cancellationToken: timeoutCts.Token)
                .ConfigureAwait(false);

            var framesWebSocketOk = await TryOpenFramesWebSocketAsync(
                normalizedBaseAddress,
                adminToken,
                timeoutCts.Token).ConfigureAwait(false);

            return new RemoteDeviceServerConnectionTestResult(
                Success: framesWebSocketOk,
                Message: framesWebSocketOk
                    ? $"Servidor remoto ok. Endpoints visuais: {endpoints?.Devices.Count ?? 0}."
                    : "HTTP/admin ok, mas o WebSocket de frames falhou.",
                HealthOk: true,
                AdminOk: true,
                FramesWebSocketOk: framesWebSocketOk,
                VisualEndpointCount: endpoints?.Devices.Count ?? 0);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RemoteDeviceServerConnectionTestResult.Fail("Teste do servidor remoto expirou.");
        }
        catch (Exception ex)
        {
            return RemoteDeviceServerConnectionTestResult.Fail($"Falha ao testar servidor remoto: {ex.Message}");
        }
    }

    private static async Task<bool> TryOpenFramesWebSocketAsync(Uri baseAddress, string adminToken, CancellationToken cancellationToken)
    {
        using var ws = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(adminToken))
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {adminToken.Trim()}");
        }

        await ws.ConnectAsync(BuildWebSocketUri(baseAddress, "/ws/v1/admin/frames"), cancellationToken).ConfigureAwait(false);
        if (ws.State != WebSocketState.Open)
        {
            return false;
        }

        ws.Abort();
        return true;
    }

    private static Uri BuildWebSocketUri(Uri baseAddress, string path)
    {
        var scheme = string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? "wss"
            : "ws";
        return new UriBuilder(baseAddress)
        {
            Scheme = scheme,
            Path = path.TrimStart('/'),
            Query = string.Empty,
        }.Uri;
    }

    private static bool TryNormalizeBaseAddress(string value, out Uri normalized)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            normalized = uri;
            return true;
        }

        normalized = new Uri("http://127.0.0.1:5272");
        return false;
    }
}

public sealed record RemoteDeviceServerConnectionTestResult(
    bool Success,
    string Message,
    bool HealthOk,
    bool AdminOk,
    bool FramesWebSocketOk,
    int VisualEndpointCount)
{
    public static RemoteDeviceServerConnectionTestResult Fail(string message)
        => new(
            Success: false,
            Message: message,
            HealthOk: false,
            AdminOk: false,
            FramesWebSocketOk: false,
            VisualEndpointCount: 0);
}
