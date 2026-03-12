using System.Buffers;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;
using Device.Server.Hosting;
using MQTTnet;
using MQTTnet.Protocol;

namespace Output.Tests;

internal static class DeviceServerTestHarness
{
    public const string DefaultMqttRootTopic = "mica/v1/devices";

    public static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
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

    public static async Task<PairDeviceResponse> PairDeviceAsync(
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

    public static async Task<bool> WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
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

    public static DeviceStatus? GetDeviceStatus(DeviceServerHost host, string deviceId)
    {
        return host.GetDevicesSnapshot().FirstOrDefault(d =>
            string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))?.Status;
    }

    public static DeviceControlPlaneState? GetControlPlaneState(DeviceServerHost host, string deviceId)
    {
        return host.GetDevicesSnapshot().FirstOrDefault(d =>
            string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))?.ControlPlaneState;
    }

    public static async Task<IMqttClient> ConnectMqttClientAsync(
        string host,
        int port,
        string deviceId,
        string token,
        string? willPayload = null,
        string? willTopic = null)
    {
        var client = new MqttClientFactory().CreateMqttClient();
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(deviceId)
            .WithCredentials(deviceId, token)
            .WithCleanSession(true);

        _ = willPayload;
        _ = willTopic;

        var result = await client.ConnectAsync(optionsBuilder.Build(), CancellationToken.None);
        Assert.Equal(MqttClientConnectResultCode.Success, result.ResultCode);
        return client;
    }

    public static async Task PublishPresenceAsync(IMqttClient client, string deviceId, string state)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new DevicePresenceMessage
        {
            DeviceId = deviceId,
            State = state,
        });

        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(GetPresenceTopic(deviceId))
            .WithPayload(payload)
            .WithRetainFlag(true)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), CancellationToken.None);
    }

    public static async Task PublishTelemetryAsync(IMqttClient client, string deviceId, object payload)
    {
        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(GetStatusTopic(deviceId))
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload))
            .WithRetainFlag(true)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), CancellationToken.None);
    }

    public static async Task PublishStatsAsync(IMqttClient client, string deviceId, object payload)
    {
        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(GetStatsTopic(deviceId))
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload))
            .WithRetainFlag(true)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), CancellationToken.None);
    }

    public static async Task PublishDeviceLogAsync(IMqttClient client, string deviceId, object payload)
    {
        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(GetLogsTopic(deviceId))
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(payload))
            .WithRetainFlag(false)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), CancellationToken.None);
    }

    public static async Task PublishCommandEventAsync(
        IMqttClient client,
        string deviceId,
        string commandId,
        bool? success = true,
        int progressPercent = 100,
        string stage = "done",
        string message = "ok")
    {
        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(GetCommandEventsTopic(deviceId))
            .WithPayload(JsonSerializer.SerializeToUtf8Bytes(new DeviceCommandProgressMessage
            {
                DeviceId = deviceId,
                CommandId = commandId,
                ProgressPercent = progressPercent,
                Stage = stage,
                Message = message,
                Success = success,
            }))
            .WithRetainFlag(false)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), CancellationToken.None);
    }

    public static async Task SubscribeAsync(IMqttClient client, string topic)
    {
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build())
            .Build(), CancellationToken.None);
    }

    public static string GetCommandsTopic(string deviceId) => $"{DefaultMqttRootTopic}/{deviceId}/commands";

    public static string GetCommandEventsTopic(string deviceId) => $"{DefaultMqttRootTopic}/{deviceId}/command-events";

    public static string GetStatusTopic(string deviceId) => $"{DefaultMqttRootTopic}/{deviceId}/status";

    public static string GetPresenceTopic(string deviceId) => $"{DefaultMqttRootTopic}/{deviceId}/presence";

    public static string GetStatsTopic(string deviceId) => $"{DefaultMqttRootTopic}/{deviceId}/stats";

    public static string GetLogsTopic(string deviceId) => $"{DefaultMqttRootTopic}/{deviceId}/logs";

    public static string DecodePayload(MqttApplicationMessageReceivedEventArgs args)
    {
        return Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());
    }
}
