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
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
internal sealed class PanelsPlaybackService : IDisposable
{
    private static readonly bool EnableBatchPerfLogging =
        AppContext.TryGetSwitch("MicaAudio.Panels.BatchPerfLogging", out var enabled) && enabled;
    private static readonly System.Text.Json.JsonSerializerOptions WebJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
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
    private CancellationTokenSource? syncToServerCts;
    private Task? loopTask;
    private PanelsFrameComposer.PanelCompositionSession? compositionSession;
    private PanelDefinition? activePanelSnapshot;
    private string? targetDeviceId;
    private Esp32S3LedOutput? matrixOutput;
    private PanelsBatchTransportState? batchTransportState;
    private SuspendedPanelState? suspendedPanelState;
    private RgbaColor[] latestFrame = BlackFrame;
    private bool disposed;

    // Mirror of what the SERVER reports as currently rendering autonomously
    // for each known device. Refreshed via RefreshServerStateAsync (called by
    // the UI on startup and after device-list changes). Read by IsServerActive
    // so the UI can mark panels as "ativo (servidor)" even when the local
    // playback loop has not been started in this session.
    private readonly Dictionary<string, ServerSidePanelInfo> serverPanelStateByDeviceId =
        new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Returns true when the panel is currently being rendered server-side
    /// (autonomously) for ANY known device — irrespective of whether this
    /// WinUI session has started its own playback loop.
    /// </summary>
    public bool IsServerActivePanel(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return false;
        }

