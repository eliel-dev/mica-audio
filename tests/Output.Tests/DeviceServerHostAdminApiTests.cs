using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using MicaAudio.Server;
using Panels.Composition.Models;

namespace Output.Tests;

public sealed class DeviceServerHostAdminApiTests
{
    private const string AdminToken = "test-admin-token";

    [Fact]
    public async Task AdminDevices_ShouldRejectMissingOrInvalidToken()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        using var missing = await client.GetAsync("/api/v1/admin/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/devices");
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong");
        using var invalid = await client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact]
    public async Task AdminPairingCode_ShouldReturnDisabled_WhenAdminTokenIsEmpty()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        using var request = CreateAdminRequest(HttpMethod.Post, "/api/v1/admin/pairing-codes");
        request.Content = JsonContent.Create(new AdminCreatePairingCodeRequest { TtlSeconds = 300 });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("admin_api_disabled", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminApi_WithValidToken_ShouldCreatePairingCodeListAndRemoveDevice()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var pairing = await CreatePairingCodeViaAdminAsync(client);

        var pairedResponse = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = pairing.Code,
            DeviceName = "admin-api-device",
        });
        pairedResponse.EnsureSuccessStatusCode();
        var paired = await pairedResponse.Content.ReadFromJsonAsync<PairDeviceResponse>();
        Assert.NotNull(paired);

        using var listRequest = CreateAdminRequest(HttpMethod.Get, "/api/v1/admin/devices");
        using var listResponse = await client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var devices = await listResponse.Content.ReadFromJsonAsync<AdminDevicesResponse>();
        Assert.Contains(devices!.Devices, device => string.Equals(device.DeviceId, paired!.DeviceId, StringComparison.OrdinalIgnoreCase));

        using var deleteRequest = CreateAdminRequest(HttpMethod.Delete, $"/api/v1/admin/devices/{paired!.DeviceId}");
        using var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        Assert.DoesNotContain(host.GetDevicesSnapshot(), device => string.Equals(device.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdminPanelsBatch_ShouldRegisterAndClearBatch()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "admin-batch-device");
        var payload = "RIFFremoteWEBP"u8.ToArray();

        using var postRequest = CreateAdminRequest(
            HttpMethod.Post,
            $"/api/v1/admin/panels/batches/{paired.DeviceId}/session-r/7?frameCount=12&durationMs=400");
        postRequest.Content = new ByteArrayContent(payload);
        postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");

        using var postResponse = await client.SendAsync(postRequest);
        postResponse.EnsureSuccessStatusCode();
        var registration = await postResponse.Content.ReadFromJsonAsync<AdminPanelsBatchRegistrationResponse>();
        Assert.NotNull(registration);
        Assert.Equal(payload.LongLength, registration!.FileSizeBytes);
        Assert.Equal("image/webp", registration.ContentType);
        Assert.Equal(12, registration.FrameCount);
        Assert.Equal(400, registration.DurationMs);

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, registration.DownloadUrl);
        downloadRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        downloadRequest.Headers.Add("X-Device-Token", paired.Token);
        using var downloadResponse = await client.SendAsync(downloadRequest);
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal(payload, await downloadResponse.Content.ReadAsByteArrayAsync());

        using var clearRequest = CreateAdminRequest(HttpMethod.Delete, $"/api/v1/admin/panels/batches/{paired.DeviceId}?panelsSessionId=session-r");
        using var clearResponse = await client.SendAsync(clearRequest);
        Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

        using var clearedRequest = new HttpRequestMessage(HttpMethod.Get, registration.DownloadUrl);
        clearedRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        clearedRequest.Headers.Add("X-Device-Token", paired.Token);
        using var clearedResponse = await client.SendAsync(clearedRequest);
        Assert.Equal(HttpStatusCode.NotFound, clearedResponse.StatusCode);
    }

    [Fact]
    public async Task DeviceDisplayState_ShouldReportFirstRunActiveAndExplicitlyCleared()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        host.AttachPanelStore(new InMemoryServerPanelStore());
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = DeviceServerTestHarness.GetFreeTcpPort(),
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "display-state-device");

        var firstRun = await GetDisplayStateAsync(client, paired);
        Assert.Equal("first_run", firstRun.State);

        using var uploadRequest = CreateAdminRequest(HttpMethod.Put, $"/api/v1/admin/devices/{paired.DeviceId}/panel");
        uploadRequest.Content = JsonContent.Create(CreateClockPanel());
        using var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var active = await GetDisplayStateAsync(client, paired);
        Assert.Equal("panel_active", active.State);

        using var deleteRequest = CreateAdminRequest(HttpMethod.Delete, $"/api/v1/admin/devices/{paired.DeviceId}/panel");
        using var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        var cleared = await GetDisplayStateAsync(client, paired);
        Assert.Equal("no_mode_active", cleared.State);
    }

    [Fact]
    public async Task DeviceDisplayState_ShouldTreatExplicitClearWithoutExistingPanelAsNoModeActive()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        host.AttachPanelStore(new InMemoryServerPanelStore());
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = DeviceServerTestHarness.GetFreeTcpPort(),
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "display-state-empty-clear-device");

        using var deleteRequest = CreateAdminRequest(HttpMethod.Delete, $"/api/v1/admin/devices/{paired.DeviceId}/panel");
        using var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        var cleared = await GetDisplayStateAsync(client, paired);
        Assert.Equal("no_mode_active", cleared.State);
    }

    [Fact]
    public async Task AdminMediaUpload_ShouldAllowMediaPayloadLargerThanJsonLimit()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var storageRoot = Path.Combine(Path.GetTempPath(), "mica-audio-media-tests", Guid.NewGuid().ToString("N"));
        await using var host = new DeviceServerHost();
        host.AttachMediaStore(new FileServerMediaStore(storageRoot));
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = DeviceServerTestHarness.GetFreeTcpPort(),
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
            MaxJsonBodyBytes = 1024,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var payload = Enumerable.Repeat((byte)0x47, 2048).ToArray();

        using var request = CreateAdminRequest(HttpMethod.Put, "/api/v1/admin/devices/mp-media/media/large.gif");
        request.Content = new ByteArrayContent(payload);
        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AdminMediaDownload_ShouldReturnStoredPayload()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var storageRoot = Path.Combine(Path.GetTempPath(), "mica-audio-media-tests", Guid.NewGuid().ToString("N"));
        await using var host = new DeviceServerHost();
        host.AttachMediaStore(new FileServerMediaStore(storageRoot));
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = DeviceServerTestHarness.GetFreeTcpPort(),
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var payload = "GIF89a-test"u8.ToArray();

        using var upload = CreateAdminRequest(HttpMethod.Put, "/api/v1/admin/devices/mp-media/media/preview.gif");
        upload.Content = new ByteArrayContent(payload);
        using var uploadResponse = await client.SendAsync(upload);
        uploadResponse.EnsureSuccessStatusCode();

        using var download = CreateAdminRequest(HttpMethod.Get, "/api/v1/admin/devices/mp-media/media/preview.gif");
        using var downloadResponse = await client.SendAsync(download);

        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/gif", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(payload, await downloadResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task AdminFramesWebSocket_ShouldForwardBroadcastAndTargetedFrames()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "admin-frames-device");

        using var deviceWs = new ClientWebSocket();
        deviceWs.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        deviceWs.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await deviceWs.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        using var adminWs = new ClientWebSocket();
        adminWs.Options.SetRequestHeader("X-Mica-Admin-Token", AdminToken);
        await adminWs.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/admin/frames"), CancellationToken.None);

        var targetedPayload = new byte[] { 1, 2, 3, 4 };
        await adminWs.SendAsync(
            BuildFrameEnvelope(targeted: true, paired.DeviceId, targetedPayload),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);

        Assert.Equal(targetedPayload, await ReceiveBinaryFrameAsync(deviceWs));

        var broadcastPayload = new byte[] { 5, 6, 7, 8 };
        await adminWs.SendAsync(
            BuildFrameEnvelope(targeted: false, string.Empty, broadcastPayload),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);

        Assert.Equal(broadcastPayload, await ReceiveBinaryFrameAsync(deviceWs));

        await CloseWebSocketQuietlyAsync(adminWs);
        await CloseWebSocketQuietlyAsync(deviceWs);
    }

    private static async Task<PairingCodeInfo> CreatePairingCodeViaAdminAsync(HttpClient client)
    {
        using var request = CreateAdminRequest(HttpMethod.Post, "/api/v1/admin/pairing-codes");
        request.Content = JsonContent.Create(new AdminCreatePairingCodeRequest { TtlSeconds = 300 });
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var pairing = await response.Content.ReadFromJsonAsync<PairingCodeInfo>();
        Assert.NotNull(pairing);
        return pairing!;
    }

    private static HttpRequestMessage CreateAdminRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);
        return request;
    }

    private static async Task<DeviceDisplayStateResponse> GetDisplayStateAsync(HttpClient client, PairDeviceResponse paired)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/device/display-state");
        request.Headers.Add("X-Device-Id", paired.DeviceId);
        request.Headers.Add("X-Device-Token", paired.Token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var state = await response.Content.ReadFromJsonAsync<DeviceDisplayStateResponse>();
        Assert.NotNull(state);
        return state!;
    }

    private static PanelDefinition CreateClockPanel()
    {
        return new PanelDefinition
        {
            PanelId = "clock-panel",
            Name = "Clock Panel",
            Widgets =
            [
                new PanelWidgetDefinition
                {
                    WidgetId = "clock",
                    AppId = "analogclock",
                    Width = 128,
                    Height = 64,
                },
            ],
        };
    }

    private static byte[] BuildFrameEnvelope(bool targeted, string deviceId, byte[] payload)
    {
        var deviceIdBytes = Encoding.UTF8.GetBytes(deviceId);
        var envelope = new byte[1 + sizeof(ushort) + deviceIdBytes.Length + payload.Length];
        envelope[0] = targeted ? (byte)1 : (byte)0;
        envelope[1] = (byte)(deviceIdBytes.Length & 0xFF);
        envelope[2] = (byte)(deviceIdBytes.Length >> 8);
        deviceIdBytes.CopyTo(envelope.AsSpan(3));
        payload.CopyTo(envelope.AsSpan(3 + deviceIdBytes.Length));
        return envelope;
    }

    private static async Task<byte[]> ReceiveBinaryFrameAsync(ClientWebSocket ws)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, timeoutCts.Token);
            Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
            if (result.Count > 0)
            {
                ms.Write(buffer, 0, result.Count);
            }

            if (result.EndOfMessage)
            {
                return ms.ToArray();
            }
        }
    }

    private static async Task CloseWebSocketQuietlyAsync(ClientWebSocket ws)
    {
        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                return;
            }
            catch (WebSocketException)
            {
            }
        }

        ws.Abort();
    }
}
