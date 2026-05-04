using System.Buffers;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using MQTTnet;
using MQTTnet.Protocol;

namespace Output.Tests;

// DOCS: docs/wiki/modules/device-server-protocol.md#contrato-mqtt-de-controle
public sealed class DeviceServerHostMqttTests
{
    [Fact]
    public async Task MqttSessionShadowPublish_ShouldUpdateSessionFieldsInSnapshot()
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
            MqttRootTopic = DeviceServerTestHarness.DefaultMqttRootTopic,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-shadow");
        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);

        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.PublishSessionShadowAsync(mqttClient, paired.DeviceId, new DeviceSessionShadowMessage
        {
            DeviceId = paired.DeviceId,
            ShadowVersion = 3,
            Mode = "visualizer",
            ActiveClientId = "win-eliel-1234abcd",
            ActiveOwnerEpoch = 7,
            OwnerLeaseRemainingMs = 4200,
            LockHeld = true,
            LockClientId = "win-eliel-1234abcd",
            LockReason = "settings",
            LockLeaseRemainingMs = 11000,
            ActiveAppId = "visualizer-hub75",
            FallbackState = "none",
        });

        var updated = await DeviceServerTestHarness.WaitForConditionAsync(
            () => host.GetDevicesSnapshot().Any(snapshot =>
                string.Equals(snapshot.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase)
                && snapshot.SessionActiveOwnerEpoch == 7),
            TimeSpan.FromSeconds(3));

        Assert.True(updated, "Snapshot nao refletiu o shadow MQTT de sessao.");
        var device = Assert.Single(host.GetDevicesSnapshot(), snapshot =>
            string.Equals(snapshot.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("visualizer", device.SessionMode);
        Assert.Equal("win-eliel-1234abcd", device.SessionActiveClientId);
        Assert.Equal((uint)7, device.SessionActiveOwnerEpoch);
        Assert.Equal(4200, device.SessionOwnerLeaseRemainingMs);
        Assert.True(device.SessionLockHeld);
        Assert.Equal("win-eliel-1234abcd", device.SessionLockClientId);
        Assert.Equal("settings", device.SessionLockReason);
        Assert.Equal(11000, device.SessionLockLeaseRemainingMs);
        Assert.Equal("none", device.SessionFallbackState);
    }

    [Fact]
    public async Task SendTrackedCommandAsync_WithSessionContext_ShouldPublishMqttCommandSessionFields()
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
            MqttRootTopic = DeviceServerTestHarness.DefaultMqttRootTopic,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-session-command");
        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);

        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.SubscribeAsync(mqttClient, DeviceServerTestHarness.GetCommandsTopic(paired.DeviceId));

        JsonElement? command = null;
        mqttClient.ApplicationMessageReceivedAsync += args =>
        {
            command = JsonSerializer.Deserialize<JsonElement>(DeviceServerTestHarness.DecodePayload(args));
            var commandId = command.Value.GetProperty("commandId").GetString();
            return DeviceServerTestHarness.PublishCommandEventAsync(
                mqttClient,
                paired.DeviceId,
                commandId!,
                success: true,
                progressPercent: 100,
                stage: "done",
                message: "ok");
        };

        var result = await host.SendCommandTrackedAsync(
            paired.DeviceId,
            DeviceCommandType.SetBrightness,
            new Dictionary<string, string> { ["brightness"] = "120" },
            new DeviceCommandSessionContext("win-eliel-1234abcd", 7, "lock-token"),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(command);
        Assert.Equal("win-eliel-1234abcd", command.Value.GetProperty("clientId").GetString());
        Assert.Equal(7u, command.Value.GetProperty("ownerEpoch").GetUInt32());
        Assert.Equal("lock-token", command.Value.GetProperty("lockToken").GetString());
        Assert.Equal("set_brightness", command.Value.GetProperty("command").GetString());
    }

    [Fact]
    public async Task PairingAndServerInfo_ShouldPreserveExternalRequestHostPort()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        const string externalHostHeader = "192.168.15.20:5272";

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = string.Empty,
            Port = port,
            MqttPort = mqttPort,
            MqttRootTopic = DeviceServerTestHarness.DefaultMqttRootTopic,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(
            host,
            client,
            "mqtt-external-port",
            hostHeader: externalHostHeader);

        using var infoRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/server/info");
        infoRequest.Headers.Host = externalHostHeader;
        using var infoResponse = await client.SendAsync(infoRequest);
        infoResponse.EnsureSuccessStatusCode();
        var serverInfo = await infoResponse.Content.ReadFromJsonAsync<ServerInfoResponse>();

        Assert.Equal("http://192.168.15.20:5272", paired.HttpBase);
        Assert.Equal("192.168.15.20", paired.MqttHost);
        Assert.NotNull(serverInfo);
        Assert.Equal("http://192.168.15.20:5272", serverInfo!.HttpBase);
        Assert.Equal("192.168.15.20", serverInfo.MqttHost);
    }

    [Fact]
    public async Task PairingAndServerInfo_ShouldUseConfiguredPublicHttpBaseAddress()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();

        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = string.Empty,
            PublicHttpBaseAddress = "http://192.168.15.30:5272",
            Port = port,
            MqttPort = mqttPort,
            MqttRootTopic = DeviceServerTestHarness.DefaultMqttRootTopic,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-public-base");
        var serverInfo = await client.GetFromJsonAsync<ServerInfoResponse>("/api/v1/server/info");

        Assert.Equal("http://192.168.15.30:5272", paired.HttpBase);
        Assert.Equal("192.168.15.30", paired.MqttHost);
        Assert.NotNull(serverInfo);
        Assert.Equal("http://192.168.15.30:5272", serverInfo!.HttpBase);
        Assert.Equal("192.168.15.30", serverInfo.MqttHost);
    }

    [Fact]
    public async Task PairingAndServerInfo_ShouldExposeMqttControlPlaneSettings()
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
            MqttRootTopic = DeviceServerTestHarness.DefaultMqttRootTopic,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-contract");
        var serverInfo = await client.GetFromJsonAsync<ServerInfoResponse>("/api/v1/server/info");

        Assert.Equal("127.0.0.1", paired.MqttHost);
        Assert.Equal(mqttPort, paired.MqttPort);
        Assert.Equal(DeviceServerTestHarness.DefaultMqttRootTopic, paired.MqttRootTopic);
        Assert.NotNull(serverInfo);
        Assert.Equal("127.0.0.1", serverInfo!.MqttHost);
        Assert.Equal(mqttPort, serverInfo.MqttPort);
        Assert.Equal(DeviceServerTestHarness.DefaultMqttRootTopic, serverInfo.MqttRootTopic);
    }

    [Fact]
    public async Task MqttAuthentication_ShouldAcceptValidCredentials_AndRejectInvalidCredentials()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-auth");

        using var validClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);

        using var invalidClient = new MqttClientFactory().CreateMqttClient();
        var invalidResult = await invalidClient.ConnectAsync(
            new MqttClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", mqttPort)
                .WithClientId(paired.DeviceId)
                .WithCredentials(paired.DeviceId, "wrong-token")
                .WithCleanSession(true)
                .Build(),
            CancellationToken.None);

        Assert.Equal(MqttClientConnectResultCode.BadUserNameOrPassword, invalidResult.ResultCode);
    }

    [Fact]
    public async Task MqttDisconnect_ShouldRespectGracePeriod_BeforeMarkingDeviceOffline()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero));
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();

        await using var host = new DeviceServerHost(timeProvider);
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = mqttPort,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-grace");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");

        var becameOnline = await DeviceServerTestHarness.WaitForConditionAsync(
            () => DeviceServerTestHarness.GetDeviceStatus(host, paired.DeviceId) == DeviceStatus.Online,
            TimeSpan.FromSeconds(5));
        Assert.True(becameOnline, "Device nao entrou em online apos conectar o control plane MQTT.");

        await mqttClient.DisconnectAsync();

        Assert.Equal(DeviceStatus.Online, DeviceServerTestHarness.GetDeviceStatus(host, paired.DeviceId));

        timeProvider.Advance(TimeSpan.FromMilliseconds(600));
        var becameOffline = await DeviceServerTestHarness.WaitForConditionAsync(
            () => DeviceServerTestHarness.GetDeviceStatus(host, paired.DeviceId) == DeviceStatus.Offline,
            TimeSpan.FromSeconds(5));
        Assert.True(becameOffline, "Device nao transitou para offline apos o grace period do control plane MQTT.");
    }

    [Fact]
    public async Task MqttStatusPublish_ShouldUpdateSnapshot()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-status");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.PublishTelemetryAsync(mqttClient, paired.DeviceId, new
        {
            deviceId = paired.DeviceId,
            firmwareVersion = "9.9.9",
            ipAddress = "192.168.1.77",
            uptimeSeconds = 123,
            loopHealthyPercent = 91,
            brightnessApplied = 99,
            chipTemperatureCelsius = 47.25,
        });

        var updated = await DeviceServerTestHarness.WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

            return snapshot is not null
                && snapshot.Status == DeviceStatus.Online
                && snapshot.ControlPlaneState == DeviceControlPlaneState.MqttOnline
                && snapshot.UptimeSeconds == 123
                && snapshot.LoopHealthyPercent == 91
                && snapshot.BrightnessApplied == 99
                && snapshot.ChipTemperatureCelsius == 47.25d
                && string.Equals(snapshot.FirmwareVersion, "9.9.9", StringComparison.Ordinal)
                && string.Equals(snapshot.LastKnownIp, "192.168.1.77", StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(5));

        Assert.True(updated, "Snapshot nao refletiu o status MQTT publicado no timeout esperado.");
    }

    [Fact]
    public async Task MqttStatsPublish_ShouldUpdateSnapshotAndPersistRetainedState()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-stats");

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

        var updated = await DeviceServerTestHarness.WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));

            return snapshot is not null
                && string.Equals(snapshot.ChipModel, "ESP32-S3", StringComparison.Ordinal)
                && snapshot.ChipRevision == 2
                && snapshot.CpuFreqMHz == 240
                && snapshot.HeapTotalBytes == 327680L
                && snapshot.FreeSketchBytes == 9437184L;
        }, TimeSpan.FromSeconds(5));

        Assert.True(updated, "Snapshot nao refletiu o payload MQTT de stats.");
    }

    [Fact]
    public async Task MqttDeviceLogPublish_ShouldEmitStructuredDeviceLogEvent()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-logs");

        var received = new TaskCompletionSource<DeviceLogMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.DeviceLogReceived += (_, log) =>
        {
            if (string.Equals(log.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                received.TrySetResult(log);
            }
        };

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.PublishDeviceLogAsync(mqttClient, paired.DeviceId, new
        {
            deviceId = paired.DeviceId,
            sequence = 5,
            level = "error",
            category = "mqtt",
            eventCode = "mqtt_connect_failed",
            message = "Falha ao autenticar no broker.",
            uptimeSeconds = 45,
            telemetrySequence = 7,
        });

        var logMessage = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(5u, logMessage.Sequence);
        Assert.Equal("error", logMessage.Level);
        Assert.Equal("mqtt", logMessage.Category);
        Assert.Equal("mqtt_connect_failed", logMessage.EventCode);
    }

    [Fact]
    public async Task MqttStatsAndLogs_ShouldRejectInvalidPayloads()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-invalid");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");

        var invalidStats = await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(DeviceServerTestHarness.GetStatsTopic(paired.DeviceId))
            .WithPayload(Encoding.UTF8.GetBytes("{"))
            .WithRetainFlag(true)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), CancellationToken.None);
        var invalidLogs = await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(DeviceServerTestHarness.GetLogsTopic(paired.DeviceId))
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(new
            {
                deviceId = paired.DeviceId,
                sequence = 0,
                level = "info",
                category = "wifi",
                message = "",
            }))
            .WithRetainFlag(false)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), CancellationToken.None);

        Assert.Equal(MqttClientPublishReasonCode.UnspecifiedError, invalidStats.ReasonCode);
        Assert.Equal(MqttClientPublishReasonCode.UnspecifiedError, invalidLogs.ReasonCode);
    }

    [Fact]
    public async Task SendTrackedCommandAsync_ShouldTimeout_WhenDeviceStaysSilent()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        using var activityCapture = new ActivityCapture(DeviceServerObservability.ActivitySourceName);
        using var metricCapture = new MetricCapture(DeviceServerObservability.MeterName);

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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-timeout");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");

        var result = await host.SendCommandTrackedAsync(
            paired.DeviceId,
            DeviceCommandType.TestLed,
            timeout: TimeSpan.FromMilliseconds(200));

        Assert.True(result.Accepted);
        Assert.True(result.Completed);
        Assert.False(result.Success);
        Assert.Equal("timeout", result.Stage);
        Assert.Equal("timeout", result.ErrorCode);

        var activity = Assert.Single(activityCapture.CompletedActivities, item => item.OperationName == "device.command.tracked");
        Assert.Equal(paired.DeviceId, activity.GetTagItem(DeviceServerObservability.DeviceIdKey));
        Assert.Equal(result.CommandId, activity.GetTagItem(DeviceServerObservability.CommandIdKey));
        Assert.Equal(DeviceCommandType.TestLed.ToString(), activity.GetTagItem(DeviceServerObservability.CommandTypeKey));
        Assert.Equal("timeout", activity.GetTagItem("command.stage"));

        Assert.Contains(metricCapture.Measurements, item =>
            item.InstrumentName == "mica.device.command.timeout.count"
            && Equals(item.Tags[DeviceServerObservability.DeviceIdKey], paired.DeviceId));
        Assert.Contains(metricCapture.Measurements, item =>
            item.InstrumentName == "mica.device.command.duration"
            && Equals(item.Tags["stage"], "timeout"));
    }

    [Fact]
    public async Task SendTrackedCommandAsync_ShouldPreserveCorrelation_WhenAckArrivesOverMqtt()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        using var activityCapture = new ActivityCapture(DeviceServerObservability.ActivitySourceName);
        using var metricCapture = new MetricCapture(DeviceServerObservability.MeterName);

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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-success");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.SubscribeAsync(mqttClient, DeviceServerTestHarness.GetCommandsTopic(paired.DeviceId));

        var seenCommandId = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        mqttClient.ApplicationMessageReceivedAsync += async args =>
        {
            if (!string.Equals(args.ApplicationMessage.Topic, DeviceServerTestHarness.GetCommandsTopic(paired.DeviceId), StringComparison.Ordinal))
            {
                return;
            }

            var payloadBytes = new byte[args.ApplicationMessage.Payload.Length];
            args.ApplicationMessage.Payload.CopyTo(payloadBytes);
            using var doc = JsonDocument.Parse(payloadBytes);
            var commandId = doc.RootElement.GetProperty("commandId").GetString();
            if (!string.IsNullOrWhiteSpace(commandId) && seenCommandId.TrySetResult(commandId))
            {
                await DeviceServerTestHarness.PublishCommandEventAsync(mqttClient, paired.DeviceId, commandId, success: true, progressPercent: 100, stage: "ack", message: "ok");
            }
        };

        var result = await host.SendCommandTrackedAsync(
            paired.DeviceId,
            DeviceCommandType.TestLed,
            timeout: TimeSpan.FromSeconds(2));
        var observedCommandId = await seenCommandId.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Accepted);
        Assert.True(result.Completed);
        Assert.True(result.Success);
        Assert.Equal(observedCommandId, result.CommandId);

        var activity = Assert.Single(activityCapture.CompletedActivities, item => item.OperationName == "device.command.tracked");
        Assert.Equal(paired.DeviceId, activity.GetTagItem(DeviceServerObservability.DeviceIdKey));
        Assert.Equal(observedCommandId, activity.GetTagItem(DeviceServerObservability.CommandIdKey));
        Assert.Equal("ack", activity.GetTagItem("command.stage"));
        Assert.Contains(activity.Events, item => item.Name == "command.completed");

        Assert.Contains(metricCapture.Measurements, item =>
            item.InstrumentName == "mica.device.command.duration"
            && Equals(item.Tags["stage"], "ack")
            && Equals(item.Tags["success"], true));
    }

    [Fact]
    public async Task RemoveDevice_ShouldClearRetainedStatusPresenceAndStats()
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
        var target = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-retained-target");
        var inspector = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-retained-inspector");

        using var targetClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            target.DeviceId,
            target.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(targetClient, target.DeviceId, "online");
        await DeviceServerTestHarness.PublishTelemetryAsync(targetClient, target.DeviceId, new
        {
            deviceId = target.DeviceId,
            uptimeSeconds = 55,
        });
        await DeviceServerTestHarness.PublishStatsAsync(targetClient, target.DeviceId, new
        {
            deviceId = target.DeviceId,
            chipModel = "ESP32-S3",
            heapTotalBytes = 327680,
        });

        var retainedReady = await DeviceServerTestHarness.WaitForConditionAsync(() =>
        {
            var snapshot = host.GetDevicesSnapshot().FirstOrDefault(d =>
                string.Equals(d.DeviceId, target.DeviceId, StringComparison.OrdinalIgnoreCase));
            return snapshot is not null && snapshot.UptimeSeconds == 55;
        }, TimeSpan.FromSeconds(5));
        Assert.True(retainedReady, "Snapshot nao recebeu o estado MQTT antes da remocao.");

        Assert.True(host.RemoveDevice(target.DeviceId));

        using var inspectorClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            inspector.DeviceId,
            inspector.Token);

        var retainedMessages = new List<(string Topic, string Payload)>();
        inspectorClient.ApplicationMessageReceivedAsync += args =>
        {
            retainedMessages.Add((args.ApplicationMessage.Topic, DeviceServerTestHarness.DecodePayload(args)));
            return Task.CompletedTask;
        };

        await DeviceServerTestHarness.SubscribeAsync(inspectorClient, DeviceServerTestHarness.GetPresenceTopic(target.DeviceId));
        await DeviceServerTestHarness.SubscribeAsync(inspectorClient, DeviceServerTestHarness.GetStatusTopic(target.DeviceId));
        await DeviceServerTestHarness.SubscribeAsync(inspectorClient, DeviceServerTestHarness.GetStatsTopic(target.DeviceId));
        await Task.Delay(500);

        Assert.Empty(retainedMessages);
    }

    [Fact]
    public async Task SendTrackedCommandAsync_ShouldWaitForValidatedFirmwareStage()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-update-firmware");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.SubscribeAsync(mqttClient, DeviceServerTestHarness.GetCommandsTopic(paired.DeviceId));

        var seenCommand = new TaskCompletionSource<(string CommandId, string Command, string Version)>(TaskCreationOptions.RunContinuationsAsynchronously);
        mqttClient.ApplicationMessageReceivedAsync += async args =>
        {
            if (!string.Equals(args.ApplicationMessage.Topic, DeviceServerTestHarness.GetCommandsTopic(paired.DeviceId), StringComparison.Ordinal))
            {
                return;
            }

            var payloadBytes2 = new byte[args.ApplicationMessage.Payload.Length];
            args.ApplicationMessage.Payload.CopyTo(payloadBytes2);
            using var payload = JsonDocument.Parse(payloadBytes2);
            var commandId = payload.RootElement.GetProperty("commandId").GetString() ?? string.Empty;
            var command = payload.RootElement.GetProperty("command").GetString() ?? string.Empty;
            var version = payload.RootElement.GetProperty("parameters").GetProperty("version").GetString() ?? string.Empty;
            if (seenCommand.TrySetResult((commandId, command, version)))
            {
                await DeviceServerTestHarness.PublishCommandEventAsync(
                    mqttClient,
                    paired.DeviceId,
                    commandId,
                    success: null,
                    progressPercent: 94,
                    stage: "rebooting",
                    message: "reiniciando");
                await DeviceServerTestHarness.PublishCommandEventAsync(
                    mqttClient,
                    paired.DeviceId,
                    commandId,
                    success: null,
                    progressPercent: 97,
                    stage: "pending-verify",
                    message: "validando");
                await DeviceServerTestHarness.PublishCommandEventAsync(
                    mqttClient,
                    paired.DeviceId,
                    commandId,
                    success: true,
                    progressPercent: 100,
                    stage: "validated",
                    message: "ok");
            }
        };

        var result = await host.SendCommandTrackedAsync(
            paired.DeviceId,
            DeviceCommandType.UpdateFirmware,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["version"] = "v2026.03.12-untagged-c2ba150",
            },
            timeout: TimeSpan.FromSeconds(2));
        var observed = await seenCommand.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Accepted);
        Assert.True(result.Success);
        Assert.Equal("validated", result.Stage);
        Assert.Equal("update_firmware", observed.Command);
        Assert.Equal("v2026.03.12-untagged-c2ba150", observed.Version);
    }

    [Fact]
    public async Task SendTrackedCommandAsync_ShouldFailWhenFirmwareRollsBack()
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
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, client, "mqtt-update-firmware-rollback");

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            paired.DeviceId,
            paired.Token);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, paired.DeviceId, "online");
        await DeviceServerTestHarness.SubscribeAsync(mqttClient, DeviceServerTestHarness.GetCommandsTopic(paired.DeviceId));

        var seenCommand = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        mqttClient.ApplicationMessageReceivedAsync += async args =>
        {
            if (!string.Equals(args.ApplicationMessage.Topic, DeviceServerTestHarness.GetCommandsTopic(paired.DeviceId), StringComparison.Ordinal))
            {
                return;
            }

            var payloadBytes3 = new byte[args.ApplicationMessage.Payload.Length];
            args.ApplicationMessage.Payload.CopyTo(payloadBytes3);
            using var payload = JsonDocument.Parse(payloadBytes3);
            var commandId = payload.RootElement.GetProperty("commandId").GetString() ?? string.Empty;
            if (seenCommand.TrySetResult(commandId))
            {
                await DeviceServerTestHarness.PublishCommandEventAsync(
                    mqttClient,
                    paired.DeviceId,
                    commandId,
                    success: null,
                    progressPercent: 94,
                    stage: "rebooting",
                    message: "reiniciando");
                await DeviceServerTestHarness.PublishCommandEventAsync(
                    mqttClient,
                    paired.DeviceId,
                    commandId,
                    success: false,
                    progressPercent: 100,
                    stage: "rolled-back",
                    message: "rollback");
            }
        };

        var result = await host.SendCommandTrackedAsync(
            paired.DeviceId,
            DeviceCommandType.UpdateFirmware,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["version"] = "v2026.03.12-untagged-c2ba150",
            },
            timeout: TimeSpan.FromSeconds(2));
        var commandId = await seenCommand.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(string.IsNullOrWhiteSpace(commandId));
        Assert.True(result.Accepted);
        Assert.True(result.Completed);
        Assert.False(result.Success);
        Assert.Equal("rolled-back", result.Stage);
        Assert.Equal("device_reported_failure", result.ErrorCode);
    }

}
