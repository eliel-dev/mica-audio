using System.Diagnostics;
using App.WinUI.Models.Panels;
using App.WinUI.Services.Devices;
using Device.Client;
using Device.Protocol.Models;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#runtime-em-background
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-prioridade-hub75-visualizador-sobre-paineis
// DOCS: docs/handoffs/2026-04-18-panels-webp-batch-pipeline-optimizations.md
// DOCS: docs/handoffs/2026-04-22-device-server-client-boundary.md
// DOCS: docs/handoffs/2026-04-22-device-client-abstractions.md
internal sealed class PanelsPlaybackService : IDisposable
{
    private static readonly bool EnableBatchPerfLogging =
        AppContext.TryGetSwitch("MicaAudio.Panels.BatchPerfLogging", out var enabled) && enabled;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000d / PanelsFrameComposer.TargetFps);
    private static readonly TimeSpan BatchDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BatchPreloadLead = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan BatchCommandTimeout = TimeSpan.FromSeconds(10);
    private static readonly RgbaColor[] BlackFrame = Enumerable
        .Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        .ToArray();

    private readonly IDeviceServerClient serverClient;
    private readonly IDeviceFrameTransport frameTransport;
    private readonly PanelsFrameComposer composer;
    private readonly PanelsDeviceSessionService deviceSessionService;
    private readonly Hub75VisualizerSessionService hub75VisualizerSessionService;
    private readonly bool enableMatrixTransport;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object stateGate = new();

    private CancellationTokenSource? loopCts;
    private Task? loopTask;
    private PanelsFrameComposer.PanelCompositionSession? compositionSession;
    private PanelDefinition? activePanelSnapshot;
    private string? targetDeviceId;
    private Esp32S3LedOutput? matrixOutput;
    private PanelsBatchTransportState? batchTransportState;
    private SuspendedPanelState? suspendedPanelState;
    private RgbaColor[] latestFrame = BlackFrame;
    private bool disposed;

    public PanelsPlaybackService(
        IDeviceServerClient serverClient,
        IDeviceFrameTransport frameTransport,
        PanelsFrameComposer composer,
        PanelsDeviceSessionService deviceSessionService,
        Hub75VisualizerSessionService hub75VisualizerSessionService,
        bool enableMatrixTransport = true)
    {
        this.serverClient = serverClient;
        this.frameTransport = frameTransport;
        this.composer = composer;
        this.deviceSessionService = deviceSessionService;
        this.hub75VisualizerSessionService = hub75VisualizerSessionService;
        this.enableMatrixTransport = enableMatrixTransport;
    }

    public event EventHandler? StateChanged;

    public event EventHandler<RgbaColor[]>? FrameUpdated;

    public bool IsRunning
    {
        get
        {
            lock (stateGate)
            {
                return loopCts is not null
                    && compositionSession is not null
                    && (!enableMatrixTransport || !string.IsNullOrWhiteSpace(targetDeviceId));
            }
        }
    }

    public bool HasSuspendedPanel
    {
        get
        {
            lock (stateGate)
            {
                return suspendedPanelState is not null;
            }
        }
    }

    public string? TargetDeviceId
    {
        get
        {
            lock (stateGate)
            {
                return targetDeviceId;
            }
        }
    }

    public string? SuspendedTargetDeviceId
    {
        get
        {
            lock (stateGate)
            {
                return suspendedPanelState?.TargetDeviceId;
            }
        }
    }

    public PanelDefinition? GetActivePanelSnapshot()
    {
        lock (stateGate)
        {
            return activePanelSnapshot?.Clone();
        }
    }

    public PanelDefinition? GetSuspendedPanelSnapshot()
    {
        lock (stateGate)
        {
            return suspendedPanelState?.PanelSnapshot.Clone();
        }
    }

    public RgbaColor[] GetLatestFrame()
    {
        lock (stateGate)
        {
            return latestFrame;
        }
    }

    public async Task StartAsync(PanelDefinition panelSnapshot, string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panelSnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ThrowIfDisposed();

        if (enableMatrixTransport && hub75VisualizerSessionService.IsHub75Enabled)
        {
            throw new InvalidOperationException("O visualizador HUB75 tem prioridade enquanto estiver ativo.");
        }

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(restoreDevice: true, clearSuspendedState: true, cancellationToken).ConfigureAwait(false);
            await StartCoreAsync(panelSnapshot, deviceId.Trim(), resumeSuppressedSession: false, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task SuspendAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!enableMatrixTransport)
        {
            return;
        }

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PanelDefinition? retainedPanel;
            string? retainedDeviceId;
            lock (stateGate)
            {
                retainedPanel = activePanelSnapshot?.Clone();
                retainedDeviceId = targetDeviceId;
            }

            if (retainedPanel is null || string.IsNullOrWhiteSpace(retainedDeviceId))
            {
                return;
            }

            await deviceSessionService.SuppressAsync(cancellationToken).ConfigureAwait(false);

            lock (stateGate)
            {
                suspendedPanelState = new SuspendedPanelState(retainedPanel, retainedDeviceId);
            }

            await StopCoreAsync(restoreDevice: false, clearSuspendedState: false, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task ResumeSuspendedAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!enableMatrixTransport)
        {
            return;
        }

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (hub75VisualizerSessionService.IsHub75Enabled)
            {
                return;
            }

            SuspendedPanelState? retainedState;
            lock (stateGate)
            {
                retainedState = suspendedPanelState;
            }

            if (retainedState is null)
            {
                return;
            }

            await StartCoreAsync(
                    retainedState.PanelSnapshot,
                    retainedState.TargetDeviceId,
                    resumeSuppressedSession: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(restoreDevice: true, clearSuspendedState: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
        }

        lifecycleGate.Dispose();
    }

    private async Task StartCoreAsync(
        PanelDefinition panelSnapshot,
        string deviceId,
        bool resumeSuppressedSession,
        CancellationToken cancellationToken)
    {
        var snapshot = panelSnapshot.Clone();
        snapshot.Normalize();
        var normalizedDeviceId = deviceId.Trim();
        var session = await composer.CreateSessionAsync(snapshot, cancellationToken).ConfigureAwait(false);
        Esp32S3LedOutput? output = null;
        PanelsBatchTransportState? batchState = null;
        CancellationTokenSource? cts = null;

        try
        {
            if (enableMatrixTransport && resumeSuppressedSession)
            {
                await deviceSessionService.ResumeAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (enableMatrixTransport)
            {
                await deviceSessionService.StartAsync(normalizedDeviceId, cancellationToken).ConfigureAwait(false);
            }

            if (enableMatrixTransport)
            {
                output = new Esp32S3LedOutput(frameTransport);
                output.Start(new LedOutputConfig
                {
                    Width = LedDefaults.MatrixWidth,
                    Height = LedDefaults.MatrixHeight,
                    Brightness = LedDefaults.Brightness,
                    TargetDeviceId = normalizedDeviceId,
                });
                output.SetBrightness(LedDefaults.Brightness);
            }

            if (enableMatrixTransport && SupportsAnimatedWebpBatch(normalizedDeviceId))
            {
                batchState = await TryPrimeBatchTransportAsync(session, normalizedDeviceId, cancellationToken).ConfigureAwait(false);
            }

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (stateGate)
            {
                compositionSession = session;
                activePanelSnapshot = snapshot;
                targetDeviceId = normalizedDeviceId;
                matrixOutput = output;
                batchTransportState = batchState;
                loopCts = cts;
                latestFrame = BlackFrame;
                if (resumeSuppressedSession)
                {
                    suspendedPanelState = null;
                }
            }

            await SendFrameAsync(session, output, DateTimeOffset.UtcNow, sendToDevice: batchState is null).ConfigureAwait(false);

            var localLoopTask = RunLoopAsync(session, output, normalizedDeviceId, batchState, cts.Token);
            lock (stateGate)
            {
                loopTask = localLoopTask;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            cts?.Cancel();
            cts?.Dispose();
            output?.Stop();
            session.Dispose();
            throw;
        }
    }

    private async Task RunLoopAsync(
        PanelsFrameComposer.PanelCompositionSession session,
        Esp32S3LedOutput? output,
        string deviceId,
        PanelsBatchTransportState? batchState,
        CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var nextTickIndex = 1L;

            while (!cancellationToken.IsCancellationRequested)
            {
                var dueAt = TimeSpan.FromTicks(nextTickIndex * TickInterval.Ticks);
                var remaining = dueAt - stopwatch.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                }

                var utcNow = DateTimeOffset.UtcNow;
                await SendFrameAsync(session, output, utcNow, sendToDevice: batchState is null).ConfigureAwait(false);

                if (batchState is not null
                    && utcNow + BatchPreloadLead >= batchState.NextBatchStartUtc)
                {
                    if (!await QueueNextBatchAsync(session, deviceId, batchState, cancellationToken).ConfigureAwait(false))
                    {
                        serverClient.ClearPanelsBatches(deviceId, batchState.PanelsSessionId);
                        batchState = null;
                        lock (stateGate)
                        {
                            batchTransportState = null;
                        }

                        await SendFrameAsync(session, output, utcNow, sendToDevice: true).ConfigureAwait(false);
                    }
                }

                var elapsedTicks = stopwatch.Elapsed.Ticks;
                nextTickIndex = Math.Max(nextTickIndex + 1, (elapsedTicks / TickInterval.Ticks) + 1);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task StopCoreAsync(bool restoreDevice, bool clearSuspendedState, CancellationToken cancellationToken)
    {
        CancellationTokenSource? localLoopCts;
        Task? localLoopTask;
        PanelsFrameComposer.PanelCompositionSession? localSession;
        Esp32S3LedOutput? localOutput;
        string? localTargetDeviceId;
        PanelsBatchTransportState? localBatchState;

        lock (stateGate)
        {
            localLoopCts = loopCts;
            localLoopTask = loopTask;
            localSession = compositionSession;
            localOutput = matrixOutput;
            localTargetDeviceId = targetDeviceId;
            localBatchState = batchTransportState;

            loopCts = null;
            loopTask = null;
            compositionSession = null;
            matrixOutput = null;
            activePanelSnapshot = null;
            targetDeviceId = null;
            batchTransportState = null;
            latestFrame = BlackFrame;
            if (clearSuspendedState)
            {
                suspendedPanelState = null;
            }
        }

        if (localLoopCts is not null)
        {
            localLoopCts.Cancel();
            localLoopCts.Dispose();
        }

        if (localLoopTask is not null)
        {
            try
            {
                await localLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (localOutput is not null && !string.IsNullOrWhiteSpace(localTargetDeviceId))
        {
            if (localBatchState is not null)
            {
                serverClient.ClearPanelsBatches(localTargetDeviceId, localBatchState.PanelsSessionId);
            }

            localOutput.Send(LedPayloadFactory.CreateFramePayload(BlackFrame, PanelsDeviceSessionService.PanelsAppId));
            localOutput.Stop();
        }

        localSession?.Dispose();
        RaiseFrameUpdated(BlackFrame);

        if (enableMatrixTransport && restoreDevice)
        {
            await deviceSessionService.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task SendFrameAsync(
        PanelsFrameComposer.PanelCompositionSession session,
        Esp32S3LedOutput? output,
        DateTimeOffset utcNow,
        bool sendToDevice)
    {
        var frame = session.RenderFrame(utcNow);
        lock (stateGate)
        {
            latestFrame = frame;
        }

        if (sendToDevice)
        {
            output?.Send(LedPayloadFactory.CreateFramePayload(frame, PanelsDeviceSessionService.PanelsAppId));
        }

        RaiseFrameUpdated(frame);
        return Task.CompletedTask;
    }

    private bool SupportsAnimatedWebpBatch(string deviceId)
    {
        return serverClient
            .GetDevices()
            .Any(snapshot =>
                string.Equals(snapshot.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                && snapshot.AnimatedWebpBatchSupported == true);
    }

    private async Task<PanelsBatchTransportState?> TryPrimeBatchTransportAsync(
        PanelsFrameComposer.PanelCompositionSession session,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var state = new PanelsBatchTransportState(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);

        for (var i = 0; i < 2; i++)
        {
            if (!await QueueNextBatchAsync(session, deviceId, state, cancellationToken).ConfigureAwait(false))
            {
                serverClient.ClearPanelsBatches(deviceId, state.PanelsSessionId);
                return null;
            }
        }

        return state;
    }

    private async Task<bool> QueueNextBatchAsync(
        PanelsFrameComposer.PanelCompositionSession session,
        string deviceId,
        PanelsBatchTransportState state,
        CancellationToken cancellationToken)
    {
        var encodedBatch = RenderEncodedBatch(session, state.NextBatchStartUtc);
        var registration = serverClient.RegisterPanelsBatch(
            deviceId,
            state.PanelsSessionId,
            state.NextBatchSequence,
            encodedBatch.Payload,
            encodedBatch.FrameCount,
            encodedBatch.DurationMs);

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

        var result = await serverClient
            .SendCommandTrackedAsync(
                deviceId,
                DeviceCommandType.QueuePanelsBatch,
                payload.ToParameters(),
                timeout: BatchCommandTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted || !result.Success)
        {
            return false;
        }

        state.Advance();
        return true;
    }

    private static PanelsEncodedBatch RenderEncodedBatch(
        PanelsFrameComposer.PanelCompositionSession session,
        DateTimeOffset batchStartUtc)
    {
        long renderTicks = 0;
        var totalStartTimestamp = Stopwatch.GetTimestamp();
        var encodedBatch = PanelsAnimatedWebpEncoder.Encode(
            PanelsFrameComposer.TargetFps,
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight,
            (frameIndex, targetFrame) =>
            {
                var renderStartTimestamp = Stopwatch.GetTimestamp();
                session.RenderFrameInto(
                    batchStartUtc + TimeSpan.FromMilliseconds(GetBatchFrameOffsetMs(frameIndex)),
                    targetFrame);
                renderTicks += Stopwatch.GetTimestamp() - renderStartTimestamp;
            },
            GetBatchFrameDurationMs);

        if (EnableBatchPerfLogging)
        {
            var totalTicks = Stopwatch.GetTimestamp() - totalStartTimestamp;
            LogBatchPerf(renderTicks, Math.Max(0L, totalTicks - renderTicks), encodedBatch);
        }

        return encodedBatch;
    }

    private static int GetBatchFrameOffsetMs(int frameIndex)
    {
        return (frameIndex * (int)BatchDuration.TotalMilliseconds) / PanelsFrameComposer.TargetFps;
    }

    private static int GetBatchFrameDurationMs(int frameIndex)
    {
        var frameOffsetMs = GetBatchFrameOffsetMs(frameIndex);
        var nextFrameOffsetMs = GetBatchFrameOffsetMs(frameIndex + 1);
        return Math.Max(1, nextFrameOffsetMs - frameOffsetMs);
    }

    private static void LogBatchPerf(long renderTicks, long encodeTicks, PanelsEncodedBatch encodedBatch)
    {
        Debug.WriteLine(
            $"[panels-perf] render_batch_ms={TicksToMilliseconds(renderTicks):F2} encode_batch_ms={TicksToMilliseconds(encodeTicks):F2} frames={encodedBatch.FrameCount} payload_bytes={encodedBatch.Payload.Length}");
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks <= 0 ? 0d : (ticks * 1000d) / Stopwatch.Frequency;
    }

    private void RaiseFrameUpdated(RgbaColor[] frame)
    {
        FrameUpdated?.Invoke(this, frame);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record SuspendedPanelState(PanelDefinition PanelSnapshot, string TargetDeviceId);

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
}
