using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;
using SkiaSharp;

namespace App.Headless.Services;

/// <summary>
/// Captures frames from SimulatorLedOutput and broadcasts them as base64 PNG
/// over WebSocket connections. Also sends heartbeat every 1s.
/// </summary>
internal sealed class PreviewBroadcaster : IUiRealtimePublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SimulatorLedOutput simulatorOutput;
    private readonly IDeviceServerHost deviceServerHost;
    private readonly HeadlessAudioPipeline audioPipeline;
    private readonly ConcurrentDictionary<string, WebSocket> clients = new();
    private readonly Timer frameTimer;
    private readonly Timer heartbeatTimer;
    private int clientIdCounter;

    public PreviewBroadcaster(
        SimulatorLedOutput simulatorOutput,
        IDeviceServerHost deviceServerHost,
        HeadlessAudioPipeline audioPipeline)
    {
        this.simulatorOutput = simulatorOutput;
        this.deviceServerHost = deviceServerHost;
        this.audioPipeline = audioPipeline;

        // ~8 FPS for preview frames
        frameTimer = new Timer(OnFrameTick, null, TimeSpan.FromMilliseconds(125), TimeSpan.FromMilliseconds(125));
        heartbeatTimer = new Timer(OnHeartbeatTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        this.audioPipeline.SpectrumUpdated += OnSpectrumUpdated;
        this.audioPipeline.VisualizerStatusUpdated += OnVisualizerStatusUpdated;
        this.audioPipeline.VisualizerSettingsUpdated += OnVisualizerSettingsUpdated;
    }

    public string AddClient(WebSocket ws)
    {
        var id = Interlocked.Increment(ref clientIdCounter).ToString();
        clients.TryAdd(id, ws);
        PublishInitialStateToClient(ws, id);
        return id;
    }

    public void RemoveClient(string id)
    {
        clients.TryRemove(id, out _);
    }

    public void Publish(object message)
    {
        if (message is null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            BroadcastText(json);
        }
        catch
        {
            // Ignore serialization issues for non-critical UI push events.
        }
    }

    private void OnFrameTick(object? state)
    {
        if (clients.IsEmpty)
        {
            return;
        }

        var frame = simulatorOutput.GetFrameSnapshot();
        if (frame.Length == 0)
        {
            return;
        }

        var pngBase64 = RenderFrameToPngBase64(frame);
        var json = JsonSerializer.Serialize(new { type = "frame", data = pngBase64 }, JsonOptions);
        BroadcastText(json);
    }

    private void OnHeartbeatTick(object? state)
    {
        if (clients.IsEmpty)
        {
            return;
        }

        var devicesOnline = deviceServerHost.GetDevicesSnapshot().Count(d => d.Status == DeviceStatus.Online);
        var visualizerState = audioPipeline.GetStateSnapshot();
        var json = JsonSerializer.Serialize(new
        {
            type = "heartbeat",
            devicesOnline,
            audioCapturing = visualizerState.AudioCapturing,
        }, JsonOptions);
        BroadcastText(json);
    }

    private void OnSpectrumUpdated(object? sender, VisualizerSpectrumSnapshot spectrum)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "visualizer-spectrum",
            bands = spectrum.Bands,
            level = spectrum.Level,
            timestampUtc = spectrum.TimestampUtc,
        }, JsonOptions);
        BroadcastText(json);
    }

    private void OnVisualizerStatusUpdated(object? sender, VisualizerRuntimeStateSnapshot state)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "visualizer-status",
            audioCapturing = state.AudioCapturing,
            targetFps = state.TargetFps,
            status = state.Status,
            contentMode = state.ContentMode,
            hub75Enabled = state.Settings.Hub75Enabled,
            gifPlaying = state.Gif.IsPlaying,
            gifFrameCount = state.Gif.FrameCount,
            updatedAtUtc = state.UpdatedAtUtc,
        }, JsonOptions);
        BroadcastText(json);
    }

    private void OnVisualizerSettingsUpdated(object? sender, VisualizerRuntimeStateSnapshot state)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "visualizer-settings-changed",
            settings = state.Settings,
            updatedAtUtc = state.UpdatedAtUtc,
        }, JsonOptions);
        BroadcastText(json);
    }

    private void BroadcastText(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var (id, ws) in clients)
        {
            if (ws.State != WebSocketState.Open)
            {
                clients.TryRemove(id, out _);
                continue;
            }

            // Fire-and-forget send; drop if client is slow
            _ = SendSafeAsync(ws, segment, id);
        }
    }

    private async Task SendSafeAsync(WebSocket ws, ArraySegment<byte> data, string clientId)
    {
        try
        {
            await ws.SendAsync(data, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            clients.TryRemove(clientId, out _);
        }
    }

    private void PublishInitialStateToClient(WebSocket ws, string clientId)
    {
        try
        {
            var snapshot = audioPipeline.GetStateSnapshot();
            var statusJson = JsonSerializer.Serialize(new
            {
                type = "visualizer-status",
                audioCapturing = snapshot.AudioCapturing,
                targetFps = snapshot.TargetFps,
                status = snapshot.Status,
                contentMode = snapshot.ContentMode,
                hub75Enabled = snapshot.Settings.Hub75Enabled,
                gifPlaying = snapshot.Gif.IsPlaying,
                gifFrameCount = snapshot.Gif.FrameCount,
                updatedAtUtc = snapshot.UpdatedAtUtc,
            }, JsonOptions);

            var settingsJson = JsonSerializer.Serialize(new
            {
                type = "visualizer-settings-changed",
                settings = snapshot.Settings,
                updatedAtUtc = snapshot.UpdatedAtUtc,
            }, JsonOptions);

            var spectrumJson = JsonSerializer.Serialize(new
            {
                type = "visualizer-spectrum",
                bands = snapshot.Spectrum.Bands,
                level = snapshot.Spectrum.Level,
                timestampUtc = snapshot.Spectrum.TimestampUtc,
            }, JsonOptions);

            _ = SendSafeAsync(ws, new ArraySegment<byte>(Encoding.UTF8.GetBytes(statusJson)), clientId);
            _ = SendSafeAsync(ws, new ArraySegment<byte>(Encoding.UTF8.GetBytes(settingsJson)), clientId);
            _ = SendSafeAsync(ws, new ArraySegment<byte>(Encoding.UTF8.GetBytes(spectrumJson)), clientId);
        }
        catch
        {
            // Ignore websocket sync failures for newly connected clients.
        }
    }

    private static string RenderFrameToPngBase64(RgbaColor[] frame)
    {
        const int width = LedDefaults.MatrixWidth;
        const int height = LedDefaults.MatrixHeight;

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                if (i >= frame.Length)
                {
                    continue;
                }

                var c = frame[i];
                bitmap.SetPixel(x, y, new SKColor(c.R, c.G, c.B, c.A));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    public void Dispose()
    {
        audioPipeline.SpectrumUpdated -= OnSpectrumUpdated;
        audioPipeline.VisualizerStatusUpdated -= OnVisualizerStatusUpdated;
        audioPipeline.VisualizerSettingsUpdated -= OnVisualizerSettingsUpdated;
        frameTimer.Dispose();
        heartbeatTimer.Dispose();
    }
}
