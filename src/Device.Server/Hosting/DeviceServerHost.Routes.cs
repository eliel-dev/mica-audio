using Device.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#fluxo-de-execucao
public sealed partial class DeviceServerHost
{
    private void MapRoutes(WebApplication localApp)
    {
        ArgumentNullException.ThrowIfNull(localApp);

        var api = localApp.MapGroup("/api/v1");
        api.MapGet("/health", HandleHealth);
        api.MapPost("/pair", (Delegate)HandlePairAsync)
            .RequireRateLimiting(PairRatePolicy);

        var server = api.MapGroup("/server");
        server.MapGet("/info", HandleServerInfo);

        var device = api.MapGroup("/device");
        device.MapGet("/config", (Delegate)HandleDeviceConfig);
        device.MapGet("/firmware/latest", (Delegate)HandleDeviceFirmwareLatest);
        device.MapGet("/firmware/download", (Delegate)HandleDeviceFirmwareDownload);
        device.MapGet("/panels/batches/{batchSequence:long}.webp", (Delegate)HandlePanelsBatchDownload);
        device.MapPost("/command-ack", (Delegate)HandleCommandAckAsync)
            .RequireRateLimiting(CommandAckRatePolicy);

        var ws = localApp.MapGroup("/ws/v1");
        ws.Map("/stream", (RequestDelegate)HandleWebSocketAsync)
            .RequireRateLimiting(WebSocketHandshakeRatePolicy);

        localApp.Map("/ws/device/{deviceId}", (RequestDelegate)HandleDashboardWebSocketAsync)
            .RequireRateLimiting(WebSocketHandshakeRatePolicy);
    }

    private IResult HandleHealth()
    {
        return Results.Ok(new
        {
            status = "ok",
            utc = timeProvider.GetUtcNow(),
        });
    }

    private IResult HandleServerInfo(HttpContext ctx)
    {
        var host = ResolveHost(ctx);
        return Results.Ok(new ServerInfoResponse
        {
            HttpBase = $"http://{host}:{runtimeConfig.Port}",
            MqttHost = host,
            MqttPort = runtimeConfig.MqttPort,
            MqttRootTopic = runtimeConfig.MqttRootTopic,
            MdnsService = runtimeConfig.MdnsServiceName,
            MaxDevices = runtimeConfig.MaxDevices,
            WsPath = "/ws/v1/stream",
        });
    }
}
