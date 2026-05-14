using System.Diagnostics;
using Device.Protocol.Stream;
using Device.Server.Hosting;
using MicaAudio.Core.Presets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Panels.Composition.Models;
using Panels.Composition.ServerSide;

namespace MicaAudio.Server;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/handoffs/2026-05-12-display-state-gif-panels.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// Background worker that keeps server-capable panels rendering on the LED
// matrix even after the WinUI client has disconnected. For every device that
// has a stored panel (uploaded via PUT /api/v1/admin/devices/{id}/panel) AND
// whose widgets are all server-capable today (Clock), this service:
//   1. Builds a ServerSidePanelCompositor instance,
//   2. Renders one frame at TargetFps (30) cadence,
//   3. Encodes the frame as RGB565 + StreamFrameV2,
//   4. Hands the bytes to DeviceServerHost.SendFrame.
//
// Devices with widgets that need the WinUI client (audio visualizer, PC
// metrics) are skipped — their frames continue to come from the desktop and
// the device dashboard surfaces a "panel inativo no servidor" capability.
//
// Compositors are cached and only rebuilt when the underlying PanelDefinition
// changes (compared by the UpdatedAtUtc timestamp + widget count + panelId).
internal sealed partial class PanelCompositorHostedService : BackgroundService
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1000.0 / ServerSidePanelCompositor.TargetFps);

    // Window within which a recent WinUI client compositor frame causes the
    // server-side compositor to skip rendering for that device.  200 ms ≈ 6
    // frames at 30 fps — enough to absorb a late client tick without letting a
    // single dropped frame restart server rendering prematurely.
    private static readonly TimeSpan ClientCompositorBackoffWindow = TimeSpan.FromMilliseconds(200);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Server-side compositor failed for device {DeviceId}.")]
    private partial void LogDeviceFailure(Exception exception, string deviceId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Server-side compositor armed for device {DeviceId} (panelId={PanelId} widgets={WidgetCount}).")]
    private partial void LogCompositorArmed(string deviceId, string panelId, int widgetCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Server-side compositor: device {DeviceId} has no open frame connection — frames being dropped (state for {Seconds}s).")]
    private partial void LogNoFrameConnection(string deviceId, double seconds);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Server-side compositor: streaming resumed to {DeviceId} (after {Seconds}s gap).")]
    private partial void LogFrameConnectionResumed(string deviceId, double seconds);

    private readonly DeviceServerHost host;
    private readonly IServerPanelStore panelStore;
    private readonly IServerPanelCatalogStore? panelCatalogStore;
    private readonly IServerMediaStore? mediaStore;
    private readonly ILogger<PanelCompositorHostedService> logger;
    private readonly Dictionary<string, CachedCompositor> compositorsByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeviceFrameDeliveryState> frameDeliveryStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly RgbaColor[] frameScratchRgba = new RgbaColor[StreamFrameV2.PixelCount128x64];
    private readonly ushort[] frameScratchRgb565 = new ushort[StreamFrameV2.PixelCount128x64];
    private uint sequence;

    public PanelCompositorHostedService(
        DeviceServerHost host,
        IServerPanelStore panelStore,
        ILogger<PanelCompositorHostedService> logger,
        IServerMediaStore? mediaStore = null,
        IServerPanelCatalogStore? panelCatalogStore = null)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.panelStore = panelStore ?? throw new ArgumentNullException(nameof(panelStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.mediaStore = mediaStore;
        this.panelCatalogStore = panelCatalogStore;

        // Make sure DeviceServerHost can find the stores when serving the
        // PUT/GET/DELETE panel + media endpoints.
        host.AttachPanelStore(panelStore);
        if (mediaStore is not null)
        {
            host.AttachMediaStore(mediaStore);
        }

        if (panelCatalogStore is not null)
        {
            host.AttachPanelCatalogStore(panelCatalogStore);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FrameInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                TickAllDevices();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            DisposeAllCompositors();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        DisposeAllCompositors();
    }

    private void TickAllDevices()
    {
        var deviceIds = panelStore.EnumerateDeviceIds();
        if (deviceIds.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var deviceId in deviceIds)
        {
            try
            {
                RenderOneDevice(deviceId, now);
            }
            catch (Exception ex)
            {
                LogDeviceFailure(ex, deviceId);
            }
        }
    }

    private void RenderOneDevice(string deviceId, DateTimeOffset now)
    {
        var panel = panelStore.TryGet(deviceId);
        if (panel is null)
        {
            DisposeCompositor(deviceId);
            return;
        }

        var capability = PanelServerCapabilityClassifier.Classify(panel);
        if (capability == PanelServerCapability.RequiresClient)
        {
            DisposeCompositor(deviceId);
            return;
        }

        if (capability == PanelServerCapability.Empty)
        {
            // Empty panel: blank frame keeps the last desired state on the matrix.
            // Falling back to the same code path keeps rendering predictable.
        }

        // Yield to the WinUI client compositor when it is actively sending
        // frames for this device. Both sources write to the same WebSocket so
        // interleaving produces the "two panels at once" ghosting effect.
        // The 200 ms backoff covers ~6 frames at 30 fps; the server resumes
        // automatically as soon as the client stops sending (e.g. app closed).
        if (host.WasClientCompositorActiveRecently(deviceId, ClientCompositorBackoffWindow))
        {
            return;
        }

        var cached = GetOrBuildCompositor(deviceId, panel);
        if (cached is null)
        {
            return;
        }

        // Track delivery health: log a single warning when the device's stream
        // WebSocket goes away (so frames sent silently land in /dev/null) and
        // another when it comes back. Without this the only symptom from
        // outside is "the panel just stops" with no hint why.
        var hasConnection = host.HasOpenFrameConnection(deviceId);
        UpdateFrameDeliveryHealth(deviceId, now, hasConnection);

        cached.Compositor.RenderFrameInto(now, frameScratchRgba);
        EncodeRgb565(frameScratchRgba, frameScratchRgb565);

        var payload = StreamFrameV2.CreateFrame128x64Rgb565(
            sequence: ++sequence,
            timestampQpc: Stopwatch.GetTimestamp(),
            pixels128x64Rgb565: frameScratchRgb565,
            brightness0To255: 255);

        host.SendFrame(deviceId, payload);
    }

    private void UpdateFrameDeliveryHealth(string deviceId, DateTimeOffset now, bool hasConnection)
    {
        if (!frameDeliveryStates.TryGetValue(deviceId, out var state))
        {
            // First tick for this device: bootstrap state but only emit a log
            // if we are starting in the bad state (so user sees it immediately).
            state = new DeviceFrameDeliveryState
            {
                IsConnected = hasConnection,
                LastTransitionUtc = now,
                LastWarningUtc = now - TimeSpan.FromMinutes(1),
            };
            frameDeliveryStates[deviceId] = state;

            if (!hasConnection)
            {
                LogNoFrameConnection(deviceId, 0);
                state.LastWarningUtc = now;
            }
            return;
        }

        if (state.IsConnected != hasConnection)
        {
            // State change: emit one log line per transition.
            var elapsed = (now - state.LastTransitionUtc).TotalSeconds;
            if (hasConnection)
            {
                LogFrameConnectionResumed(deviceId, elapsed);
            }
            else
            {
                LogNoFrameConnection(deviceId, 0);
                state.LastWarningUtc = now;
            }

            state.IsConnected = hasConnection;
            state.LastTransitionUtc = now;
            return;
        }

        // No change. If still disconnected, repeat the warning every 30s
        // so a long-running outage stays visible in the logs.
        if (!hasConnection && (now - state.LastWarningUtc) > TimeSpan.FromSeconds(30))
        {
            LogNoFrameConnection(deviceId, (now - state.LastTransitionUtc).TotalSeconds);
            state.LastWarningUtc = now;
        }
    }

    private sealed class DeviceFrameDeliveryState
    {
        public bool IsConnected { get; set; }
        public DateTimeOffset LastTransitionUtc { get; set; }
        public DateTimeOffset LastWarningUtc { get; set; }
    }

    private CachedCompositor? GetOrBuildCompositor(string deviceId, PanelDefinition panel)
    {
        var fingerprint = ComputeFingerprint(panel);
        if (compositorsByDeviceId.TryGetValue(deviceId, out var cached) && cached.Fingerprint == fingerprint)
        {
            return cached;
        }

        cached?.Compositor.Dispose();
        compositorsByDeviceId.Remove(deviceId);

        // Pass the device-specific media directory so gifhub75 runtimes can
        // locate their uploaded files.  Null-safe: clock-only panels ignore it.
        var mediaDirectory = mediaStore?.GetMediaDirectory(deviceId);
        var compositor = ServerSidePanelCompositor.TryCreate(panel, mediaDirectory);
        if (compositor is null)
        {
            return null;
        }

        var entry = new CachedCompositor(compositor, fingerprint);
        compositorsByDeviceId[deviceId] = entry;
        LogCompositorArmed(deviceId, panel.PanelId, panel.Widgets.Count);
        return entry;
    }

    private void DisposeCompositor(string deviceId)
    {
        if (compositorsByDeviceId.TryGetValue(deviceId, out var cached))
        {
            cached.Compositor.Dispose();
            compositorsByDeviceId.Remove(deviceId);
        }
    }

    private void DisposeAllCompositors()
    {
        foreach (var pair in compositorsByDeviceId)
        {
            pair.Value.Compositor.Dispose();
        }

        compositorsByDeviceId.Clear();
    }

    private static string ComputeFingerprint(PanelDefinition panel)
    {
        // Cheap fingerprint: panelId + widget count + last update timestamp.
        // Two distinct edits in the same millisecond would collide; widget
        // count guards against trivial add/remove in those windows.
        return string.Concat(
            panel.PanelId,
            "|",
            panel.UpdatedAtUtc.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            "|",
            panel.Widgets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void EncodeRgb565(ReadOnlySpan<RgbaColor> source, Span<ushort> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var color = source[i];
            var r = (ushort)((color.R >> 3) & 0x1F);
            var g = (ushort)((color.G >> 2) & 0x3F);
            var b = (ushort)((color.B >> 3) & 0x1F);
            destination[i] = (ushort)((r << 11) | (g << 5) | b);
        }
    }

    private sealed record CachedCompositor(ServerSidePanelCompositor Compositor, string Fingerprint);
}
