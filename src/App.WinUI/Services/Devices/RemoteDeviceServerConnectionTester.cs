using System.Net.Http.Headers;
using System.Net.WebSockets;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-28-direct-lan-visual-and-device-identity.md
// DOCS: docs/handoffs/2026-04-29-remote-visual-endpoint-diagnostics.md
// DOCS: docs/handoffs/2026-04-29-server-mediated-visual-udp.md
public sealed class RemoteDeviceServerConnectionTester
{
    private const string AdminDevicesPath = "/api/v1/admin/devices";
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

            using var adminResponse = await httpClient.GetAsync(AdminDevicesPath, timeoutCts.Token).ConfigureAwait(false);
            if (!adminResponse.IsSuccessStatusCode)
            {
                return new RemoteDeviceServerConnectionTestResult(
                    Success: false,
                    Message: BuildAdminDevicesFailureMessage(adminResponse.StatusCode),
                    HealthOk: true,
                    AdminOk: false,
                    FramesWebSocketOk: false);
            }

            var framesWebSocketOk = await TryOpenFramesWebSocketAsync(
                normalizedBaseAddress,
                adminToken,
                timeoutCts.Token).ConfigureAwait(false);

            return new RemoteDeviceServerConnectionTestResult(
                Success: framesWebSocketOk,
                Message: framesWebSocketOk
                    ? "Servidor remoto ok. Health, admin token e WebSocket de frames validos."
                    : "HTTP/admin ok, mas o WebSocket de frames falhou.",
                HealthOk: true,
                AdminOk: true,
                FramesWebSocketOk: framesWebSocketOk);
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

    private static string BuildAdminDevicesFailureMessage(System.Net.HttpStatusCode statusCode)
        => statusCode switch
        {
            System.Net.HttpStatusCode.NotFound =>
                $"Servidor remoto sem a rota admin {AdminDevicesPath} (HTTP 404). Recrie o container com scripts/docker-server-redeploy.ps1 para publicar a versao atual.",
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                $"Admin token falhou em {AdminDevicesPath}: HTTP {(int)statusCode}.",
            _ =>
                $"Endpoint admin {AdminDevicesPath} falhou: HTTP {(int)statusCode}.",
        };

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
    bool FramesWebSocketOk)
{
    public static RemoteDeviceServerConnectionTestResult Fail(string message)
        => new(
            Success: false,
            Message: message,
            HealthOk: false,
            AdminOk: false,
            FramesWebSocketOk: false);
}
