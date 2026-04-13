using System.Net;
using Device.Protocol.Contracts;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class DeviceServerHostPanelsBatchTests
{
    [Fact]
    public async Task RegisterPanelsBatch_ShouldServePayloadOnlyToAuthenticatedTargetDevice()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "panels-batch-device");
        var payload = "RIFFdemoWEBP"u8.ToArray();

        var registration = host.RegisterPanelsBatch(
            paired.DeviceId,
            panelsSessionId: "session-a",
            batchSequence: 1,
            payload,
            frameCount: 30,
            durationMs: 1000);

        Assert.Equal(payload.LongLength, registration.FileSizeBytes);
        Assert.Equal("image/webp", registration.ContentType);
        Assert.Equal(30, registration.FrameCount);
        Assert.Equal(1000, registration.DurationMs);

        using var authorizedRequest = new HttpRequestMessage(HttpMethod.Get, registration.DownloadUrl);
        authorizedRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        authorizedRequest.Headers.Add("X-Device-Token", paired.Token);

        using var authorizedResponse = await client.SendAsync(authorizedRequest);
        Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        Assert.Equal(payload, await authorizedResponse.Content.ReadAsByteArrayAsync());

        using var wrongSessionRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/device/panels/batches/1.webp?panelsSessionId=other-session");
        wrongSessionRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        wrongSessionRequest.Headers.Add("X-Device-Token", paired.Token);

        using var wrongSessionResponse = await client.SendAsync(wrongSessionRequest);
        Assert.Equal(HttpStatusCode.NotFound, wrongSessionResponse.StatusCode);

        host.ClearPanelsBatches(paired.DeviceId, "session-a");

        using var clearedRequest = new HttpRequestMessage(HttpMethod.Get, registration.DownloadUrl);
        clearedRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        clearedRequest.Headers.Add("X-Device-Token", paired.Token);

        using var clearedResponse = await client.SendAsync(clearedRequest);
        Assert.Equal(HttpStatusCode.NotFound, clearedResponse.StatusCode);
    }
}
