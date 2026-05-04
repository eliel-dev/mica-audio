using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

// DOCS: docs/wiki/modules/device-server-protocol.md
public sealed class DeviceServerHostDashboardTests
{
    [Fact]
    public async Task DashboardEndpoint_ShouldServeHtmlAndStaticAssets()
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
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        var htmlResponse = await client.GetAsync("/dashboard");
        var htmlBody = await htmlResponse.Content.ReadAsStringAsync();
        Assert.True(htmlResponse.IsSuccessStatusCode, $"/dashboard falhou com {(int)htmlResponse.StatusCode}: {htmlBody}");
        var css = await client.GetStringAsync("/dashboard/dashboard.css");
        var js = await client.GetStringAsync("/dashboard/dashboard.js");

        Assert.Contains("panel-detail", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"espdash-sec\" style=\"display:none;\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"s-fps\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"s-rssi\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"m-health\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"m-temp\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"firmware-row\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"firmware-current\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"firmware-latest\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"btn-fw\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("Firmware atual", htmlBody, StringComparison.Ordinal);
        Assert.Contains("Ultimo release", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"chart-loop\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("id=\"chart-heap\"", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"btn-led-secondary\"", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"btn-rm-secondary\"", htmlBody, StringComparison.Ordinal);
        Assert.Contains("--bg-gradient", css, StringComparison.Ordinal);
        Assert.DoesNotContain("#espdash-sec", css, StringComparison.Ordinal);
        Assert.Contains("selectDevice", js, StringComparison.Ordinal);
        Assert.Contains("HEAP_TOTAL_BYTES_FALLBACK = 320000", js, StringComparison.Ordinal);
        Assert.Contains("PSRAM_TOTAL_BYTES_FALLBACK = 8000000", js, StringComparison.Ordinal);
        Assert.Contains("resolveHealthState", js, StringComparison.Ordinal);
        Assert.Contains("resolveHeapPercent", js, StringComparison.Ordinal);
        Assert.Contains("resolvePsramPercent", js, StringComparison.Ordinal);
        Assert.Contains("loopHealthyPercent", js, StringComparison.Ordinal);
        Assert.Contains("chipTemperatureCelsius", js, StringComparison.Ordinal);
        Assert.Contains("firmwareUpdateAvailable", js, StringComparison.Ordinal);
        Assert.Contains("latestFirmwareVersion", js, StringComparison.Ordinal);
        Assert.Contains("Firmware atual nao identificado", js, StringComparison.Ordinal);
        Assert.Contains("Sem release oficial", js, StringComparison.Ordinal);
        Assert.Contains("update-firmware", js, StringComparison.Ordinal);
        Assert.DoesNotContain("device.loopLoadPercent", js, StringComparison.Ordinal);
        Assert.Contains("style.display = selectedDeviceId ? \"block\" : \"none\"", js, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DashboardWebSocket_ShouldProjectDedicatedDto_AndPublishUpdates()
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
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "dashboard-ws");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);

        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.PublishStatsAsync(mqttClient, paired.DeviceId, new
        {
            deviceId = paired.DeviceId,
            chipModel = "ESP32-S3",
            chipRevision = 2,
            chipCores = 2,
            cpuFreqMHz = 240,
            sdkVersion = "5.5.0",
            heapTotalBytes = 327680,
            psramTotalBytes = 8388608,
            flashTotalBytes = 16777216,
            sketchSizeBytes = 901120,
            freeSketchBytes = 9437184,
        });
        await DeviceServerTestHarness.PublishTelemetryAsync(mqttClient, paired.DeviceId, new
        {
            deviceId = paired.DeviceId,
            rssi = -52,
            uptimeSeconds = 123,
            telemetrySequence = 7,
            loopHealthyPercent = 92,
            chipTemperatureCelsius = 48.5,
            freeHeapBytes = 204800,
            largestHeapBlockBytes = 174080,
            psramAvailable = true,
            freePsramBytes = 6553600,
            largestPsramBlockBytes = 6225920,
            wifiConnected = true,
            wifiState = "connected",
            testLedAvailable = true,
            testLedEnabled = false,
            testLedDuty = 0,
            brightnessCap = 160,
            brightnessRequested = 120,
            brightnessApplied = 100,
            activeAppName = "visualizer-hub75",
            streamFramesReceived = 60,
            streamFramesApplied = 60,
            hub75PresentFrames = 60,
            streamSequenceGapCount = 0,
            streamInvalidFrameCount = 0,
            streamLastSequence = 60,
        });

        var snapshotReady = await DeviceServerTestHarness.WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(device =>
                string.Equals(device.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
            return snapshot is not null
                && snapshot.Status == DeviceStatus.Online
                && snapshot.TelemetrySequence == 7
                && snapshot.HeapTotalBytes == 327680
                && snapshot.ActiveAppName == "visualizer-hub75";
        }, TimeSpan.FromSeconds(5));

        Assert.True(snapshotReady, "Snapshot de origem do dashboard nao foi preparado no timeout esperado.");

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/device/{paired.DeviceId}"), CancellationToken.None);

