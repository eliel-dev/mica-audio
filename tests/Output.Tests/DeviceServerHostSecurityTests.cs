using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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
    public async Task AuthToken_ShouldNotBeAcceptedViaQueryString_ForHttpEndpoints()
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

        var paired = await PairDeviceAsync(host, client, "http-query-auth");

        var viaQuery = await client.GetAsync($"/api/v1/device/config?deviceId={paired.DeviceId}&token={paired.Token}");
        Assert.Equal(HttpStatusCode.Unauthorized, viaQuery.StatusCode);

        using var viaHeaderRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/device/config");
        viaHeaderRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        viaHeaderRequest.Headers.Add("X-Device-Token", paired.Token);

        var viaHeader = await client.SendAsync(viaHeaderRequest);
        Assert.Equal(HttpStatusCode.OK, viaHeader.StatusCode);
    }

    [Fact]
    public async Task Pairing_WithBoardAndPanel_ShouldPersistMetadataInSnapshot()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(
            host,
            client,
            "meta-pair",
            boardModel: "esp32s3_devkitc1",
            panelType: "hub75_p2_5_128x64_smd2121_scan32");

        var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
            string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(snapshot);
        Assert.Equal("esp32s3_devkitc1", snapshot!.BoardModel);
        Assert.Equal("hub75_p2_5_128x64_smd2121_scan32", snapshot.PanelType);
    }

    [Fact]
    public async Task Pairing_WithoutBoardAndPanel_ShouldRemainBackwardCompatible()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "legacy-pair");

        var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
            string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(snapshot);
        Assert.True(string.IsNullOrWhiteSpace(snapshot!.BoardModel));
        Assert.True(string.IsNullOrWhiteSpace(snapshot.PanelType));
    }

    [Fact]
    public async Task Telemetry_WithBoardAndPanel_ShouldUpdateSnapshotMetadata()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "telemetry-meta");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var telemetryJson = JsonSerializer.Serialize(new
        {
            deviceId = paired.DeviceId,
            rssi = -38,
            firmwareVersion = "1.2.3",
            ipAddress = "192.168.1.55",
            boardModel = "esp32s3_devkitc1",
            panelType = "hub75_p2_5_128x64_smd2121_scan32",
        });

        var payload = Encoding.UTF8.GetBytes(telemetryJson);
        await ws.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);

        var updated = await WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

            return snapshot is not null
                && string.Equals(snapshot.BoardModel, "esp32s3_devkitc1", StringComparison.OrdinalIgnoreCase)
                && string.Equals(snapshot.PanelType, "hub75_p2_5_128x64_smd2121_scan32", StringComparison.OrdinalIgnoreCase);
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(updated, "Snapshot nao refletiu board/panel enviados por telemetria.");
        await CloseWebSocketQuietlyAsync(ws);
    }

    [Fact]
    public async Task TelemetryMetrics_ShouldPassThroughWithoutServerNormalization_AndPersistOnRecord()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "telemetry-metrics-pass-through");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var telemetryJson = JsonSerializer.Serialize(new
        {
            deviceId = paired.DeviceId,
            uptimeSeconds = 321,
            loopLoadPercent = 135,
            freeHeapBytes = 1024,
            largestHeapBlockBytes = 4096,
            psramAvailable = true,
            freePsramBytes = 2048,
            largestPsramBlockBytes = 8192,
            wifiConnected = false,
        });

        var payload = Encoding.UTF8.GetBytes(telemetryJson);
        await ws.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);

        var telemetryApplied = await WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

            return snapshot is not null
                && snapshot.UptimeSeconds == 321
                && snapshot.LoopLoadPercent == 135
                && snapshot.FreeHeapBytes == 1024
                && snapshot.LargestHeapBlockBytes == 4096
                && snapshot.PsramAvailable == true
                && snapshot.FreePsramBytes == 2048
                && snapshot.LargestPsramBlockBytes == 8192
                && snapshot.WifiConnected == false;
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(telemetryApplied, "Telemetria com metricas estendidas nao foi refletida no snapshot no timeout esperado.");

        var snapshotAfter = host.GetDevicesSnapshot().First(d =>
            string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(321, snapshotAfter.UptimeSeconds);
        Assert.Equal(135, snapshotAfter.LoopLoadPercent);
        Assert.Equal(1024L, snapshotAfter.FreeHeapBytes);
        Assert.Equal(4096L, snapshotAfter.LargestHeapBlockBytes);
        Assert.True(snapshotAfter.PsramAvailable);
        Assert.Equal(2048L, snapshotAfter.FreePsramBytes);
        Assert.Equal(8192L, snapshotAfter.LargestPsramBlockBytes);
        Assert.False(snapshotAfter.WifiConnected);

        var recordAfter = host.GetDeviceRecords().First(d =>
            string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(321, recordAfter.UptimeSeconds);
        Assert.Equal(135, recordAfter.LoopLoadPercent);
        Assert.Equal(1024L, recordAfter.FreeHeapBytes);
        Assert.Equal(4096L, recordAfter.LargestHeapBlockBytes);
        Assert.True(recordAfter.PsramAvailable);
        Assert.Equal(2048L, recordAfter.FreePsramBytes);
        Assert.Equal(8192L, recordAfter.LargestPsramBlockBytes);
        Assert.False(recordAfter.WifiConnected);

        await CloseWebSocketQuietlyAsync(ws);
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
    public async Task LegacyWsQueryToken_ShouldBeAccepted_WhenEnabled()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AllowLegacyWebSocketQueryToken = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-legacy-enabled");

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream?deviceId={paired.DeviceId}&token={paired.Token}"), CancellationToken.None);

        Assert.Equal(WebSocketState.Open, ws.State);
        await CloseWebSocketQuietlyAsync(ws);
    }

    [Fact]
    public async Task LegacyWsQueryToken_ShouldBeRejected_ByDefault()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-legacy-default-disabled");

        using var ws = new ClientWebSocket();
        var ex = await Assert.ThrowsAsync<WebSocketException>(() =>
            ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream?deviceId={paired.DeviceId}&token={paired.Token}"), CancellationToken.None));

        Assert.Contains("401", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LegacyWsQueryToken_ShouldBeRejected_WhenDisabled()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AllowLegacyWebSocketQueryToken = false,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-legacy-disabled");

        using var ws = new ClientWebSocket();

        var ex = await Assert.ThrowsAsync<WebSocketException>(() =>
            ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream?deviceId={paired.DeviceId}&token={paired.Token}"), CancellationToken.None));

        Assert.Contains("401", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebSocketHeaderToken_ShouldBeAccepted_WhenLegacyDisabled()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AllowLegacyWebSocketQueryToken = false,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-header-legacy-disabled");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        Assert.Equal(WebSocketState.Open, ws.State);
        await CloseWebSocketQuietlyAsync(ws);
    }

    [Fact]
    public async Task LargeRequestBody_Pair_ShouldReturn413Or400()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            MaxJsonBodyBytes = 256,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var oversizedName = new string('a', 4_096);
        var body = JsonSerializer.Serialize(new PairDeviceRequest
        {
            PairingCode = "123456",
            DeviceName = oversizedName,
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/pair", content);

        Assert.True(
            response.StatusCode == HttpStatusCode.RequestEntityTooLarge || response.StatusCode == HttpStatusCode.BadRequest,
            $"Unexpected status code: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task LargeRequestBody_CommandAck_ShouldReturn413Or400()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            MaxJsonBodyBytes = 256,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ack-oversized");

        var body = JsonSerializer.Serialize(new DeviceCommandAckRequest
        {
            CommandId = "cmd-1",
            Success = true,
            Message = new string('x', 8_192),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/device/command-ack");
        request.Headers.Add("X-Device-Id", paired.DeviceId);
        request.Headers.Add("X-Device-Token", paired.Token);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.RequestEntityTooLarge || response.StatusCode == HttpStatusCode.BadRequest,
            $"Unexpected status code: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ResponseHeaders_ShouldContainSecurityHeaders()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var response = await client.GetAsync("/api/v1/health");
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniffValues));
        Assert.Equal("nosniff", Assert.Single(nosniffValues));

        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var xFrameValues));
        Assert.Equal("DENY", Assert.Single(xFrameValues));

        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicyValues));
        Assert.Equal("no-referrer", Assert.Single(referrerPolicyValues));

        Assert.True(response.Headers.TryGetValues("Cache-Control", out var cacheControlValues));
        Assert.Equal("no-store", Assert.Single(cacheControlValues));
    }

    [Fact]
    public async Task WebSocketMultiFrameText_ShouldNotTruncateMessage()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            MaxWebSocketMessageBytes = 64 * 1024,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-fragmented");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var longAppName = "weather-" + new string('z', 6_000);
        var telemetryJson = JsonSerializer.Serialize(new
        {
            deviceId = paired.DeviceId,
            rssi = -40,
            firmwareVersion = "1.0.0",
            ipAddress = "192.168.1.10",
            activeAppId = "accuweather",
            activeAppName = longAppName,
        });

        var payload = Encoding.UTF8.GetBytes(telemetryJson);
        var firstChunk = Math.Min(3000, payload.Length - 1);

        await ws.SendAsync(new ArraySegment<byte>(payload, 0, firstChunk), WebSocketMessageType.Text, false, CancellationToken.None);
        await ws.SendAsync(new ArraySegment<byte>(payload, firstChunk, payload.Length - firstChunk), WebSocketMessageType.Text, true, CancellationToken.None);

        var updated = await WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d => string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
            return snapshot is not null && string.Equals(snapshot.ActiveAppName, longAppName, StringComparison.Ordinal);
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(updated, "Telemetry fragmentada nao foi processada integralmente no timeout esperado.");

        await CloseWebSocketQuietlyAsync(ws);
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

        var paired = await PairDeviceAsync(host, client, "pending-stop");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

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

    [Fact]
    public async Task WebSocketAuthentication_ShouldStampLastAuthUtc_WhenTokenIsValid()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-auth-stamp");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var stamped = await WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

            return snapshot is not null
                && snapshot.LastAuthUtc.HasValue
                && snapshot.FirstSeenUtc.HasValue
                && snapshot.ConfigState == DeviceConfigState.KnownGood;
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(stamped, "Autenticacao WebSocket nao atualizou LastAuthUtc/FirstSeenUtc/ConfigState no timeout esperado.");
        await CloseWebSocketQuietlyAsync(ws);
    }

    [Fact]
    public async Task WebSocketDetach_ShouldKeepDeviceOnlineDuringShortGracePeriod()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-detach-grace");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var onlineBeforeDetach = await WaitForConditionAsync(() =>
            GetDeviceStatus(host, paired.DeviceId) == DeviceStatus.Online,
            timeout: TimeSpan.FromSeconds(5));

        Assert.True(onlineBeforeDetach, "Device nao entrou em estado online antes do teste de grace period.");

        ws.Abort();

        await Task.Delay(200);
        Assert.Equal(DeviceStatus.Online, GetDeviceStatus(host, paired.DeviceId));

        var offlineAfterGrace = await WaitForConditionAsync(() =>
            GetDeviceStatus(host, paired.DeviceId) == DeviceStatus.Offline,
            timeout: TimeSpan.FromSeconds(5));

        Assert.True(offlineAfterGrace, "Device nao transitou para offline apos o grace period de detach.");
    }

    [Fact]
    public async Task ClosingOldSocket_ShouldNotDetachNewSocketConnection()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-identity-detach");

        using var ws1 = new ClientWebSocket();
        ws1.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws1.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws1.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var onlineWithFirstSocket = await WaitForConditionAsync(() =>
            GetDeviceStatus(host, paired.DeviceId) == DeviceStatus.Online,
            timeout: TimeSpan.FromSeconds(5));
        Assert.True(onlineWithFirstSocket, "Device nao entrou em online com o primeiro socket.");

        using var ws2 = new ClientWebSocket();
        ws2.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws2.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws2.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var onlineWithSecondSocket = await WaitForConditionAsync(() =>
            GetDeviceStatus(host, paired.DeviceId) == DeviceStatus.Online,
            timeout: TimeSpan.FromSeconds(5));
        Assert.True(onlineWithSecondSocket, "Device nao permaneceu online apos abrir o segundo socket.");

        await CloseWebSocketQuietlyAsync(ws1);

        await Task.Delay(900);
        Assert.Equal(DeviceStatus.Online, GetDeviceStatus(host, paired.DeviceId));

        await CloseWebSocketQuietlyAsync(ws2);

        var offlineAfterBothClosed = await WaitForConditionAsync(() =>
            GetDeviceStatus(host, paired.DeviceId) == DeviceStatus.Offline,
            timeout: TimeSpan.FromSeconds(5));

        Assert.True(offlineAfterBothClosed, "Device nao foi para offline apos fechar os dois sockets.");
    }

    [Fact]
    public async Task TelemetryProcessing_ShouldStampLastTelemetryUtc_WithoutOwningLastAuthUtc()
    {
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await PairDeviceAsync(host, client, "ws-telemetry-stamp");

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        ws.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        var authStamped = await WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

            return snapshot is not null && snapshot.LastAuthUtc.HasValue;
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(authStamped, "LastAuthUtc nao foi preenchido antes da telemetria.");

        var beforeTelemetry = host.GetDevicesSnapshot().First(d =>
            string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
        var authUtc = beforeTelemetry.LastAuthUtc;

        var telemetryJson = JsonSerializer.Serialize(new
        {
            deviceId = paired.DeviceId,
            rssi = -47,
            firmwareVersion = "2.0.0",
            ipAddress = "192.168.1.88",
            activeAppId = "clock",
            activeAppName = "Relogio",
        });

        var payload = Encoding.UTF8.GetBytes(telemetryJson);
        await ws.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);

        var telemetryStamped = await WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

            return snapshot is not null
                && snapshot.LastTelemetryUtc.HasValue
                && snapshot.LastAuthUtc == authUtc;
        }, timeout: TimeSpan.FromSeconds(5));

        Assert.True(telemetryStamped, "Telemetria nao atualizou LastTelemetryUtc preservando LastAuthUtc no timeout esperado.");
        await CloseWebSocketQuietlyAsync(ws);
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
                // Server can close without close handshake during shutdown/auth tests.
            }
        }

        ws.Abort();
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

    private static async Task<PairDeviceResponse> PairDeviceAsync(
        DeviceServerHost host,
        HttpClient client,
        string deviceName,
        string? boardModel = null,
        string? panelType = null)
    {
        var pairing = host.CreatePairingCode(TimeSpan.FromMinutes(5));
        var pairedResponse = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = pairing.Code,
            DeviceName = deviceName,
            BoardModel = boardModel,
            PanelType = panelType,
        });

        pairedResponse.EnsureSuccessStatusCode();
        var paired = await pairedResponse.Content.ReadFromJsonAsync<PairDeviceResponse>();

        Assert.NotNull(paired);
        Assert.False(string.IsNullOrWhiteSpace(paired!.DeviceId));
        Assert.False(string.IsNullOrWhiteSpace(paired.Token));
        return paired;
    }

    private static async Task<bool> WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return predicate();
    }

    private static DeviceStatus? GetDeviceStatus(DeviceServerHost host, string deviceId)
    {
        return host.GetDevicesSnapshot().FirstOrDefault(d =>
            string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))?.Status;
    }
}



