using System.Net.Http.Json;
using System.Net.WebSockets;
using Device.Client.Remote;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class RemoteDeviceServerClientTests
{
    private const string AdminToken = "remote-client-test-token";

    [Fact]
    public async Task RemoteDeviceServerClient_ShouldCreatePairingCodeListAndRemoveDevices()
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

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        await using var remote = new RemoteDeviceServerClient(
            httpClient,
            new RemoteDeviceServerClientOptions
            {
                BaseAddress = $"http://127.0.0.1:{port}",
                AdminToken = AdminToken,
            });

        var pairing = await remote.CreatePairingCodeAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(pairing.Code));

        var pairedResponse = await httpClient.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = pairing.Code,
            DeviceName = "remote-client-device",
        });
        pairedResponse.EnsureSuccessStatusCode();
        var paired = await pairedResponse.Content.ReadFromJsonAsync<PairDeviceResponse>();
        Assert.NotNull(paired);

        var devices = await remote.GetDevicesAsync(CancellationToken.None);
        Assert.Contains(devices, device => string.Equals(device.DeviceId, paired!.DeviceId, StringComparison.OrdinalIgnoreCase));

        Assert.True(await remote.RemoveDeviceAsync(paired!.DeviceId, CancellationToken.None));
        var afterRemove = await remote.GetDevicesAsync(CancellationToken.None);
        Assert.DoesNotContain(afterRemove, device => string.Equals(device.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RemoteDeviceServerClient_ShouldRegisterAndClearPanelsBatch()
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

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        await using var remote = new RemoteDeviceServerClient(
            httpClient,
            new RemoteDeviceServerClientOptions
            {
                BaseAddress = $"http://127.0.0.1:{port}",
                AdminToken = AdminToken,
            });

        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, httpClient, "remote-batch-device");
        var payload = "RIFFclientWEBP"u8.ToArray();

        var registration = await remote.RegisterPanelsBatchAsync(
            paired.DeviceId,
            "session-client",
            3,
            payload,
            frameCount: 20,
            durationMs: 800,
            cancellationToken: CancellationToken.None);

        Assert.Equal(payload.LongLength, registration.FileSizeBytes);
        Assert.Equal("image/webp", registration.ContentType);
        Assert.Equal(20, registration.FrameCount);
        Assert.Equal(800, registration.DurationMs);

        await remote.ClearPanelsBatchesAsync(paired.DeviceId, "session-client", CancellationToken.None);
        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, registration.DownloadUrl);
        downloadRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        downloadRequest.Headers.Add("X-Device-Token", paired.Token);
        using var cleared = await httpClient.SendAsync(downloadRequest);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, cleared.StatusCode);
    }

    [Fact]
    public async Task RemoteDeviceFrameTransport_ShouldForwardFramesThroughAdminWebSocket()
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

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, httpClient, "remote-frame-device");

        using var deviceWs = new ClientWebSocket();
        deviceWs.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        deviceWs.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await deviceWs.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        await using var transport = new RemoteDeviceFrameTransport(new RemoteDeviceServerClientOptions
        {
            BaseAddress = $"http://127.0.0.1:{port}",
            AdminToken = AdminToken,
            FrameQueueCapacity = 4,
        });
        await transport.StartAsync(CancellationToken.None);

        var payload = new byte[] { 9, 8, 7, 6 };
        transport.SendFrame(paired.DeviceId, payload);

        Assert.Equal(payload, await ReceiveBinaryFrameAsync(deviceWs));

        await transport.StopAsync();
        await CloseWebSocketQuietlyAsync(deviceWs);
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