        using var firstPayload = JsonDocument.Parse(await ReceiveTextMessageAsync(ws));
        Assert.Equal(paired.DeviceId, firstPayload.RootElement.GetProperty("deviceId").GetString());
        Assert.Equal("dashboard-ws", firstPayload.RootElement.GetProperty("name").GetString());
        Assert.True(firstPayload.RootElement.GetProperty("online").GetBoolean());
        Assert.Equal("visualizer-hub75", firstPayload.RootElement.GetProperty("activeAppName").GetString());
        Assert.Equal(92, firstPayload.RootElement.GetProperty("loopHealthyPercent").GetInt32());
        Assert.Equal(48.5d, firstPayload.RootElement.GetProperty("chipTemperatureCelsius").GetDouble());
        Assert.Equal(JsonValueKind.Null, firstPayload.RootElement.GetProperty("loopLoadPercent").ValueKind);
        Assert.Equal(62, firstPayload.RootElement.GetProperty("heapFreePercent").GetInt32());
        Assert.Equal(78, firstPayload.RootElement.GetProperty("psramFreePercent").GetInt32());
        Assert.Equal(15, firstPayload.RootElement.GetProperty("heapFragmentationPercent").GetInt32());
        Assert.True(firstPayload.RootElement.GetProperty("psramAvailable").GetBoolean());

        await Task.Delay(350);
        await DeviceServerTestHarness.PublishTelemetryAsync(mqttClient, paired.DeviceId, new
        {
            deviceId = paired.DeviceId,
            rssi = -49,
            uptimeSeconds = 125,
            telemetrySequence = 8,
            loopHealthyPercent = 74,
            chipTemperatureCelsius = 50.0,
            freeHeapBytes = 196608,
            largestHeapBlockBytes = 167936,
            psramAvailable = true,
            freePsramBytes = 6488064,
            largestPsramBlockBytes = 6160384,
            wifiConnected = true,
            wifiState = "connected",
            testLedAvailable = true,
            testLedEnabled = false,
            testLedDuty = 0,
            brightnessCap = 160,
            brightnessRequested = 120,
            brightnessApplied = 100,
            activeAppName = "visualizer-hub75",
            streamFramesReceived = 120,
            streamFramesApplied = 60,
            hub75PresentFrames = 81,
            streamSequenceGapCount = 0,
            streamInvalidFrameCount = 0,
            streamLastSequence = 120,
        });

        using var secondPayload = JsonDocument.Parse(await ReceiveTextMessageAsync(ws));
        Assert.Equal(8u, secondPayload.RootElement.GetProperty("telemetrySequence").GetUInt32());
        Assert.Equal(74, secondPayload.RootElement.GetProperty("loopHealthyPercent").GetInt32());
        Assert.Equal(50d, secondPayload.RootElement.GetProperty("chipTemperatureCelsius").GetDouble());
        Assert.Equal(60u, secondPayload.RootElement.GetProperty("streamFramesApplied").GetUInt32());
        Assert.True(secondPayload.RootElement.GetProperty("hub75Fps").GetInt32() > 0);
    }

    private static async Task<JsonDocument> WaitForDashboardPayloadAsync(ClientWebSocket ws, long previousTelemetrySequence)
    {
        while (true)
        {
            var payload = JsonDocument.Parse(await ReceiveTextMessageAsync(ws));
            var seq = payload.RootElement.GetProperty("telemetrySequence").GetInt64();
            if (seq > previousTelemetrySequence)
            {
                return payload;
            }
        }
    }

    private static async Task<string> ReceiveTextMessageAsync(ClientWebSocket ws)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("O dashboard encerrou o WebSocket antes do payload esperado.");
            }

            if (result.Count > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, result.Count));
            }

            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task DashboardWebSocket_ShouldKeepMemoryPercentsNullUntilStatsArrive()
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
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "dashboard-no-stats");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);

        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.PublishTelemetryAsync(mqttClient, paired.DeviceId, new
        {
            deviceId = paired.DeviceId,
            rssi = -61,
            uptimeSeconds = 44,
            telemetrySequence = 2,
            loopHealthyPercent = 88,
            freeHeapBytes = 180224,
            largestHeapBlockBytes = 131072,
            psramAvailable = true,
            freePsramBytes = 4194304,
            largestPsramBlockBytes = 3145728,
            wifiConnected = true,
            wifiState = "connected",
            activeAppName = "visualizer-hub75",
        });

        var snapshotReady = await DeviceServerTestHarness.WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(device =>
                string.Equals(device.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
            return snapshot is not null
                && snapshot.Status == DeviceStatus.Online
                && snapshot.TelemetrySequence == 2
                && snapshot.FreeHeapBytes == 180224
                && snapshot.HeapTotalBytes is null;
        }, TimeSpan.FromSeconds(5));

        Assert.True(snapshotReady, "Snapshot sem stats nao foi preparado no timeout esperado.");

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/device/{paired.DeviceId}"), CancellationToken.None);

        using var payload = JsonDocument.Parse(await ReceiveTextMessageAsync(ws));
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("heapTotalBytes").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("heapFreePercent").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("psramTotalBytes").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("psramFreePercent").ValueKind);
        Assert.Equal(88, payload.RootElement.GetProperty("loopHealthyPercent").GetInt32());
        Assert.Equal(180224L, payload.RootElement.GetProperty("heapFreeBytes").GetInt64());
        Assert.Equal(4194304L, payload.RootElement.GetProperty("psramFreeBytes").GetInt64());
    }

}
