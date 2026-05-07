using System.Diagnostics;
using Device.Protocol.Models;
using Device.Protocol.Stream;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using MicaAudio.Panels;
using Microsoft.Extensions.Logging;

namespace MicaAudio.Server;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/device-server-protocol.md#runtime-remoto-de-paineis
// DOCS: docs/handoffs/2026-05-07-panels-batch-firmware-crash-and-remote-contract.md
// DOCS: docs/handoffs/2026-05-06-panels-heartbeat-nonblocking-batch-runtime.md
// DOCS: docs/handoffs/2026-05-06-fix-pipeline-batches-server-await-firmware-fila.md
// DOCS: docs/handoffs/2026-04-30-remote-only-server-panel-runtime.md
public sealed partial class ServerPanelRuntimeService : IAsyncDisposable
{
    public const string ServerPanelsClientId = "server-panels";
    private const string PanelsAppId = "panels-hub75";
    private const string PanelsAppName = "Paineis HUB75";
    private const string PanelsMode = "panels";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000d / PanelFrameComposer.TargetFps);
    private static readonly TimeSpan BatchDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BatchPreloadLead = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
    private static readonly RgbaColor[] BlackFrame = Enumerable
        .Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        .ToArray();

    private readonly IDeviceServerHost serverHost;
    private readonly IPanelLibraryStore panelLibraryStore;
    private readonly IPanelRuntimeStateStore runtimeStateStore;
    private readonly IPanelRuntimeStatusStore runtimeStatusStore;
    private readonly PanelFrameComposer composer;
    private readonly ILogger<ServerPanelRuntimeService> logger;
    private readonly object gate = new();
    private readonly Dictionary<string, uint> ownerEpochByDeviceId = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? loopCts;
    private Task? loopTask;
    private bool disposed;

    public ServerPanelRuntimeService(
        IDeviceServerHost serverHost,
        IPanelLibraryStore panelLibraryStore,
        IPanelRuntimeStateStore runtimeStateStore,
        IPanelRuntimeStatusStore runtimeStatusStore,
        PanelFrameComposer composer,
        ILogger<ServerPanelRuntimeService> logger)
    {
        this.serverHost = serverHost;
        this.panelLibraryStore = panelLibraryStore;
        this.runtimeStateStore = runtimeStateStore;
        this.runtimeStatusStore = runtimeStatusStore;
        this.composer = composer;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (loopTask is not null)
            {
                return Task.CompletedTask;
            }

            loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            loopTask = Task.Run(() => RunLoopAsync(loopCts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (gate)
        {
            cts = loopCts;
            task = loopTask;
            loopCts = null;
            loopTask = null;
        }

        if (cts is null)
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        PanelRuntimeStateDocument? activeState = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var state = await runtimeStateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
                if (!IsRunnable(state))
                {
                    activeState = null;
                    await SaveStatusAsync(new PanelRuntimeStatusDocument(), cancellationToken).ConfigureAwait(false);
                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!SameRuntimeState(activeState, state))
                {
                    LogRuntimeActivated(logger, state.PanelId!, state.TargetDeviceId!);
                    activeState = state;
                }

                await RunActiveStateAsync(state, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogRuntimeLoopFailed(logger, ex);
                await SaveStatusAsync(new PanelRuntimeStatusDocument
                {
                    Running = false,
                    PanelId = activeState?.PanelId,
                    TargetDeviceId = activeState?.TargetDeviceId,
                    LastError = ex.Message,
                }, CancellationToken.None).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunActiveStateAsync(PanelRuntimeStateDocument state, CancellationToken cancellationToken)
    {
        var library = await panelLibraryStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var panel = library.Panels.FirstOrDefault(candidate =>
            string.Equals(candidate.PanelId, state.PanelId, StringComparison.OrdinalIgnoreCase)
            && candidate.IsEnabled);
        if (panel is null)
        {
            await SaveStatusAsync(new PanelRuntimeStatusDocument
            {
                Running = false,
                PanelId = state.PanelId,
                TargetDeviceId = state.TargetDeviceId,
                LastError = "panel_not_found",
            }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            return;
        }

        var device = ResolveTargetDevice(state.TargetDeviceId);
        if (device is null)
        {
            await SaveStatusAsync(new PanelRuntimeStatusDocument
            {
                Running = false,
                PanelId = state.PanelId,
                TargetDeviceId = state.TargetDeviceId,
                LastError = "target_device_offline",
            }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            return;
        }

        using var session = await composer.CreateSessionAsync(panel, PanelRenderIntent.Animated, cancellationToken).ConfigureAwait(false);
        var skippedWidgets = session.GetSkippedWidgets();
        var status = new PanelRuntimeStatusDocument
        {
            Running = true,
            PanelId = panel.PanelId,
            TargetDeviceId = device.DeviceId,
            SkippedWidgets = skippedWidgets,
        };

        await SaveStatusAsync(status, cancellationToken).ConfigureAwait(false);
        await ActivatePanelsAppAsync(device, cancellationToken).ConfigureAwait(false);

        if (device.AnimatedWebpBatchSupported == true)
        {
            await RunBatchTransportAsync(state, session, panel.Widgets.Count, skippedWidgets, device.DeviceId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await RunFrameTransportAsync(state, session, panel.Widgets.Count, skippedWidgets, device.DeviceId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunBatchTransportAsync(
        PanelRuntimeStateDocument state,
        PanelFrameComposer.PanelCompositionSession session,
        int widgetCount,
        IReadOnlyList<string> skippedWidgets,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var batchState = new PanelsBatchTransportState(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        await QueueNextBatchAsync(session, widgetCount, skippedWidgets, deviceId, batchState, cancellationToken).ConfigureAwait(false);
        await QueueNextBatchAsync(session, widgetCount, skippedWidgets, deviceId, batchState, cancellationToken).ConfigureAwait(false);

        var lastStateCheckUtc = DateTimeOffset.MinValue;
        while (!cancellationToken.IsCancellationRequested)
        {
            var utcNow = DateTimeOffset.UtcNow;
            if (utcNow - lastStateCheckUtc >= TimeSpan.FromSeconds(1))
            {
                var latestState = await runtimeStateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
                if (!SameRuntimeState(state, latestState))
                {
                    await serverHost.ClearPanelsBatchesAsyncCompat(deviceId, batchState.PanelsSessionId).ConfigureAwait(false);
                    return;
                }

                lastStateCheckUtc = utcNow;
            }

            if (utcNow + BatchPreloadLead >= batchState.NextBatchStartUtc)
            {
                await QueueNextBatchAsync(session, widgetCount, skippedWidgets, deviceId, batchState, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunFrameTransportAsync(
        PanelRuntimeStateDocument state,
        PanelFrameComposer.PanelCompositionSession session,
        int widgetCount,
        IReadOnlyList<string> skippedWidgets,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextTickIndex = 1L;
        var sequence = 0u;
        var rgb565 = new ushort[StreamFrameV2.PixelCount128x64];
        var lastStateCheckUtc = DateTimeOffset.MinValue;
        var lastHeartbeatUtc = DateTimeOffset.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            var dueAt = TimeSpan.FromTicks(nextTickIndex * TickInterval.Ticks);
            var remaining = dueAt - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }

            var utcNow = DateTimeOffset.UtcNow;
            if (utcNow - lastHeartbeatUtc >= HeartbeatInterval)
            {
                DispatchHeartbeat(deviceId, cancellationToken);
                lastHeartbeatUtc = utcNow;
            }

            if (utcNow - lastStateCheckUtc >= TimeSpan.FromSeconds(1))
            {
                var latestState = await runtimeStateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
                if (!SameRuntimeState(state, latestState))
                {
                    return;
                }

                lastStateCheckUtc = utcNow;
            }

            var frame = RenderFrame(session, widgetCount, skippedWidgets, utcNow);
            EncodeToRgb565(frame, rgb565);
            var context = CreateCommandContext(ResolveTargetDevice(deviceId));
            var payload = StreamFrameV3.CreateFrame128x64Rgb565(
                sequence: ++sequence,
                ownerEpoch: context.OwnerEpoch,
                timestampQpc: Stopwatch.GetTimestamp(),
                pixels128x64Rgb565: rgb565,
                brightness0To255: 255);
            serverHost.SendFrame(deviceId, payload);

            await SaveStatusAsync(new PanelRuntimeStatusDocument
            {
                Running = true,
                PanelId = state.PanelId,
                TargetDeviceId = deviceId,
                SkippedWidgets = skippedWidgets,
                LastRenderedAtUtc = utcNow,
            }, cancellationToken).ConfigureAwait(false);

            var elapsedTicks = stopwatch.Elapsed.Ticks;
            nextTickIndex = Math.Max(nextTickIndex + 1, (elapsedTicks / TickInterval.Ticks) + 1);
        }
    }

    private async Task QueueNextBatchAsync(
        PanelFrameComposer.PanelCompositionSession session,
        int widgetCount,
        IReadOnlyList<string> skippedWidgets,
        string deviceId,
        PanelsBatchTransportState state,
        CancellationToken cancellationToken)
    {
        var encodeStopwatch = Stopwatch.StartNew();
        var encodedBatch = RenderEncodedBatch(session, widgetCount, skippedWidgets, state.NextBatchStartUtc);
        var encodeMs = encodeStopwatch.ElapsedMilliseconds;

        var registration = serverHost.RegisterPanelsBatch(
            deviceId,
            state.PanelsSessionId,
            state.NextBatchSequence,
            encodedBatch.Payload,
            encodedBatch.FrameCount,
            encodedBatch.DurationMs);

        LogBatchEncoded(logger, encodedBatch.Payload.Length, encodedBatch.FrameCount, encodeMs);

        var payload = new PanelsBatchCommandPayload
        {
            PanelsSessionId = registration.PanelsSessionId,
            BatchSequence = registration.BatchSequence,
            DownloadUrl = registration.DownloadUrl,
            Sha256 = registration.Sha256,
            FileSizeBytes = registration.FileSizeBytes,
            ContentType = registration.ContentType,
            FrameCount = registration.FrameCount,
            DurationMs = registration.DurationMs,
        };

        await DispatchBatchCommandAsync(deviceId, payload, cancellationToken).ConfigureAwait(false);

        await SaveStatusAsync(new PanelRuntimeStatusDocument
        {
            Running = true,
            PanelId = session.Panel.PanelId,
            TargetDeviceId = deviceId,
            SkippedWidgets = skippedWidgets,
            LastRenderedAtUtc = state.NextBatchStartUtc,
        }, cancellationToken).ConfigureAwait(false);

        state.Advance();
    }

    private async Task DispatchBatchCommandAsync(
        string deviceId,
        PanelsBatchCommandPayload payload,
        CancellationToken cancellationToken)
    {
        var queueStopwatch = Stopwatch.StartNew();
        try
        {
            var result = await serverHost.SendCommandTrackedAsync(
                    deviceId,
                    DeviceCommandType.QueuePanelsBatch,
                    payload.ToParameters(),
                    CommandTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            var queueMs = queueStopwatch.ElapsedMilliseconds;
            LogBatchQueued(logger, queueMs);

            if (!result.Accepted || !result.Success)
            {
                LogBatchCommandFailed(logger, result.ErrorCode ?? result.Message ?? "queue_panels_batch_failed", queueMs);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var queueMs = queueStopwatch.ElapsedMilliseconds;
            LogBatchCommandFailed(logger, ex.Message, queueMs);
        }
    }

    private async Task ActivatePanelsAppAsync(DeviceSnapshot device, CancellationToken cancellationToken)
    {
        try
        {
            var result = await serverHost.SendCommandTrackedAsync(
                    device.DeviceId,
                    DeviceCommandType.ActivateApp,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["appId"] = PanelsAppId,
                        ["displayName"] = PanelsAppName,
                    },
                    CommandTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.Accepted || !result.Success)
            {
                throw new InvalidOperationException(result.ErrorCode ?? result.Message ?? "activate_panels_failed");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogActivateAppFailed(logger, ex.Message);
            throw;
        }

    }

    private void DispatchHeartbeat(string deviceId, CancellationToken cancellationToken)
        => _ = DispatchHeartbeatAsync(deviceId, cancellationToken);

    private async Task DispatchHeartbeatAsync(string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await serverHost.SendCommandTrackedAsync(
                    deviceId,
                    DeviceCommandType.SessionHeartbeat,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mode"] = PanelsMode,
                    },
                    CreateCommandContext(ResolveTargetDevice(deviceId)),
                    TimeSpan.FromSeconds(2),
                    cancellationToken)
                .ConfigureAwait(false);
            LogHeartbeatResult(logger, deviceId, result.Accepted, result.Success, result.ErrorCode, result.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogHeartbeatDispatchFailed(logger, ex.Message);
        }
    }

    private static PanelsEncodedBatch RenderEncodedBatch(
        PanelFrameComposer.PanelCompositionSession session,
        int widgetCount,
        IReadOnlyList<string> skippedWidgets,
        DateTimeOffset batchStartUtc)
    {
        return PanelsAnimatedWebpEncoder.Encode(
            PanelFrameComposer.TargetFps,
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight,
            (frameIndex, targetFrame) =>
            {
                var frame = RenderFrame(
                    session,
                    widgetCount,
                    skippedWidgets,
                    batchStartUtc + TimeSpan.FromMilliseconds(GetBatchFrameOffsetMs(frameIndex)));
                frame.CopyTo(targetFrame, 0);
            },
            GetBatchFrameDurationMs);
    }

    private static RgbaColor[] RenderFrame(
        PanelFrameComposer.PanelCompositionSession session,
        int widgetCount,
        IReadOnlyList<string> skippedWidgets,
        DateTimeOffset utcNow)
    {
        if (widgetCount > 0 && skippedWidgets.Count >= widgetCount)
        {
            return RenderFallbackFrame("SERVER", "SEM WIDGET");
        }

        var frame = session.RenderFrame(utcNow);
        return frame.SequenceEqual(BlackFrame)
            ? RenderFallbackFrame("SERVER", "PAINEL")
            : frame;
    }

    private static RgbaColor[] RenderFallbackFrame(string top, string bottom)
    {
        var frame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        PanelsMatrixDrawHelpers.Clear(frame);
        PanelsMatrixDrawHelpers.DrawText5x7(frame, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 4, 20, top, new RgbaColor(95, 210, 255, 255));
        PanelsMatrixDrawHelpers.DrawText5x7(frame, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 4, 32, bottom, new RgbaColor(255, 225, 100, 255));
        return frame;
    }

    private DeviceSnapshot? ResolveTargetDevice(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return serverHost.GetDevicesSnapshot().FirstOrDefault(device =>
            device.Status == DeviceStatus.Online
            && string.Equals(device.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private DeviceCommandSessionContext CreateCommandContext(DeviceSnapshot? device)
    {
        var deviceId = device?.DeviceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new DeviceCommandSessionContext(ServerPanelsClientId, 1, null);
        }

        lock (gate)
        {
            if (device?.SessionActiveOwnerEpoch is { } shadowEpoch)
            {
                if (shadowEpoch == 0)
                {
                    ownerEpochByDeviceId.Remove(deviceId);
                }
                else if (ownerEpochByDeviceId.TryGetValue(deviceId, out var cachedEpoch) && shadowEpoch < cachedEpoch)
                {
                    ownerEpochByDeviceId.Remove(deviceId);
                }
            }

            if (device?.SessionActiveOwnerEpoch is { } observedEpoch and > 0
                && string.Equals(device.SessionActiveClientId, ServerPanelsClientId, StringComparison.OrdinalIgnoreCase))
            {
                ownerEpochByDeviceId[deviceId] = observedEpoch;
                return new DeviceCommandSessionContext(ServerPanelsClientId, observedEpoch, null);
            }

            if (ownerEpochByDeviceId.TryGetValue(deviceId, out var currentEpoch) && currentEpoch > 0)
            {
                return new DeviceCommandSessionContext(ServerPanelsClientId, currentEpoch, null);
            }

            var predictedEpoch = Math.Max(1, device?.SessionActiveOwnerEpoch.GetValueOrDefault() + 1 ?? 1);
            ownerEpochByDeviceId[deviceId] = predictedEpoch;
            return new DeviceCommandSessionContext(ServerPanelsClientId, predictedEpoch, null);
        }
    }

    private async Task SaveStatusAsync(PanelRuntimeStatusDocument document, CancellationToken cancellationToken)
        => await runtimeStatusStore.SaveAsync(document, cancellationToken).ConfigureAwait(false);

    private static bool IsRunnable(PanelRuntimeStateDocument state)
        => state.Enabled
           && !string.IsNullOrWhiteSpace(state.PanelId)
           && !string.IsNullOrWhiteSpace(state.TargetDeviceId);

    private static bool SameRuntimeState(PanelRuntimeStateDocument? left, PanelRuntimeStateDocument? right)
        => left is not null
           && right is not null
           && left.Enabled == right.Enabled
           && string.Equals(left.PanelId, right.PanelId, StringComparison.OrdinalIgnoreCase)
           && string.Equals(left.TargetDeviceId, right.TargetDeviceId, StringComparison.OrdinalIgnoreCase)
           && left.UpdatedAtUtc == right.UpdatedAtUtc;

    private static int GetBatchFrameOffsetMs(int frameIndex)
        => (frameIndex * (int)BatchDuration.TotalMilliseconds) / PanelFrameComposer.TargetFps;

    private static int GetBatchFrameDurationMs(int frameIndex)
    {
        var frameOffsetMs = GetBatchFrameOffsetMs(frameIndex);
        var nextFrameOffsetMs = GetBatchFrameOffsetMs(frameIndex + 1);
        return Math.Max(1, nextFrameOffsetMs - frameOffsetMs);
    }

    private static void EncodeToRgb565(ReadOnlySpan<RgbaColor> frame, Span<ushort> destination)
    {
        if (destination.Length < frame.Length)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));
        }

        for (var i = 0; i < frame.Length; i++)
        {
            var color = frame[i];
            var r = (ushort)((color.R >> 3) & 0x1F);
            var g = (ushort)((color.G >> 2) & 0x3F);
            var b = (ushort)((color.B >> 3) & 0x1F);
            destination[i] = (ushort)((r << 11) | (g << 5) | b);
        }
    }

    private sealed class PanelsBatchTransportState
    {
        public PanelsBatchTransportState(string panelsSessionId, DateTimeOffset nextBatchStartUtc)
        {
            PanelsSessionId = panelsSessionId;
            NextBatchStartUtc = nextBatchStartUtc;
        }

        public string PanelsSessionId { get; }

        public ulong NextBatchSequence { get; private set; } = 1;

        public DateTimeOffset NextBatchStartUtc { get; private set; }

        public void Advance()
        {
            NextBatchSequence++;
            NextBatchStartUtc += BatchDuration;
        }
    }

    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Server panel runtime activated. panelId={PanelId} deviceId={DeviceId}")]
    private static partial void LogRuntimeActivated(ILogger logger, string panelId, string deviceId);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Server panel runtime loop failed.")]
    private static partial void LogRuntimeLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "Batch encoded in {EncodeMs}ms, payload={PayloadBytes} bytes, frames={FrameCount}")]
    private static partial void LogBatchEncoded(ILogger logger, int payloadBytes, int frameCount, long encodeMs);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Information, Message = "Batch queued in {QueueMs}ms")]
    private static partial void LogBatchQueued(ILogger logger, long queueMs);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Warning, Message = "Batch command failed: {ErrorCode} ({QueueMs}ms)")]
    private static partial void LogBatchCommandFailed(ILogger logger, string errorCode, long queueMs);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Warning, Message = "ActivateApp failed: {ErrorCode}")]
    private static partial void LogActivateAppFailed(ILogger logger, string errorCode);

    [LoggerMessage(EventId = 2106, Level = LogLevel.Debug, Message = "Session heartbeat dispatch failed: {ErrorCode}")]
    private static partial void LogHeartbeatDispatchFailed(ILogger logger, string errorCode);

    [LoggerMessage(EventId = 2107, Level = LogLevel.Debug, Message = "Heartbeat result for {DeviceId}: Accepted={Accepted} Success={Success} ErrorCode={ErrorCode} Message={Message}")]
    private static partial void LogHeartbeatResult(ILogger logger, string deviceId, bool accepted, bool success, string? errorCode, string? message);

}

internal static class DeviceServerHostPanelsCompatibility
{
    public static Task ClearPanelsBatchesAsyncCompat(this IDeviceServerHost host, string deviceId, string? panelsSessionId)
    {
        host.ClearPanelsBatches(deviceId, panelsSessionId);
        return Task.CompletedTask;
    }
}
