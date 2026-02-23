using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class DeviceServerHostSecurityTests
{
    [Fact]
    public async Task PairingAttemptLimit_ShouldReturnTooManyRequestsAfterWindowExceeded()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            PairRequestsPerMinute = 100,
            PairingAttemptsPerWindow = 2,
            PairingAttemptWindowSeconds = 120,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
            {
                PairingCode = "000000",
                DeviceName = "test",
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = "000000",
            DeviceName = "test",
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task HeaderToken_ShouldTakePriorityOverQueryFallback()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            PairRequestsPerMinute = 100,
            PairingAttemptsPerWindow = 100,
            PairingAttemptWindowSeconds = 120,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var pairing = host.CreatePairingCode(TimeSpan.FromMinutes(5));
        var pairedResponse = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = pairing.Code,
            DeviceName = "header-priority",
        });

        pairedResponse.EnsureSuccessStatusCode();
        var paired = await pairedResponse.Content.ReadFromJsonAsync<PairDeviceResponse>();

        Assert.NotNull(paired);
        Assert.False(string.IsNullOrWhiteSpace(paired!.DeviceId));
        Assert.False(string.IsNullOrWhiteSpace(paired.Token));

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/device/config?deviceId={paired.DeviceId}&token=invalid-query-token");
        request.Headers.Add("X-Device-Id", paired.DeviceId);
        request.Headers.Add("X-Device-Token", paired.Token);

        var configViaHeader = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, configViaHeader.StatusCode);

        var configViaQuery = await client.GetAsync($"/api/v1/device/config?deviceId={paired.DeviceId}&token={paired.Token}");
        Assert.Equal(HttpStatusCode.OK, configViaQuery.StatusCode);
    }

    [Fact]
    public async Task PairRateLimit_ShouldThrottleBurstByIp()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            PairRequestsPerMinute = 1,
            PairingAttemptsPerWindow = 100,
            PairingAttemptWindowSeconds = 120,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var first = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest { PairingCode = "111111" });
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest { PairingCode = "111111" });
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }


    [Fact]
    public async Task StopAsync_ShouldCompletePendingTrackedCommandsWithoutRace()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            PairRequestsPerMinute = 100,
            PairingAttemptsPerWindow = 100,
            PairingAttemptWindowSeconds = 120,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var pairing = host.CreatePairingCode(TimeSpan.FromMinutes(5));
        var pairedResponse = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = pairing.Code,
            DeviceName = "pending-stop",
        });

        pairedResponse.EnsureSuccessStatusCode();
        var paired = await pairedResponse.Content.ReadFromJsonAsync<PairDeviceResponse>();

        Assert.NotNull(paired);

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream?deviceId={paired!.DeviceId}&token={paired.Token}"), CancellationToken.None);

        await Task.Delay(80);

        var sentTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnProgress(object? _, DeviceCommandProgressMessage progress)
        {
            if (string.Equals(progress.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(progress.Stage, "sent", StringComparison.OrdinalIgnoreCase))
            {
                sentTcs.TrySetResult(true);
            }
        }

        host.CommandProgressChanged += OnProgress;
        var pendingTask = host.SendCommandTrackedAsync(
            paired.DeviceId,
            DeviceCommandType.TestLed,
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: CancellationToken.None);

        await sentTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await host.StopAsync();
        host.CommandProgressChanged -= OnProgress;

        var result = await pendingTask;
        Assert.True(result.Accepted);
        Assert.True(result.Completed);
        Assert.False(result.Success);
        Assert.Equal("server-stopped", result.Stage);
        Assert.Equal("server_stopped", result.ErrorCode);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
