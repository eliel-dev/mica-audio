using Device.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#fluxo-de-execucao
// DOCS: docs/wiki/reference/device-observability-dashboard.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
// DOCS: docs/handoffs/2026-04-22-micaudio-server-docker-advertised-endpoints.md
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
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

        var admin = api.MapGroup("/admin");
        admin.MapGet("/devices", (Delegate)HandleAdminDevices);
        admin.MapGet("/devices/{deviceId}/telemetry", (Delegate)HandleAdminDeviceTelemetry);
        admin.MapPost("/pairing-codes", (Delegate)HandleAdminCreatePairingCodeAsync);
        admin.MapDelete("/devices/{deviceId}", (Delegate)HandleAdminRemoveDevice);
        admin.MapPost("/devices/{deviceId}/commands/tracked", (Delegate)HandleAdminTrackedCommandAsync);
        admin.MapGet("/library/panels", (Delegate)HandleAdminGetPanelLibraryAsync);
        admin.MapPut("/library/panels", (Delegate)HandleAdminPutPanelLibraryAsync);
        admin.MapGet("/panels/runtime", (Delegate)HandleAdminGetPanelRuntimeAsync);
        admin.MapPut("/panels/runtime", (Delegate)HandleAdminPutPanelRuntimeAsync);
        admin.MapGet("/panels/runtime/status", (Delegate)HandleAdminGetPanelRuntimeStatusAsync);
        admin.MapPost("/library/media", (Delegate)HandleAdminUploadMediaAsync);
        admin.MapGet("/library/media/{mediaId}", (Delegate)HandleAdminGetMediaAsync);
        admin.MapDelete("/library/media/{mediaId}", (Delegate)HandleAdminDeleteMediaAsync);
        admin.MapPost("/panels/batches/{deviceId}/{panelsSessionId}/{batchSequence:long}", (Delegate)HandleAdminPanelsBatchAsync);
        admin.MapDelete("/panels/batches/{deviceId}", (Delegate)HandleAdminClearPanelsBatches);

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
        ws.Map("/admin/events", (RequestDelegate)HandleAdminEventsWebSocketAsync)
            .RequireRateLimiting(WebSocketHandshakeRatePolicy);
        ws.Map("/admin/frames", (RequestDelegate)HandleAdminFramesWebSocketAsync)
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
        return Results.Ok(new ServerInfoResponse
        {
            HttpBase = ResolveAdvertisedHttpBaseAddress(ctx),
            MqttHost = ResolveAdvertisedMqttHost(ctx),
            MqttPort = runtimeConfig.MqttPort,
            MqttRootTopic = runtimeConfig.MqttRootTopic,
            MdnsService = runtimeConfig.MdnsServiceName,
            MaxDevices = runtimeConfig.MaxDevices,
            WsPath = "/ws/v1/stream",
        });
    }
}