        lock (stateGate)
        {
            foreach (var entry in serverPanelStateByDeviceId.Values)
            {
                if (string.Equals(entry.PanelId, panelId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the device on which <paramref name="panelId"/> is rendering
    /// server-side, or <see langword="null"/> when none.
    /// </summary>
    public string? GetServerActiveDeviceFor(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return null;
        }

        lock (stateGate)
        {
            foreach (var (deviceId, info) in serverPanelStateByDeviceId)
            {
                if (string.Equals(info.PanelId, panelId, StringComparison.OrdinalIgnoreCase))
                {
                    return deviceId;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the server-side panel info for a device, or <see langword="null"/>
    /// when nothing is stored for that device.
    /// </summary>
    public ServerSidePanelInfo? GetServerActivePanelFor(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        lock (stateGate)
        {
            return serverPanelStateByDeviceId.TryGetValue(deviceId, out var info) ? info : null;
        }
    }

    /// <summary>
    /// Refreshes the cached server-side panel state for the given devices.
    /// The server is the source of truth: anything stored there is reflected
    /// in the UI on the next gallery refresh. Failures (network down, server
    /// not configured) leave the cache intact and return false.
    /// </summary>
    public async Task<bool> RefreshServerStateAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);
        var distinct = deviceIds
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
        {
            lock (stateGate)
            {
                serverPanelStateByDeviceId.Clear();
            }
            RaiseStateChanged();
            return true;
        }

        var fresh = new Dictionary<string, ServerSidePanelInfo>(StringComparer.OrdinalIgnoreCase);
        var anyFailure = false;
        foreach (var deviceId in distinct)
        {
            try
            {
                var snapshot = await serverClient.GetServerPanelAsync(deviceId, cancellationToken).ConfigureAwait(false);
                if (snapshot is not null && !string.IsNullOrEmpty(snapshot.PanelId))
                {
                    fresh[deviceId] = new ServerSidePanelInfo(
                        snapshot.PanelId,
                        snapshot.PanelName,
                        snapshot.WidgetCount,
                        snapshot.Capability,
                        snapshot.PanelJson);
                }
            }
            catch
            {
                anyFailure = true;
            }
        }

        lock (stateGate)
        {
            serverPanelStateByDeviceId.Clear();
            foreach (var (deviceId, info) in fresh)
            {
                serverPanelStateByDeviceId[deviceId] = info;
            }
        }

        RaiseStateChanged();
        return !anyFailure;
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public sealed record ServerSidePanelInfo(string PanelId, string PanelName, int WidgetCount, string Capability, string PanelJson = "");

    public async Task StartAsync(PanelDefinition panelSnapshot, string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(panelSnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ThrowIfDisposed();

        if (enableMatrixTransport && hub75VisualizerSessionService.IsHub75Enabled)
        {
            throw new InvalidOperationException("O visualizador HUB75 tem prioridade enquanto estiver ativo.");
        }

        CancellationTokenSource? freshSyncCts = null;
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(restoreDevice: true, clearSuspendedState: true, cancellationToken).ConfigureAwait(false);
            await StartCoreAsync(panelSnapshot, deviceId.Trim(), resumeSuppressedSession: false, cancellationToken).ConfigureAwait(false);
            // Create the upload CTS inside the gate so SuspendAsync/StopAsync can cancel
            // it via StopCoreAsync even if SuspendAsync runs before the fire-and-forget starts.
            freshSyncCts = new CancellationTokenSource();
            lock (stateGate)
            {
                syncToServerCts = freshSyncCts;
            }
        }
        finally
        {
            lifecycleGate.Release();
        }

        // Best-effort upload to MicaAudio.Server so autonomous widgets (Clock, GIF)
        // keep rendering after the WinUI client closes. Local playback always
        // runs as before; failures here only affect the server-side fallback.
        // freshSyncCts is cancelled by StopCoreAsync if StopAsync/SuspendAsync runs
        // during the upload (e.g. user enables the visualizer while a GIF is uploading).
        _ = TrySyncPanelToServerAsync(panelSnapshot, deviceId.Trim(), freshSyncCts?.Token ?? CancellationToken.None);
    }

    private async Task TrySyncPanelToServerAsync(PanelDefinition panelSnapshot, string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Build a server-friendly clone: replace local sourcePath references with
            // mediaId values that the server can resolve from its media store.
            // gifhub75 widgets with a sourcePath are uploaded first; the panel JSON
            // is sent last so the server can immediately render the widgets it finds.
            var serverPanel = await BuildServerPanelAsync(panelSnapshot, deviceId, cancellationToken).ConfigureAwait(false);
            var json = System.Text.Json.JsonSerializer.Serialize(
                serverPanel,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            await serverClient.UploadPanelAsync(deviceId, json, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Upload was intentionally cancelled (e.g. visualizer activated during upload).
        }
        catch
        {
            // Server might be temporarily unreachable; ignore so local playback
            // is unaffected. Next StartAsync call will retry the upload.
        }
    }

    /// <summary>
    /// Finds which device is rendering <paramref name="panelId"/> on the server
    /// and deletes it. Used when the panel has no local loop running (server-only active).
    /// Refreshes cached server state if the first lookup finds no match.
    /// </summary>
    public async Task DeleteServerPanelForPanel(string panelId, CancellationToken cancellationToken = default)
    {
        var deviceId = GetServerActiveDeviceFor(panelId);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            // Cached state may be stale — refresh once and retry before giving up.
            List<string> knownDevices;
            lock (stateGate)
            {
                knownDevices = serverPanelStateByDeviceId.Keys.ToList();
            }

            if (knownDevices.Count > 0)
            {
                await RefreshServerStateAsync(knownDevices, cancellationToken).ConfigureAwait(false);
                deviceId = GetServerActiveDeviceFor(panelId);
            }
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            await TryDeleteServerPanelForDeviceAsync(deviceId).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fire-and-forget: upserts the panel into the server-side catalog so all
    /// clients can discover it. Serializes using the WinUI PanelDefinition shape.
    /// Swallows exceptions so callers are never blocked.
    /// </summary>
    public void FireSyncCatalogPanel(PanelDefinition panel)
    {
        if (panel is null)
        {
            return;
        }

        _ = TrySyncCatalogPanelAsync(panel);
    }

    public Task<string?> GetCatalogJsonAsync(CancellationToken cancellationToken = default)
    {
        return serverClient.GetPanelCatalogJsonAsync(cancellationToken);
    }

    private async Task TrySyncCatalogPanelAsync(PanelDefinition panel)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(panel, WebJsonOptions);
            await serverClient.UpsertCatalogPanelAsync(panel.PanelId, json, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Server may be unreachable; ignore.
        }
    }

    /// <summary>
    /// Fire-and-forget: removes the panel from the server-side catalog.
    /// Swallows exceptions.
    /// </summary>
    public void FireDeleteCatalogPanel(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return;
        }

        _ = TryDeleteCatalogPanelAsync(panelId);
    }

    private async Task TryDeleteCatalogPanelAsync(string panelId)
    {
        try
        {
            await serverClient.DeleteCatalogPanelAsync(panelId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Server may be unreachable; ignore.
        }
    }

    private void FireDeleteServerPanel(string deviceId)
        => _ = TryDeleteServerPanelForDeviceAsync(deviceId);

    private async Task TryDeleteServerPanelForDeviceAsync(string deviceId)
    {
        try
        {
            await serverClient.DeletePanelAsync(deviceId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Server may be unreachable; ignore so callers are never blocked.
        }

        lock (stateGate)
        {
            serverPanelStateByDeviceId.Remove(deviceId);
        }

        RaiseStateChanged();
    }

    private static readonly HashSet<string> SupportedMediaExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp" };

    /// <summary>
    /// Returns a clone of <paramref name="panel"/> where each gifhub75 widget's
    /// local <c>sourcePath</c> has been replaced by a list of <c>mediaIds</c> after
    /// uploading the source file (or every supported image inside the source folder
    /// for slideshow mode) to the server media store. Widgets whose source files
    /// cannot be read are left unchanged (the server will classify them as
    /// RequiresClient and skip autonomous rendering).
    /// </summary>
    private async Task<PanelDefinition> BuildServerPanelAsync(PanelDefinition panel, string deviceId, CancellationToken cancellationToken = default)
    {
        var clone = panel.Clone();
        foreach (var widget in clone.Widgets)
        {
            if (!string.Equals(widget.AppId, "gifhub75", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!widget.RuntimeState.TryGetValue("sourcePath", out var sourcePath)
                || string.IsNullOrWhiteSpace(sourcePath))
            {
                Debug.WriteLine($"[panel-sync] widget {widget.WidgetId}: sourcePath ausente.");
                continue;
            }

            var sourceFiles = ResolveSourceFiles(sourcePath);
            if (sourceFiles.Count == 0)
            {
                Debug.WriteLine($"[panel-sync] widget {widget.WidgetId}: nenhum arquivo de imagem encontrado em '{sourcePath}'.");
                continue;
            }

            var uploadedIds = new List<string>(sourceFiles.Count);
            for (var i = 0; i < sourceFiles.Count; i++)
            {
                var file = sourceFiles[i];
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext))
                {
                    continue;
                }

                // Stable mediaId per (widgetId, index): re-upload overwrites idempotently.
                // Index suffix lets a single widget host multiple images for slideshow.
                var mediaId = $"{SanitizeSegment(widget.WidgetId)}-{i:D3}{ext}";

                try
                {
                    var bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
                    await serverClient.UploadMediaAsync(deviceId, mediaId, bytes, cancellationToken).ConfigureAwait(false);
                    uploadedIds.Add(mediaId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[panel-sync] upload falhou para '{file}' -> {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (uploadedIds.Count == 0)
            {
                Debug.WriteLine($"[panel-sync] widget {widget.WidgetId}: nenhum upload concluido com sucesso.");
                continue;
            }

            // mediaIds = canonical list (1+ entries). Server iterates this for slideshow.
            // Also set legacy mediaId to the first entry for older server builds.
            widget.RuntimeState["mediaIds"] = string.Join(",", uploadedIds);
            widget.RuntimeState["mediaId"] = uploadedIds[0];
            Debug.WriteLine($"[panel-sync] widget {widget.WidgetId}: {uploadedIds.Count} arquivo(s) enviado(s).");
        }

        return clone;
    }

    /// <summary>
    /// Returns the list of files that should be uploaded for this widget.
    /// Single file → list of one. Directory → all supported images inside, sorted.
    /// </summary>
    private static IReadOnlyList<string> ResolveSourceFiles(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            return new[] { sourcePath };
        }

        if (Directory.Exists(sourcePath))
        {
            return Directory
                .EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => SupportedMediaExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return Array.Empty<string>();
    }

    private static string SanitizeSegment(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
        }

        return sb.Length > 0 ? sb.ToString() : "widget";
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
            bool hasLocalSession;
            lock (stateGate)
            {
                retainedPanel = activePanelSnapshot?.Clone();
                retainedDeviceId = targetDeviceId;
            }

            hasLocalSession = retainedPanel is not null && !string.IsNullOrWhiteSpace(retainedDeviceId);

            if (!hasLocalSession)
            {
                // No local playback loop — check for a server-side panel that the
                // compositor is rendering autonomously (e.g. clock active from a
                // previous session). Suspend it so the visualizer gets exclusive access.
                (retainedPanel, retainedDeviceId) = FindServerSidePanelToSuspend();
            }

            if (retainedPanel is null || string.IsNullOrWhiteSpace(retainedDeviceId))
            {
                return;
            }

            if (hasLocalSession)
            {
                await deviceSessionService.SuppressAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (stateGate)
            {
                suspendedPanelState = new SuspendedPanelState(retainedPanel, retainedDeviceId);
            }

            if (hasLocalSession)
            {
                await StopCoreAsync(restoreDevice: false, clearSuspendedState: false, cancellationToken).ConfigureAwait(false);
            }

            // Tell the server to stop rendering so the visualizer gets exclusive
            // frame access. ResumeSuspendedAsync re-uploads the panel when done.
            await TryDeleteServerPanelForDeviceAsync(retainedDeviceId).ConfigureAwait(false);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions PanelJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private (PanelDefinition? panel, string? deviceId) FindServerSidePanelToSuspend()
    {
        lock (stateGate)
        {
            foreach (var (deviceId, info) in serverPanelStateByDeviceId)
            {
                if (!string.Equals(info.Capability, "ServerCapable", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(info.PanelJson))
                {
                    continue;
                }

                try
                {
                    var panel = System.Text.Json.JsonSerializer.Deserialize<PanelDefinition>(
                        info.PanelJson, PanelJsonOptions);
                    if (panel is not null)
                    {
                        return (panel, deviceId);
                    }
                }
                catch
                {
                    // Malformed JSON — skip this entry.
                }
            }
        }

        return (null, null);
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

            // Create CTS inside gate — same pattern as StartAsync (public).
            var freshSyncCts = new CancellationTokenSource();
            lock (stateGate)
            {
                syncToServerCts = freshSyncCts;
            }

            // Re-upload the panel to the server so autonomous widgets (Clock, GIF) resume
            // after the visualizer session ends. Mirrors what StartAsync (public) does for
            // fresh activations.
            _ = TrySyncPanelToServerAsync(retainedState.PanelSnapshot, retainedState.TargetDeviceId, freshSyncCts.Token);
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
            string? localDeviceId;
            lock (stateGate) { localDeviceId = targetDeviceId; }

            // Delete from server BEFORE restoring the device session to eliminate
            // the window where the compositor could resume rendering after the
            // session is released but before the store entry is removed.
            if (!string.IsNullOrWhiteSpace(localDeviceId))
            {
                await TryDeleteServerPanelForDeviceAsync(localDeviceId).ConfigureAwait(false);
            }

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

            if (enableMatrixTransport && await SupportsAnimatedWebpBatchAsync(normalizedDeviceId, cancellationToken).ConfigureAwait(false))
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
                        await serverClient.ClearPanelsBatchesAsync(deviceId, batchState.PanelsSessionId, cancellationToken).ConfigureAwait(false);
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
        CancellationTokenSource? localSyncCts;
        Task? localLoopTask;
        PanelsFrameComposer.PanelCompositionSession? localSession;
        Esp32S3LedOutput? localOutput;
        string? localTargetDeviceId;
        PanelsBatchTransportState? localBatchState;

        lock (stateGate)
        {
            localLoopCts = loopCts;
            localSyncCts = syncToServerCts;
            localLoopTask = loopTask;
            localSession = compositionSession;
            localOutput = matrixOutput;
            localTargetDeviceId = targetDeviceId;
            localBatchState = batchTransportState;

            loopCts = null;
            syncToServerCts = null;
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

        // Cancel the background server-sync upload before tearing down playback.
        // This prevents a GIF upload that started before a SuspendAsync/StopAsync
        // from completing after the panel is deleted, re-uploading it and
        // restarting the compositor (visualizer/panel conflict).
        if (localSyncCts is not null)
        {
            localSyncCts.Cancel();
            localSyncCts.Dispose();
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
                await serverClient.ClearPanelsBatchesAsync(localTargetDeviceId, localBatchState.PanelsSessionId, cancellationToken).ConfigureAwait(false);
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

    private async Task<bool> SupportsAnimatedWebpBatchAsync(string deviceId, CancellationToken cancellationToken)
    {
        var devices = await serverClient.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        return devices
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
                await serverClient.ClearPanelsBatchesAsync(deviceId, state.PanelsSessionId, cancellationToken).ConfigureAwait(false);
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
        var registration = await serverClient
            .RegisterPanelsBatchAsync(
                deviceId,
                state.PanelsSessionId,
                state.NextBatchSequence,
                encodedBatch.Payload,
                encodedBatch.FrameCount,
                encodedBatch.DurationMs,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

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
