using App.WinUI.Models.Panels;
using Device.Client;
using Device.Protocol.Models;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-no-servidor
// DOCS: docs/wiki/modules/app-winui.md#remote-only
// DOCS: docs/handoffs/2026-04-30-remote-only-server-panel-runtime.md
internal sealed class PanelsPlaybackService : IDisposable
{
    private static readonly RgbaColor[] BlackFrame = Enumerable
        .Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        .ToArray();

    private readonly IDeviceServerClient serverClient;
    private readonly PanelsFrameComposer composer;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object stateGate = new();

    private PanelDefinition? activePanelSnapshot;
    private string? targetDeviceId;
    private SuspendedPanelState? suspendedPanelState;
    private RgbaColor[] latestFrame = BlackFrame;
    private bool disposed;

    public PanelsPlaybackService(IDeviceServerClient serverClient, PanelsFrameComposer composer)
    {
        this.serverClient = serverClient;
        this.composer = composer;
    }

    public event EventHandler? StateChanged;

    public event EventHandler<RgbaColor[]>? FrameUpdated;

    public bool IsRunning
    {
        get
        {
            lock (stateGate)
            {
                return activePanelSnapshot is not null && !string.IsNullOrWhiteSpace(targetDeviceId);
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

    public async Task SyncFromRemoteAsync(PanelsStoreDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ThrowIfDisposed();

        try
        {
            var runtime = await serverClient.GetPanelRuntimeStateAsync(cancellationToken).ConfigureAwait(false);
            if (!runtime.Enabled
                || string.IsNullOrWhiteSpace(runtime.PanelId)
                || string.IsNullOrWhiteSpace(runtime.TargetDeviceId))
            {
                ClearActiveState();
                return;
            }

            var panel = document.Panels.FirstOrDefault(candidate =>
                string.Equals(candidate.PanelId, runtime.PanelId, StringComparison.OrdinalIgnoreCase));
            if (panel is null)
            {
                ClearActiveState();
                return;
            }

            await SetActiveStateAsync(panel.Clone(), runtime.TargetDeviceId, renderPreview: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ClearActiveState();
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
            var snapshot = panelSnapshot.Clone();
            snapshot.Normalize();
            var normalizedDeviceId = deviceId.Trim();
            await serverClient.SavePanelRuntimeStateAsync(new PanelRuntimeStateDocument
            {
                Enabled = true,
                PanelId = snapshot.PanelId,
                TargetDeviceId = normalizedDeviceId,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken).ConfigureAwait(false);

            await SetActiveStateAsync(snapshot, normalizedDeviceId, renderPreview: true, cancellationToken).ConfigureAwait(false);
            lock (stateGate)
            {
                suspendedPanelState = null;
            }
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task SuspendAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

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

            lock (stateGate)
            {
                suspendedPanelState = new SuspendedPanelState(retainedPanel, retainedDeviceId);
            }

            await SaveStoppedRuntimeStateAsync(cancellationToken).ConfigureAwait(false);
            ClearActiveState();
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public async Task ResumeSuspendedAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SuspendedPanelState? retainedState;
            lock (stateGate)
            {
                retainedState = suspendedPanelState;
            }

            if (retainedState is null)
            {
                return;
            }

            await serverClient.SavePanelRuntimeStateAsync(new PanelRuntimeStateDocument
            {
                Enabled = true,
                PanelId = retainedState.PanelSnapshot.PanelId,
                TargetDeviceId = retainedState.TargetDeviceId,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken).ConfigureAwait(false);

            await SetActiveStateAsync(retainedState.PanelSnapshot.Clone(), retainedState.TargetDeviceId, renderPreview: true, cancellationToken).ConfigureAwait(false);
            lock (stateGate)
            {
                suspendedPanelState = null;
            }
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
            await SaveStoppedRuntimeStateAsync(cancellationToken).ConfigureAwait(false);
            lock (stateGate)
            {
                suspendedPanelState = null;
            }

            ClearActiveState();
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
            SuspendedPanelState? retainedState;
            lock (stateGate)
            {
                retainedState = suspendedPanelState;
            }

            if (retainedState is not null)
            {
                serverClient.SavePanelRuntimeStateAsync(new PanelRuntimeStateDocument
                {
                    Enabled = true,
                    PanelId = retainedState.PanelSnapshot.PanelId,
                    TargetDeviceId = retainedState.TargetDeviceId,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                }).GetAwaiter().GetResult();
            }
        }
        catch
        {
        }

        lifecycleGate.Dispose();
    }

    private async Task SetActiveStateAsync(
        PanelDefinition panelSnapshot,
        string normalizedDeviceId,
        bool renderPreview,
        CancellationToken cancellationToken)
    {
        RgbaColor[] frame = BlackFrame;
        if (renderPreview)
        {
            using var session = await composer.CreateSessionAsync(panelSnapshot, cancellationToken).ConfigureAwait(false);
            frame = session.RenderFrame(DateTimeOffset.UtcNow);
        }

        lock (stateGate)
        {
            activePanelSnapshot = panelSnapshot.Clone();
            targetDeviceId = normalizedDeviceId.Trim();
            latestFrame = frame;
        }

        RaiseFrameUpdated(frame);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveStoppedRuntimeStateAsync(CancellationToken cancellationToken)
    {
        PanelDefinition? retainedPanel;
        string? retainedDeviceId;
        lock (stateGate)
        {
            retainedPanel = activePanelSnapshot;
            retainedDeviceId = targetDeviceId;
        }

        await serverClient.SavePanelRuntimeStateAsync(new PanelRuntimeStateDocument
        {
            Enabled = false,
            PanelId = retainedPanel?.PanelId,
            TargetDeviceId = retainedDeviceId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);
    }

    private void ClearActiveState()
    {
        lock (stateGate)
        {
            activePanelSnapshot = null;
            targetDeviceId = null;
            latestFrame = BlackFrame;
        }

        RaiseFrameUpdated(BlackFrame);
        StateChanged?.Invoke(this, EventArgs.Empty);
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
