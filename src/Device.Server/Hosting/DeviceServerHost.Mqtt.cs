using System.Buffers;
using System.Collections;
using System.Net;
using System.Text;
using Device.Protocol.Models;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#contrato-mqtt-de-controle
public sealed partial class DeviceServerHost
{
    private const string MqttSessionDeviceIdKey = "deviceId";
    private const string MqttSessionRemoteIpKey = "remoteIp";

    private MqttServer CreateMqttServer(DeviceServerRuntimeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var optionsBuilder = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(config.MqttPort);

        if (TryParseListenAddress(config.ListenHost, out var listenAddress))
        {
            optionsBuilder = optionsBuilder.WithDefaultEndpointBoundIPAddress(listenAddress);
        }

        var server = new MqttServerFactory().CreateMqttServer(optionsBuilder.Build());
        server.ValidatingConnectionAsync += args => HandleMqttValidatingConnectionAsync(config, args);
        server.ClientConnectedAsync += HandleMqttClientConnectedAsync;
        server.ClientDisconnectedAsync += HandleMqttClientDisconnectedAsync;
        server.InterceptingPublishAsync += args => HandleMqttInterceptingPublishAsync(config, args);
        return server;
    }

    private static async Task StopMqttServerAsync(MqttServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        try
        {
            server.AcceptNewConnections = false;
            await server.StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore broker shutdown races
        }
    }

    private Task HandleMqttValidatingConnectionAsync(DeviceServerRuntimeConfig config, ValidatingConnectionEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);

        var deviceId = NormalizeOptional(args.ClientId);
        var userName = NormalizeOptional(args.UserName);
        if (string.IsNullOrWhiteSpace(deviceId) || !string.Equals(deviceId, userName, StringComparison.OrdinalIgnoreCase))
        {
            args.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
            args.ReasonString = "device_id_mismatch";
            return Task.CompletedTask;
        }

        var remoteIp = TryResolveRemoteIp(args.RemoteEndPoint);
        if (!IsRequestAllowed(config, remoteIp))
        {
            args.ReasonCode = MqttConnectReasonCode.NotAuthorized;
            args.ReasonString = "network_not_allowed";
            return Task.CompletedTask;
        }

        DeviceSession? state;
        lock (gate)
        {
            devices.TryGetValue(deviceId, out state);
        }

        if (state is null || !TokensMatchConstantTime(state.Record.Token, NormalizeMqttPassword(args.Password) ?? string.Empty))
        {
            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            args.ReasonString = "invalid_device_credentials";
            return Task.CompletedTask;
        }

        var sessionItems = args.SessionItems;
        sessionItems[MqttSessionDeviceIdKey] = state.Record.DeviceId;
        if (remoteIp is not null)
        {
            sessionItems[MqttSessionRemoteIpKey] = remoteIp.ToString();
        }

