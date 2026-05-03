using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/reference/device-observability-dashboard.md
// DOCS: docs/wiki/modules/device-server-protocol.md
// DOCS: docs/handoffs/2026-04-20-remove-usb-flash-flow.md
public sealed partial class DeviceServerHost
{
    private readonly object dashboardGate = new();
    private readonly Dictionary<string, DashboardFpsSample> dashboardFpsSamples = new(StringComparer.OrdinalIgnoreCase);

    private static async Task HandleDashboardAsync(HttpContext ctx)
    {
        var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "dashboard", "index.html");
        if (!File.Exists(indexPath))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new { error = "dashboard_not_found" }).ConfigureAwait(false);
            return;
        }

        var redirectTarget = string.IsNullOrWhiteSpace(ctx.Request.QueryString.Value)
            ? "/dashboard/index.html"
            : $"/dashboard/index.html{ctx.Request.QueryString.Value}";
        ctx.Response.Redirect(redirectTarget, permanent: false);
    }

    private async Task HandleDashboardWebSocketAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "websocket_required" }).ConfigureAwait(false);
            return;
        }

        var routeDeviceId = ctx.Request.RouteValues["deviceId"]?.ToString();
        var deviceId = string.IsNullOrWhiteSpace(routeDeviceId) ? null : routeDeviceId.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "device_id_required" }).ConfigureAwait(false);
            return;
        }

        if (!TryBuildDashboardEnvelope(deviceId, out var initialPayload, out var lastSignature))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new { error = "device_not_found" }).ConfigureAwait(false);
            return;
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        if (!await TrySendDashboardPayloadAsync(ws, initialPayload, ctx.RequestAborted).ConfigureAwait(false))
        {
            return;
        }

        while (!ctx.RequestAborted.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try
            {
                await WaitForDashboardUpdateAsync(ctx.RequestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (ctx.RequestAborted.IsCancellationRequested || ws.State != WebSocketState.Open)
            {
                break;
            }

            if (!TryBuildDashboardEnvelope(deviceId, out var nextPayload, out var nextSignature))
            {
                await TryCloseDashboardSocketAsync(ws, WebSocketCloseStatus.NormalClosure, "device_not_found").ConfigureAwait(false);
                break;
            }

            if (string.Equals(lastSignature, nextSignature, StringComparison.Ordinal))
            {
                continue;
            }

            if (!await TrySendDashboardPayloadAsync(ws, nextPayload, ctx.RequestAborted).ConfigureAwait(false))
            {
                break;
            }

            lastSignature = nextSignature;
        }
    }

    private Task WaitForDashboardUpdateAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        CancellationTokenRegistration registration = default;

        handler = (_, _) =>
        {
            DevicesChanged -= handler;
            registration.Dispose();
            tcs.TrySetResult();
        };

        DevicesChanged += handler;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(() =>
            {
                DevicesChanged -= handler;
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        return tcs.Task;
    }

    private bool TryBuildDashboardEnvelope(string deviceId, out byte[] payload, out string signature)
    {
        payload = Array.Empty<byte>();
        signature = string.Empty;

        if (!TryBuildDeviceDashboardDto(deviceId, out var dto))
        {
            return false;
        }

        payload = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        signature = Encoding.UTF8.GetString(payload);
        return true;
    }

    private bool TryBuildDeviceDashboardDto(string deviceId, out DeviceTelemetryResponse dto)
    {
        dto = default!;
        var snapshot = GetDevicesSnapshot().FirstOrDefault(snapshot =>
            string.Equals(snapshot.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        if (snapshot is null)
        {
            RemoveDashboardFpsSample(deviceId);
            return false;
        }

        dto = ProjectDeviceDashboardDto(snapshot);
        return true;
    }

    private DeviceTelemetryResponse ProjectDeviceDashboardDto(DeviceSnapshot snapshot)
    {
        var psramAvailable = snapshot.PsramAvailable;
        if (!psramAvailable.HasValue && snapshot.PsramTotalBytes is > 0)
        {
            psramAvailable = true;
        }

        var firmwareUpdateState = ResolveFirmwareUpdateState(snapshot);

        return new DeviceTelemetryResponse
        {
            DeviceId = snapshot.DeviceId,
            Name = snapshot.Name,
            Online = snapshot.Status == DeviceStatus.Online,
            ActiveAppName = snapshot.ActiveAppName,
            WifiState = snapshot.WifiState,
            SignalDbm = snapshot.LastKnownRssi,
            UptimeSeconds = snapshot.UptimeSeconds,
            TelemetrySequence = snapshot.TelemetrySequence,
            TestLedAvailable = snapshot.TestLedAvailable,
            TestLedEnabled = snapshot.TestLedEnabled,
            TestLedDuty = snapshot.TestLedDuty,
            FirmwareVersion = snapshot.FirmwareVersion,
            BrightnessCap = snapshot.BrightnessCap,
            BrightnessRequested = snapshot.BrightnessRequested,
            BrightnessApplied = snapshot.BrightnessApplied,
            LoopHealthyPercent = snapshot.LoopHealthyPercent,
            LoopLoadPercent = snapshot.LoopLoadPercent,
            ChipTemperatureCelsius = snapshot.ChipTemperatureCelsius,
            HeapFreeBytes = snapshot.FreeHeapBytes,
            HeapLargestBlockBytes = snapshot.LargestHeapBlockBytes,
            HeapTotalBytes = snapshot.HeapTotalBytes,
            HeapFreePercent = ComputePercent(snapshot.FreeHeapBytes, snapshot.HeapTotalBytes),
            HeapFragmentationPercent = ComputeFragmentationPercent(snapshot.FreeHeapBytes, snapshot.LargestHeapBlockBytes),
            PsramAvailable = psramAvailable,
            PsramFreeBytes = snapshot.FreePsramBytes,
            PsramLargestBlockBytes = snapshot.LargestPsramBlockBytes,
            PsramTotalBytes = snapshot.PsramTotalBytes,
            PsramFreePercent = ComputePercent(snapshot.FreePsramBytes, snapshot.PsramTotalBytes),
            StreamFramesReceived = snapshot.StreamFramesReceived,
            StreamFramesApplied = snapshot.StreamFramesApplied,
            StreamSequenceGapCount = snapshot.StreamSequenceGapCount,
            StreamInvalidFrameCount = snapshot.StreamInvalidFrameCount,
            StreamLastSequence = snapshot.StreamLastSequence,
            Hub75Fps = ComputeHub75Fps(snapshot.DeviceId, snapshot.Hub75PresentFrames),
            LatestFirmwareVersion = firmwareUpdateState.LatestFirmwareVersion,
            FirmwareUpdateSupported = firmwareUpdateState.Supported,
            FirmwareUpdateAvailable = firmwareUpdateState.Available,
        };
    }

    private FirmwareUpdateState ResolveFirmwareUpdateState(DeviceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!TryResolveOfficialFirmware(snapshot, out var package, out _)
            || !string.Equals(package.ControlPlane, "mqtt", StringComparison.OrdinalIgnoreCase))
        {
            return new FirmwareUpdateState(null, Supported: false, Available: false);
        }

        return new FirmwareUpdateState(
            package.FirmwareVersion,
            Supported: true,
            Available: snapshot.Status == DeviceStatus.Online
                && !string.Equals(snapshot.FirmwareVersion, package.FirmwareVersion, StringComparison.OrdinalIgnoreCase));
    }

    private static int? ComputePercent(long? freeBytes, long? totalBytes)
    {
        if (!freeBytes.HasValue || !totalBytes.HasValue || freeBytes.Value < 0 || totalBytes.Value <= 0)
        {
            return null;
        }

        return Math.Clamp((int)Math.Round((freeBytes.Value / (double)totalBytes.Value) * 100d), 0, 100);
    }

    private static int? ComputeFragmentationPercent(long? freeBytes, long? largestBlockBytes)
    {
        if (!freeBytes.HasValue || !largestBlockBytes.HasValue || freeBytes.Value <= 0 || largestBlockBytes.Value < 0)
        {
            return null;
        }

        return Math.Clamp((int)Math.Round((1d - (largestBlockBytes.Value / (double)freeBytes.Value)) * 100d), 0, 100);
    }

    private int? ComputeHub75Fps(string deviceId, uint? presentFrames)
    {
        if (!presentFrames.HasValue)
        {
            RemoveDashboardFpsSample(deviceId);
            return null;
        }

        lock (dashboardGate)
        {
            var now = timeProvider.GetUtcNow();
            if (!dashboardFpsSamples.TryGetValue(deviceId, out var sample))
            {
                dashboardFpsSamples[deviceId] = new DashboardFpsSample(presentFrames, now, null);
                return null;
            }

            if (!sample.LastPresentFrames.HasValue || presentFrames.Value < sample.LastPresentFrames.Value)
            {
                dashboardFpsSamples[deviceId] = sample with
                {
                    LastPresentFrames = presentFrames,
                    LastSampleUtc = now,
                    CurrentFps = null,
                };
                return null;
            }

            var elapsedSeconds = (now - sample.LastSampleUtc).TotalSeconds;
            if (elapsedSeconds < 0.25d)
            {
                return sample.CurrentFps;
            }

            var deltaFrames = presentFrames.Value - sample.LastPresentFrames.Value;
            var currentFps = Math.Max(0, (int)Math.Round(deltaFrames / elapsedSeconds));
            dashboardFpsSamples[deviceId] = sample with
            {
                LastPresentFrames = presentFrames,
                LastSampleUtc = now,
                CurrentFps = currentFps,
            };

            return currentFps;
        }
    }

    private void RemoveDashboardFpsSample(string deviceId)
    {
        lock (dashboardGate)
        {
            dashboardFpsSamples.Remove(deviceId);
        }
    }

    private static async Task<bool> TrySendDashboardPayloadAsync(WebSocket ws, byte[] payload, CancellationToken cancellationToken)
    {
        if (ws.State != WebSocketState.Open)
        {
            return false;
        }

        try
        {
            await ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task TryCloseDashboardSocketAsync(WebSocket ws, WebSocketCloseStatus closeStatus, string description)
    {
        if (ws.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            await ws.CloseAsync(closeStatus, description, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // ignore close races
        }
    }

    private sealed record DashboardFpsSample(uint? LastPresentFrames, DateTimeOffset LastSampleUtc, int? CurrentFps);

    private sealed record FirmwareUpdateState(string? LatestFirmwareVersion, bool Supported, bool Available);


}
