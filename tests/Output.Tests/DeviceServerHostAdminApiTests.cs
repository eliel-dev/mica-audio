using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

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
    public async Task PairingLegacy_ShouldReuseExistingDeviceByMac()
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
        var first = await DeviceServerTestHarness.PairDeviceAsync(
            host,
            client,
            "legacy-pair-first",
            deviceMac: "AA:BB:CC:DD:EE:22");
        var second = await DeviceServerTestHarness.PairDeviceAsync(
            host,
            client,
            "legacy-pair-second",
            deviceMac: "aa-bb-cc-dd-ee-22");

        Assert.Equal(first.DeviceId, second.DeviceId);
        Assert.Equal(first.Token, second.Token);

        var record = Assert.Single(host.GetDeviceRecords());
        Assert.Equal("aa:bb:cc:dd:ee:22", record.DeviceMac);
    }

    [Fact]
    public async Task AdminVisualEndpoints_ShouldReturnOnlyOnlineUdpCapableDevicesWithLanIp()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = mqttPort,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        host.SeedDevices(
        [
            new DeviceRecord
            {
                DeviceId = "online-udp",
                Token = "online-token",
                Name = "Online UDP",
                LastSeenUtc = DateTimeOffset.UtcNow,
                LanIpAddress = "192.168.15.51",
                LastKnownIp = "172.17.0.1",
                VisualUdpSupported = true,
                VisualUdpPort = 5274,
                VisualUdpMode = "bins128",
                FirmwareVersion = "v2026.04.28-222740Z-untagged-aac170d",
            },
            new DeviceRecord
            {
                DeviceId = "offline-udp",
                Token = "offline-token",
                Name = "Offline UDP",
                LastSeenUtc = DateTimeOffset.UtcNow,
                LanIpAddress = "192.168.15.52",
                VisualUdpSupported = true,
                VisualUdpPort = 5274,
                VisualUdpMode = "bins128",
            },
            new DeviceRecord
            {
                DeviceId = "online-no-lan",
                Token = "online-no-lan-token",
                Name = "Online no LAN",
                LastSeenUtc = DateTimeOffset.UtcNow,
                LastKnownIp = "172.17.0.1",
                VisualUdpSupported = true,
                VisualUdpPort = 5274,
                VisualUdpMode = "bins128",
            },
        ]);

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            "online-udp",
            "online-token");
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, "online-udp", "online");

        var online = await DeviceServerTestHarness.WaitForConditionAsync(
            () => DeviceServerTestHarness.GetDeviceStatus(host, "online-udp") == DeviceStatus.Online,
            TimeSpan.FromSeconds(5));
        Assert.True(online, "Device seedado nao entrou em online pelo MQTT.");

        using var request = CreateAdminRequest(HttpMethod.Get, "/api/v1/admin/visual-endpoints");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AdminVisualEndpointsResponse>();

        Assert.NotNull(body);
        var endpoint = Assert.Single(body!.Devices);
        Assert.Equal("online-udp", endpoint.DeviceId);
        Assert.Equal("192.168.15.51", endpoint.LanIpAddress);
        Assert.Equal(5274, endpoint.VisualUdpPort);
        Assert.Equal("bins128", endpoint.VisualUdpMode);
        Assert.Equal("online-token", endpoint.DeviceToken);
        Assert.Equal("v2026.04.28-222740Z-untagged-aac170d", endpoint.FirmwareVersion);
        Assert.Equal(DeviceControlPlaneState.MqttOnline, endpoint.ControlPlaneState);
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