        args.ReasonCode = MqttConnectReasonCode.Success;
        return Task.CompletedTask;
    }

    private Task HandleMqttClientConnectedAsync(ClientConnectedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!TryResolveDeviceSession(args.ClientId, out var state))
        {
            return Task.CompletedTask;
        }

        var remoteIp = TryGetSessionString(args.SessionItems, MqttSessionRemoteIpKey)
            ?? TryResolveRemoteIp(args.RemoteEndPoint)?.ToString();

        lock (gate)
        {
            state.MarkAuthenticated();
            state.MarkControlPlaneConnected(remoteIp);
        }

        NotifyDevicesChanged();
        Log($"Control plane MQTT conectado: {state.Record.DeviceId}");
        return Task.CompletedTask;
    }

    private Task HandleMqttClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!TryResolveDeviceSession(args.ClientId, out var state))
        {
            return Task.CompletedTask;
        }

        lock (gate)
        {
            state.MarkControlPlaneDisconnected();
        }

        NotifyDevicesChanged();
        Log($"Control plane MQTT desconectado: {state.Record.DeviceId}");
        return Task.CompletedTask;
    }

    private Task HandleMqttInterceptingPublishAsync(DeviceServerRuntimeConfig config, InterceptingPublishEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);

        if (args.ApplicationMessage is null
            || !DeviceMqttTopics.TryParse(config.MqttRootTopic, args.ApplicationMessage.Topic, out var descriptor))
        {
            args.ProcessPublish = false;
            args.Response.ReasonCode = MqttPubAckReasonCode.TopicNameInvalid;
            args.Response.ReasonString = "topic_invalid";
            return Task.CompletedTask;
        }

        if (IsServerInjectedPublish(args))
        {
            return Task.CompletedTask;
        }

        if (descriptor.Channel == DeviceMqttChannel.Commands)
        {
            args.ProcessPublish = false;
            args.Response.ReasonCode = MqttPubAckReasonCode.NotAuthorized;
            args.Response.ReasonString = "commands_read_only";
            return Task.CompletedTask;
        }

        if (!string.Equals(args.ClientId, descriptor.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            args.ProcessPublish = false;
            args.Response.ReasonCode = MqttPubAckReasonCode.NotAuthorized;
            args.Response.ReasonString = "device_topic_mismatch";
            return Task.CompletedTask;
        }

        if (!TryResolveDeviceSession(descriptor.DeviceId, out var state))
        {
            args.ProcessPublish = false;
            args.Response.ReasonCode = MqttPubAckReasonCode.NotAuthorized;
            args.Response.ReasonString = "device_not_registered";
            return Task.CompletedTask;
        }

        var remoteIp = TryGetSessionString(args.SessionItems, MqttSessionRemoteIpKey);
        var payload = args.ApplicationMessage.Payload.ToArray();
        var handled = descriptor.Channel switch
        {
            DeviceMqttChannel.CommandEvents => TryHandleCommandEventPayload(state, payload),
            DeviceMqttChannel.Status => TryHandleTelemetryPayload(state, payload, remoteIp),
            DeviceMqttChannel.Presence => TryHandlePresencePayload(state, payload, remoteIp),
            DeviceMqttChannel.Stats => TryHandleStatsPayload(state, payload, remoteIp),
            DeviceMqttChannel.Logs => TryHandleLogPayload(state, payload),
            _ => false,
        };

        if (!handled)
        {
            args.ProcessPublish = false;
            args.Response.ReasonCode = MqttPubAckReasonCode.UnspecifiedError;
            args.Response.ReasonString = "payload_invalid";
            return Task.CompletedTask;
        }

        NotifyDevicesChanged();
        return Task.CompletedTask;
    }

    private void ScheduleRetainedDeviceStateCleanup(MqttServer server, string deviceId)
    {
        _ = ClearRetainedDeviceStateAsync(server, deviceId);
    }

    private async Task PublishCommandOverMqttAsync(string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(payload);

        MqttServer? localServer;
        DeviceServerRuntimeConfig localConfig;
        lock (gate)
        {
            localServer = mqttServer;
            localConfig = runtimeConfig;
        }

        if (localServer is null)
        {
            throw new InvalidOperationException("Broker MQTT indisponivel.");
        }

        await PublishApplicationMessageAsync(
                localServer,
                DeviceMqttTopics.Commands(localConfig.MqttRootTopic, deviceId),
                payload,
                retain: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ClearRetainedDeviceStateAsync(MqttServer server, string deviceId)
    {
        try
        {
            await PublishApplicationMessageAsync(
                    server,
                    DeviceMqttTopics.Status(runtimeConfig.MqttRootTopic, deviceId),
                    Array.Empty<byte>(),
                    retain: true,
                    CancellationToken.None)
                .ConfigureAwait(false);

            await PublishApplicationMessageAsync(
                    server,
                    DeviceMqttTopics.Presence(runtimeConfig.MqttRootTopic, deviceId),
                    Array.Empty<byte>(),
                    retain: true,
                    CancellationToken.None)
                .ConfigureAwait(false);

            await PublishApplicationMessageAsync(
                    server,
                    DeviceMqttTopics.Stats(runtimeConfig.MqttRootTopic, deviceId),
                    Array.Empty<byte>(),
                    retain: true,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Falha ao limpar estado MQTT retido de {deviceId}: {ex.Message}");
        }
    }

    private static async Task PublishApplicationMessageAsync(
        MqttServer server,
        string topic,
        byte[] payload,
        bool retain,
        CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();

        var injected = new InjectedMqttApplicationMessage(message)
        {
            CustomSessionItems = server.ServerSessionItems,
        };

        await server.InjectApplicationMessage(injected, cancellationToken).ConfigureAwait(false);
    }

    private bool TryResolveDeviceSession(string? deviceId, out DeviceSession state)
    {
        lock (gate)
        {
            if (!devices.TryGetValue(deviceId ?? string.Empty, out var foundState) || foundState is null)
            {
                state = null!;
                return false;
            }

            state = foundState;
            return true;
        }
    }

    private static bool TryParseListenAddress(string? listenHost, out IPAddress address)
    {
        address = IPAddress.Any;
        if (string.IsNullOrWhiteSpace(listenHost) || string.Equals(listenHost, "0.0.0.0", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(listenHost, "::", StringComparison.OrdinalIgnoreCase))
        {
            address = IPAddress.IPv6Any;
            return true;
        }

        if (IPAddress.TryParse(listenHost, out var parsedAddress) && parsedAddress is not null)
        {
            address = parsedAddress;
            return true;
        }

        address = IPAddress.Any;
        return false;
    }

    private static IPAddress? TryResolveRemoteIp(object? remoteEndPoint)
    {
        return remoteEndPoint switch
        {
            IPEndPoint ipEndPoint => ipEndPoint.Address,
            EndPoint endPoint when endPoint.ToString() is { Length: > 0 } text && IPAddress.TryParse(text.Split(':')[0], out var parsedIp) => parsedIp,
            _ => null,
        };
    }

    private static bool IsServerInjectedPublish(InterceptingPublishEventArgs args)
    {
        return string.IsNullOrWhiteSpace(args.ClientId);
    }

    private static string? NormalizeMqttPassword(object? rawPassword)
    {
        return rawPassword switch
        {
            null => null,
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            ArraySegment<byte> segment => Encoding.UTF8.GetString(segment),
            ReadOnlySequence<byte> sequence => Encoding.UTF8.GetString(sequence.ToArray()),
            _ => rawPassword.ToString(),
        };
    }

    private static string? TryGetSessionString(IDictionary? sessionItems, string key)
    {
        if (sessionItems is null)
        {
            return null;
        }

        foreach (DictionaryEntry item in sessionItems)
        {
            if (string.Equals(item.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                return item.Value?.ToString();
            }
        }

        return null;
    }
}
