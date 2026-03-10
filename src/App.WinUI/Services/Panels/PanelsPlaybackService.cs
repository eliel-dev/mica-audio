using App.WinUI.Models.Panels;
using App.WinUI.Services.Devices;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#runtime-em-background
internal sealed class PanelsPlaybackService : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000d / PanelsFrameComposer.TargetFps);
    private static readonly RgbaColor[] BlackFrame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();

    private readonly DeviceServerHost host;
    private readonly PanelsFrameComposer composer;
    private readonly PanelsDeviceSessionService deviceSessionService;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object stateGate = new();

    private CancellationTokenSource? loopCts;
    private Task? loopTask;
    private PanelsFrameComposer.PanelCompositionSession? compositionSession;
    private PanelDefinition? activePanelSnapshot;
    private string? targetDeviceId;
    private Esp32S3LedOutput? matrixOutput;
    private RgbaColor[] latestFrame = BlackFrame.ToArray();
    private bool disposed;

    public PanelsPlaybackService(
        DeviceServerHost host,
        PanelsFrameComposer composer,
        PanelsDeviceSessionService deviceSessionService)
    {
        this.host = host;
        this.composer = composer;
        this.deviceSessionService = deviceSessionService;
    }

    public event EventHandler? StateChanged;

    public event EventHandler<RgbaColor[]>? FrameUpdated;

    public bool IsRunning
    {
        get
        {
            lock (stateGate)
            {
                return loopCts is not null && compositionSession is not null && !string.IsNullOrWhiteSpace(targetDeviceId);
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

    public PanelDefinition? GetActivePanelSnapshot()
    {
        lock (stateGate)
        {
            return activePanelSnapshot?.Clone();
        }
    }

    public RgbaColor[] GetLatestFrame()
    {
        lock (stateGate)
        {
            return latestFrame.ToArray();
        }
    }

    public async Task StartAsync(PanelDefinition panelSnapshot, string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panelSnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ThrowIfDisposed();

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(restoreDevice: true, cancellationToken).ConfigureAwait(false);

            var snapshot = panelSnapshot.Clone();
            snapshot.Normalize();
            var session = await composer.CreateSessionAsync(snapshot, cancellationToken).ConfigureAwait(false);
            var output = new Esp32S3LedOutput(host);
            output.Start(new LedOutputConfig
            {
                Width = LedDefaults.MatrixWidth,
                Height = LedDefaults.MatrixHeight,
                Brightness = LedDefaults.Brightness,
                TargetDeviceId = deviceId.Trim(),
            });
            output.SetBrightness(LedDefaults.Brightness);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (stateGate)
            {
                compositionSession = session;
                activePanelSnapshot = snapshot;
                targetDeviceId = deviceId.Trim();
                matrixOutput = output;
                loopCts = cts;
            }

            await deviceSessionService.StartAsync(deviceId.Trim(), cancellationToken).ConfigureAwait(false);
            await SendFrameAsync(session, output, DateTimeOffset.UtcNow).ConfigureAwait(false);

            var localLoopTask = RunLoopAsync(session, output, cts.Token);
            lock (stateGate)
            {
                loopTask = localLoopTask;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
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
            await StopCoreAsync(restoreDevice: true, cancellationToken).ConfigureAwait(false);
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

    private async Task RunLoopAsync(
        PanelsFrameComposer.PanelCompositionSession session,
        Esp32S3LedOutput output,
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

    private async Task StopCoreAsync(bool restoreDevice, CancellationToken cancellationToken)
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
            latestFrame = BlackFrame.ToArray();
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

        if (restoreDevice)
        {
            await deviceSessionService.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task SendFrameAsync(PanelsFrameComposer.PanelCompositionSession session, Esp32S3LedOutput output, DateTimeOffset utcNow)
    {
        var frame = session.RenderFrame(utcNow);
        lock (stateGate)
        {
            latestFrame = frame.ToArray();
        }

        output.Send(LedPayloadFactory.CreateFramePayload(frame, PanelsDeviceSessionService.PanelsAppId));
        RaiseFrameUpdated(frame);
        return Task.CompletedTask;
    }

    private void RaiseFrameUpdated(RgbaColor[] frame)
    {
        FrameUpdated?.Invoke(this, frame.ToArray());
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
