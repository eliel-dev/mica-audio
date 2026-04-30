using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using App.WinUI.Models.Panels;
using App.WinUI.Services.Panels;
using Device.Protocol.Models;
using Device.Server.Hosting;
using MicaAudio.Core.Led;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MicaAudio.Server;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-server-owned
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
public sealed class ServerOwnedPanelsRuntimeService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BatchDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan BatchPreloadLead = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private static readonly Action<ILogger, Exception?> LogRuntimeDisabled =
        LoggerMessage.Define(LogLevel.Information, new EventId(6101, nameof(LogRuntimeDisabled)), "Runtime autonomo de paineis server-owned desabilitado.");
    private static readonly Action<ILogger, Exception?> LogRuntimeTickFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(6102, nameof(LogRuntimeTickFailed)), "Falha no tick do runtime autonomo de paineis.");
    private static readonly Action<ILogger, string, Exception?> LogQueueBatchFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(6103, nameof(LogQueueBatchFailed)), "Falha ao enfileirar batch server-owned para {DeviceId}.");

    private readonly MicaAudioServerOptions options;
    private readonly DeviceServerHost deviceServerHost;
    private readonly IPanelLibraryStore panelLibraryStore;
    private readonly IMediaLibraryStore mediaLibraryStore;
    private readonly IPanelRuntimeDiagnosticsStore diagnosticsStore;
    private readonly ILogger<ServerOwnedPanelsRuntimeService> logger;
    private readonly PanelsFrameComposer composer;
    private readonly Dictionary<string, DeviceRuntimeState> runtimeStates = new(StringComparer.OrdinalIgnoreCase);

    public ServerOwnedPanelsRuntimeService(
        MicaAudioServerOptions options,
        DeviceServerHost deviceServerHost,
        IPanelLibraryStore panelLibraryStore,
        IMediaLibraryStore mediaLibraryStore,
        IPanelRuntimeDiagnosticsStore diagnosticsStore,
        ILogger<ServerOwnedPanelsRuntimeService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(deviceServerHost);
        ArgumentNullException.ThrowIfNull(panelLibraryStore);
        ArgumentNullException.ThrowIfNull(mediaLibraryStore);
        ArgumentNullException.ThrowIfNull(diagnosticsStore);
        ArgumentNullException.ThrowIfNull(logger);

        this.options = options;
        this.deviceServerHost = deviceServerHost;
        this.panelLibraryStore = panelLibraryStore;
        this.mediaLibraryStore = mediaLibraryStore;
        this.diagnosticsStore = diagnosticsStore;
        this.logger = logger;
        composer = new PanelsFrameComposer(
            new App.WinUI.Services.Gif.Hub75GifDecoder(App.WinUI.Services.Gif.Hub75GifDecoder.DefaultMaxGifFrames),
            new PanelsMediaCache(),
            new ServerPanelMediaSourceResolver(mediaLibraryStore));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.PanelsAutoRuntimeEnabled)
        {
            LogRuntimeDisabled(logger, null);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogRuntimeTickFailed(logger, ex);
            }

            await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var state in runtimeStates.Values)
        {
            state.Dispose();
        }

        runtimeStates.Clear();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var library = await panelLibraryStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var panelsById = (library.Panels ?? Array.Empty<PanelLibraryItem>())
            .Where(static panel => !string.IsNullOrWhiteSpace(panel.PanelId) && panel.IsEnabled)
            .ToDictionary(static panel => panel.PanelId.Trim(), StringComparer.OrdinalIgnoreCase);
        var activeStates = (library.ActivePanels ?? Array.Empty<PanelDeviceState>())
            .Where(static state => !string.IsNullOrWhiteSpace(state.DeviceId))
            .GroupBy(static state => state.DeviceId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var snapshots = deviceServerHost.GetDevicesSnapshot()
            .ToDictionary(static snapshot => snapshot.DeviceId, StringComparer.OrdinalIgnoreCase);

        foreach (var staleDeviceId in runtimeStates.Keys.Except(activeStates.Keys, StringComparer.OrdinalIgnoreCase).ToArray())
        {
            StopDeviceRuntime(staleDeviceId);
            diagnosticsStore.Remove(staleDeviceId);
        }

        foreach (var (deviceId, activeState) in activeStates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await TickDeviceAsync(deviceId, activeState, panelsById, snapshots, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TickDeviceAsync(
        string deviceId,
        PanelDeviceState activeState,
        Dictionary<string, PanelLibraryItem> panelsById,
        Dictionary<string, DeviceSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var activePanelId = NormalizePanelId(activeState.ActivePanelId);
        if (activePanelId is null)
        {
            StopDeviceRuntime(deviceId);
            diagnosticsStore.Remove(deviceId);
            return;
        }

        if (!panelsById.TryGetValue(activePanelId, out var panel))
        {
            StopDeviceRuntime(deviceId);
            UpdateDiagnostic(deviceId, activePanelId, "panel_missing", 0, "Painel ativo nao existe na biblioteca.");
            return;
        }

        var runtimePanel = CreateServerOwnedPanel(panel);
        if (runtimePanel.Widgets.Count == 0)
        {
            StopDeviceRuntime(deviceId);
            UpdateDiagnostic(deviceId, activePanelId, "no_server_widgets", 0, "Painel nao possui widgets dataSource=server no V1.");
            return;
        }

        if (!snapshots.TryGetValue(deviceId, out var snapshot) || snapshot.Status != DeviceStatus.Online)
        {
            StopDeviceRuntime(deviceId);
            UpdateDiagnostic(deviceId, activePanelId, "waiting_device", 0, "Device offline; aguardando MQTT/WS voltar.");
            return;
        }

        if (snapshot.AnimatedWebpBatchSupported != true)
        {
            StopDeviceRuntime(deviceId);
            UpdateDiagnostic(deviceId, activePanelId, "unsupported", 0, "Firmware nao anunciou suporte a animatedWebpBatchSupported.");
            return;
        }

        var state = await GetOrCreateStateAsync(deviceId, runtimePanel, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        if (now + BatchPreloadLead < state.NextBatchStartUtc)
        {
            UpdateDiagnostic(deviceId, activePanelId, "running", state.LastQueuedBatchSequence, null);
            return;
        }

        try
        {
            await QueueNextBatchAsync(deviceId, state, cancellationToken).ConfigureAwait(false);
            UpdateDiagnostic(deviceId, activePanelId, "running", state.LastQueuedBatchSequence, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UpdateDiagnostic(deviceId, activePanelId, "failed", state.LastQueuedBatchSequence, ex.Message);
            LogQueueBatchFailed(logger, deviceId, ex);
        }
    }

    private async Task<DeviceRuntimeState> GetOrCreateStateAsync(
        string deviceId,
        PanelDefinition panel,
        CancellationToken cancellationToken)
    {
        var panelFingerprint = CreatePanelFingerprint(panel);
        if (runtimeStates.TryGetValue(deviceId, out var existing)
            && string.Equals(existing.PanelId, panel.PanelId, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(existing.PanelFingerprint, panelFingerprint, StringComparison.Ordinal))
            {
                return existing;
            }
        }

        StopDeviceRuntime(deviceId);
        var session = await composer.CreateSessionAsync(panel, cancellationToken).ConfigureAwait(false);
        var state = new DeviceRuntimeState(
            panel.PanelId,
            panelFingerprint,
            Guid.NewGuid().ToString("N"),
            session,
            DateTimeOffset.UtcNow);
        runtimeStates[deviceId] = state;
        return state;
    }

    private async Task QueueNextBatchAsync(
        string deviceId,
        DeviceRuntimeState state,
        CancellationToken cancellationToken)
    {
        var encodedBatch = RenderEncodedBatch(state.Session, state.NextBatchStartUtc);
        var registration = deviceServerHost.RegisterPanelsBatch(
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

        var result = await deviceServerHost.SendCommandTrackedAsync(
                deviceId,
                DeviceCommandType.QueuePanelsBatch,
                payload.ToParameters(),
                timeout: CommandTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Accepted || !result.Success)
        {
            throw new InvalidOperationException(result.Message ?? result.ErrorCode ?? "Device recusou queue_panels_batch.");
        }

        state.Advance();
    }

    private static PanelsEncodedBatch RenderEncodedBatch(
        PanelsFrameComposer.PanelCompositionSession session,
        DateTimeOffset batchStartUtc)
    {
        return PanelsAnimatedWebpEncoder.Encode(
            PanelsFrameComposer.TargetFps,
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight,
            (frameIndex, targetFrame) =>
            {
                session.RenderFrameInto(
                    batchStartUtc + TimeSpan.FromMilliseconds(GetBatchFrameOffsetMs(frameIndex)),
                    targetFrame);
            },
            GetBatchFrameDurationMs);
    }

    private static PanelDefinition CreateServerOwnedPanel(PanelLibraryItem panel)
    {
        var definition = new PanelDefinition
        {
            PanelId = panel.PanelId,
            Name = panel.Name,
            Width = panel.Width,
            Height = panel.Height,
            Widgets = panel.Widgets
                .Where(static widget => string.Equals(
                    PanelWidgetDataSources.Normalize(widget.DataSource),
                    PanelWidgetDataSources.Server,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(static widget => widget.ZIndex)
                .Select(static widget => new PanelWidgetDefinition
                {
                    WidgetId = widget.WidgetId,
                    AppId = widget.AppId,
                    DataSource = PanelWidgetDataSources.Server,
                    X = widget.X,
                    Y = widget.Y,
                    Width = widget.Width,
                    Height = widget.Height,
                    ZIndex = widget.ZIndex,
                    ConfigValues = new Dictionary<string, string>(widget.ConfigValues, StringComparer.OrdinalIgnoreCase),
                    RuntimeState = new Dictionary<string, string>(widget.RuntimeState, StringComparer.OrdinalIgnoreCase),
                })
                .ToList(),
        };
        definition.Normalize();
        return definition;
    }

    private void StopDeviceRuntime(string deviceId)
    {
        if (!runtimeStates.Remove(deviceId, out var state))
        {
            return;
        }

        deviceServerHost.ClearPanelsBatches(deviceId, state.PanelsSessionId);
        state.Dispose();
    }

    private void UpdateDiagnostic(
        string deviceId,
        string? panelId,
        string state,
        ulong lastBatchSequence,
        string? lastError)
    {
        diagnosticsStore.Upsert(new PanelRuntimeDeviceDiagnostic
        {
            DeviceId = deviceId,
            PanelId = panelId,
            State = state,
            LastBatchSequence = lastBatchSequence,
            LastError = lastError,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
    }

    private static string? NormalizePanelId(string? panelId)
        => string.IsNullOrWhiteSpace(panelId) ? null : panelId.Trim();

    private static int GetBatchFrameOffsetMs(int frameIndex)
        => (frameIndex * (int)BatchDuration.TotalMilliseconds) / PanelsFrameComposer.TargetFps;

    private static int GetBatchFrameDurationMs(int frameIndex)
    {
        var frameOffsetMs = GetBatchFrameOffsetMs(frameIndex);
        var nextFrameOffsetMs = GetBatchFrameOffsetMs(frameIndex + 1);
        return Math.Max(1, nextFrameOffsetMs - frameOffsetMs);
    }

    private static string CreatePanelFingerprint(PanelDefinition panel)
    {
        var builder = new StringBuilder();
        AppendToken(builder, panel.PanelId);
        builder.Append('|').Append(panel.Width.ToString(CultureInfo.InvariantCulture));
        builder.Append('|').Append(panel.Height.ToString(CultureInfo.InvariantCulture));

        foreach (var widget in panel.Widgets.OrderBy(static widget => widget.ZIndex).ThenBy(static widget => widget.WidgetId, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append('|');
            AppendToken(builder, widget.WidgetId);
            AppendToken(builder, widget.AppId);
            AppendToken(builder, widget.DataSource);
            builder.Append(widget.X.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(widget.Y.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(widget.Width.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(widget.Height.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(widget.ZIndex.ToString(CultureInfo.InvariantCulture));
            AppendDictionary(builder, widget.ConfigValues);
            AppendDictionary(builder, widget.RuntimeState);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendDictionary(StringBuilder builder, IReadOnlyDictionary<string, string> values)
    {
        foreach (var pair in values.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            AppendToken(builder, pair.Key);
            AppendToken(builder, pair.Value);
        }
    }

    private static void AppendToken(StringBuilder builder, string? value)
    {
        value ??= string.Empty;
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
    }

    private sealed class DeviceRuntimeState : IDisposable
    {
        public DeviceRuntimeState(
            string panelId,
            string panelFingerprint,
            string panelsSessionId,
            PanelsFrameComposer.PanelCompositionSession session,
            DateTimeOffset nextBatchStartUtc)
        {
            PanelId = panelId;
            PanelFingerprint = panelFingerprint;
            PanelsSessionId = panelsSessionId;
            Session = session;
            NextBatchStartUtc = nextBatchStartUtc;
        }

        public string PanelId { get; }

        public string PanelFingerprint { get; }

        public string PanelsSessionId { get; }

        public PanelsFrameComposer.PanelCompositionSession Session { get; }

        public ulong NextBatchSequence { get; private set; } = 1;

        public ulong LastQueuedBatchSequence { get; private set; }

        public DateTimeOffset NextBatchStartUtc { get; private set; }

        public void Advance()
        {
            LastQueuedBatchSequence = NextBatchSequence;
            NextBatchSequence++;
            NextBatchStartUtc += BatchDuration;
        }

        public void Dispose()
        {
            Session.Dispose();
        }
    }

    private sealed class ServerPanelMediaSourceResolver(IMediaLibraryStore mediaLibraryStore) : IPanelMediaSourceResolver
    {
        public async Task<IReadOnlyList<PanelMediaSource>> ResolveAsync(
            PanelWidgetDefinition widget,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(widget);

            var mediaIds = ResolveMediaIds(widget);
            if (mediaIds.Length == 0)
            {
                return Array.Empty<PanelMediaSource>();
            }

            var index = (await mediaLibraryStore.LoadIndexAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(static media => media.MediaId, StringComparer.OrdinalIgnoreCase);
            var sources = new List<PanelMediaSource>(mediaIds.Length);
            foreach (var mediaId in mediaIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var payload = await mediaLibraryStore.ReadBytesAsync(mediaId, cancellationToken).ConfigureAwait(false);
                if (payload is null)
                {
                    continue;
                }

                var fileName = index.TryGetValue(mediaId, out var info)
                    ? info.FileName
                    : mediaId;
                sources.Add(new PanelMediaSource(mediaId, fileName, payload));
            }

            return sources;
        }

        private static string[] ResolveMediaIds(PanelWidgetDefinition widget)
        {
            if (widget.RuntimeState.TryGetValue(PanelWidgetRuntimeStateKeys.MediaIds, out var rawIds)
                && !string.IsNullOrWhiteSpace(rawIds))
            {
                return rawIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static mediaId => !string.IsNullOrWhiteSpace(mediaId))
                    .ToArray();
            }

            if (widget.RuntimeState.TryGetValue(PanelWidgetRuntimeStateKeys.MediaId, out var rawId)
                && !string.IsNullOrWhiteSpace(rawId))
            {
                return [rawId.Trim()];
            }

            return Array.Empty<string>();
        }
    }
}
