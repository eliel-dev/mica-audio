using System.Net.WebSockets;
using System.Text.Json;
using App.Headless.Services;
using Device.Protocol.Models;

namespace App.Headless;

internal static class UiApiEndpoints
{
    private static readonly UiAppCatalogItem[] SupportedApps =
    [
        new("visualizer-hub75", "Visualizador HUB75"),
        new("gif-hub75", "GIF HUB75"),
    ];

    public static void MapUiApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/ui");

        api.MapGet("/health", (DeviceStateService deviceState) =>
            Results.Ok(deviceState.GetHealth()));

        api.MapGet("/visualizer/state", (HeadlessAudioPipeline pipeline) =>
            Results.Ok(MapVisualizerState(pipeline.GetStateSnapshot())));

        api.MapPut("/visualizer/settings", async (
            HttpContext context,
            HeadlessAudioPipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            UiVisualizerSettingsRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<UiVisualizerSettingsRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid_json" });
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "settings_required" });
            }

            var state = await pipeline.UpdateSettingsAsync(new VisualizerSettingsUpdate
            {
                LinearBoost = request.LinearBoost,
                BarCount = request.BarCount,
                FftSize = request.FftSize,
                FftSmoothing = request.FftSmoothing,
                WeightingFilter = request.WeightingFilter,
                FrequencyScale = request.FrequencyScale,
                FrequencyMinHz = request.FrequencyMinHz,
                FrequencyMaxHz = request.FrequencyMaxHz,
                Hub75Enabled = request.Hub75Enabled,
                Brightness = request.Brightness,
            }, cancellationToken).ConfigureAwait(false);

            return Results.Ok(MapVisualizerState(state));
        });

        api.MapPost("/visualizer/hub75", async (
            HttpContext context,
            HeadlessAudioPipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            UiVisualizerHub75Request? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<UiVisualizerHub75Request>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid_json" });
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "enabled_required" });
            }

            var state = await pipeline.SetHub75ModeAsync(request.Enabled, cancellationToken).ConfigureAwait(false);
            return Results.Ok(MapVisualizerState(state));
        });

        api.MapPost("/visualizer/content-mode", async (
            HttpContext context,
            HeadlessAudioPipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            UiVisualizerContentModeRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<UiVisualizerContentModeRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid_json" });
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Mode))
            {
                return Results.BadRequest(new { error = "mode_required" });
            }

            try
            {
                var state = await pipeline.SetContentModeAsync(request.Mode, cancellationToken).ConfigureAwait(false);
                return Results.Ok(MapVisualizerState(state));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "invalid_mode", message = ex.Message });
            }
        });

        api.MapPost("/visualizer/gif/url", async (
            HttpContext context,
            HeadlessAudioPipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            UiVisualizerGifUrlRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<UiVisualizerGifUrlRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid_json" });
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Url))
            {
                return Results.BadRequest(new { error = "url_required" });
            }

            try
            {
                _ = await pipeline.SetContentModeAsync("gif", cancellationToken).ConfigureAwait(false);
                var state = await pipeline.LoadGifFromUrlAsync(request.Url, cancellationToken).ConfigureAwait(false);
                return Results.Ok(MapVisualizerState(state));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "gif_load_failed", message = ex.Message });
            }
        });

        api.MapPost("/visualizer/gif/play", (HeadlessAudioPipeline pipeline) =>
        {
            var state = pipeline.PlayGif();
            return Results.Ok(MapVisualizerState(state));
        });

        api.MapPost("/visualizer/gif/pause", (HeadlessAudioPipeline pipeline) =>
        {
            var state = pipeline.PauseGif();
            return Results.Ok(MapVisualizerState(state));
        });

        api.MapPost("/visualizer/gif/stop", (HeadlessAudioPipeline pipeline) =>
        {
            var state = pipeline.StopGif();
            return Results.Ok(MapVisualizerState(state));
        });

        api.MapGet("/apps", () =>
            Results.Ok(SupportedApps));

        api.MapGet("/devices", (DeviceStateService deviceState) =>
            Results.Ok(deviceState.GetDevices()));

        api.MapGet("/devices/{deviceId}/details", (string deviceId, DeviceStateService deviceState) =>
            Results.Ok(deviceState.GetDeviceDetails(deviceId)));

        api.MapGet("/devices/{deviceId}/logs", (string deviceId, DeviceStateService deviceState) =>
            Results.Ok(deviceState.GetDeviceLogs(deviceId)));

        api.MapPost("/devices/{deviceId}/brightness", async (
            string deviceId,
            HttpContext context,
            DeviceStateService deviceState,
            HeadlessAudioPipeline pipeline,
            CancellationToken cancellationToken) =>
        {
            UiBrightnessRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<UiBrightnessRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid_json" });
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "brightness_required" });
            }

            if (request.Value < DevicesPageFormatting.SafeBrightnessMin || request.Value > DevicesPageFormatting.SafeBrightnessMax)
            {
                return Results.BadRequest(new
                {
                    error = "invalid_brightness",
                    min = DevicesPageFormatting.SafeBrightnessMin,
                    max = DevicesPageFormatting.SafeBrightnessMax,
                });
            }

            var response = await deviceState.RunCommandAsync(
                new UiDeviceCommandRequest
                {
                    DeviceId = deviceId,
                    CommandType = DeviceCommandType.SetBrightness,
                    OperationLabel = "ajustar brilho do painel",
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["brightness"] = request.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                },
                cancellationToken).ConfigureAwait(false);

            if (!response.Accepted)
            {
                return Results.BadRequest(new { error = response.ErrorCode ?? "command_rejected", response.Message });
            }

            var normalizedBrightness = request.Value / 255f;
            pipeline.SetBrightness(normalizedBrightness);

            return Results.Ok(response);
        });

        api.MapPost("/devices/{deviceId}/app", async (
            string deviceId,
            HttpContext context,
            DeviceStateService deviceState,
            CancellationToken cancellationToken) =>
        {
            UiAppChangeRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<UiAppChangeRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid_json" });
            }

            if (request is null || string.IsNullOrWhiteSpace(request.AppId))
            {
                return Results.BadRequest(new { error = "appId_required" });
            }

            if (!SupportedApps.Any(app => string.Equals(app.Id, request.AppId, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(new { error = $"unsupported_app:{request.AppId}" });
            }

            var response = await deviceState.RunCommandAsync(
                new UiDeviceCommandRequest
                {
                    DeviceId = deviceId,
                    CommandType = DeviceCommandType.ActivateApp,
                    OperationLabel = "ativar app",
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["appId"] = request.AppId,
                    },
                },
                cancellationToken).ConfigureAwait(false);

            return response.Accepted
                ? Results.Ok(response)
                : Results.BadRequest(new { error = response.ErrorCode ?? "command_rejected", response.Message });
        });

        api.MapPost("/devices/{deviceId}/test-led", async (
            string deviceId,
            DeviceStateService deviceState,
            CancellationToken cancellationToken) =>
        {
            var response = await deviceState.RunCommandAsync(
                new UiDeviceCommandRequest
                {
                    DeviceId = deviceId,
                    CommandType = DeviceCommandType.TestLed,
                    OperationLabel = "controlar LED auxiliar",
                },
                cancellationToken).ConfigureAwait(false);

            return response.Accepted
                ? Results.Ok(response)
                : Results.BadRequest(new { error = response.ErrorCode ?? "command_rejected", response.Message });
        });

        api.MapDelete("/devices/{deviceId}", async (
            string deviceId,
            DeviceStateService deviceState,
            CancellationToken cancellationToken) =>
        {
            var result = await deviceState.RemoveDeviceAsync(deviceId, cancellationToken).ConfigureAwait(false);
            return result.Removed
                ? Results.Ok(result)
                : Results.BadRequest(result);
        });

        api.MapPost("/pairing-code", (DeviceStateService deviceState) =>
        {
            var info = deviceState.CreatePairingCode(TimeSpan.FromMinutes(10));
            return Results.Ok(new { code = info.Code, expiresAtUtc = info.ExpiresAtUtc });
        });

        api.MapGet("/onboarding/ports", async (IUsbOnboardingService onboardingService, CancellationToken cancellationToken) =>
        {
            var ports = await onboardingService.ListPortsAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(ports);
        });

        api.MapPost("/onboarding/start", async (
            HttpContext context,
            IUsbOnboardingService onboardingService,
            CancellationToken cancellationToken) =>
        {
            UiOnboardingStartRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<UiOnboardingStartRequest>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "invalid_json" });
            }

            if (request is null || string.IsNullOrWhiteSpace(request.PortName))
            {
                return Results.BadRequest(new { error = "port_required" });
            }

            var response = await onboardingService.StartAsync(request.PortName, cancellationToken).ConfigureAwait(false);
            return response.Accepted
                ? Results.Ok(response)
                : Results.BadRequest(response);
        });

        api.MapGet("/onboarding/status", (IUsbOnboardingService onboardingService) =>
            Results.Ok(onboardingService.GetStatus()));

        app.Map("/ws/ui/preview", async (HttpContext context, PreviewBroadcaster broadcaster) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            var clientId = broadcaster.AddClient(ws);

            try
            {
                var buffer = new byte[1024];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            finally
            {
                broadcaster.RemoveClient(clientId);
            }
        });
    }

    private static UiVisualizerStateResponse MapVisualizerState(VisualizerRuntimeStateSnapshot source)
    {
        return new UiVisualizerStateResponse
        {
            AudioCapturing = source.AudioCapturing,
            TargetFps = source.TargetFps,
            Status = source.Status,
            ContentMode = source.ContentMode,
            Settings = source.Settings,
            Gif = source.Gif,
            Spectrum = source.Spectrum,
            UpdatedAtUtc = source.UpdatedAtUtc,
        };
    }
}
