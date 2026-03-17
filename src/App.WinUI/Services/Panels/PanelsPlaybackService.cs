using App.WinUI.Models.Panels;
using App.WinUI.Services.Devices;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#runtime-em-background
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-prioridade-hub75-visualizador-sobre-paineis
internal sealed class PanelsPlaybackService : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000d / PanelsFrameComposer.TargetFps);
    private static readonly RgbaColor[] BlackFrame = Enumerable
        .Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        .ToArray();

    private readonly DeviceServerHost host;
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
    private SuspendedPanelState? suspendedPanelState;
    private RgbaColor[] latestFrame = BlackFrame;
    private bool disposed;

    public PanelsPlaybackService(
        DeviceServerHost host,
        PanelsFrameComposer composer,
        PanelsDeviceSessionService deviceSessionService,
        Hub75VisualizerSessionService hub75VisualizerSessionService,
        bool enableMatrixTransport = true)
    {
        this.host = host;
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
                output = new Esp32S3LedOutput(host);
                output.Start(new LedOutputConfig
                {
                    Width = LedDefaults.MatrixWidth,
                    Height = LedDefaults.MatrixHeight,
                    Brightness = LedDefaults.Brightness,
                    TargetDeviceId = normalizedDeviceId,
                });
                output.SetBrightness(LedDefaults.Brightness);
            }

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (stateGate)
            {
                compositionSession = session;
                activePanelSnapshot = snapshot;
                targetDeviceId = normalizedDeviceId;
                matrixOutput = output;
                loopCts = cts;
                latestFrame = BlackFrame;
                if (resumeSuppressedSession)
                {
                    suspendedPanelState = null;
                }
            }

            await SendFrameAsync(session, output, DateTimeOffset.UtcNow).ConfigureAwait(false);

            var localLoopTask = RunLoopAsync(session, output, cts.Token);
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
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TickInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await SendFrameAsync(session, output, DateTimeOffset.UtcNow).ConfigureAwait(false);
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

        lock (stateGate)
        {
            localLoopCts = loopCts;
            localLoopTask = loopTask;
            localSession = compositionSession;
            localOutput = matrixOutput;
            localTargetDeviceId = targetDeviceId;

            loopCts = null;
            loopTask = null;
            compositionSession = null;
            matrixOutput = null;
            activePanelSnapshot = null;
            targetDeviceId = null;
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
        DateTimeOffset utcNow)
    {
        var frame = session.RenderFrame(utcNow);
        lock (stateGate)
        {
            latestFrame = frame;
        }

        output?.Send(LedPayloadFactory.CreateFramePayload(frame, PanelsDeviceSessionService.PanelsAppId));
        RaiseFrameUpdated(frame);
        return Task.CompletedTask;
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
}
