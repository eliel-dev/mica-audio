
using System.Globalization;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace App.Headless.Services;

internal sealed class DeviceStateService : IHostedService
{
    private const int MaxGlobalLogEntries = 600;
    private const int MaxDeviceLogEntries = 120;
    private const int DashboardTrendSampleCapacity = 20;
    private const int EspDashHistorySampleCapacity = 30;

    private readonly object gate = new();
    private readonly IDeviceServerHost serverHost;
    private readonly HeadlessAudioPipeline audioPipeline;
    private readonly IUiRealtimePublisher realtimePublisher;
    private readonly ILogger<DeviceStateService> logger;

    private readonly List<string> globalLogs = new();
    private readonly Dictionary<string, List<string>> deviceLogsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> awaitingFirstTelemetryByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<int>> loopTrendByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<int>> espDashLoopHistoryByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<int>> espDashHeapHistoryByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, uint?> lastTelemetrySequenceByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> lastProgressPercentByCommandKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UiCommandStatusViewModel> commandByDevice = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<DeviceSnapshot> snapshots = Array.Empty<DeviceSnapshot>();
    private UiCommandStatusViewModel lastCommandStatus = new();
    private readonly LifecycleThresholds lifecycleThresholds = LifecycleThresholds.Default;

    public DeviceStateService(
        IDeviceServerHost serverHost,
        HeadlessAudioPipeline audioPipeline,
        IUiRealtimePublisher realtimePublisher,
        ILogger<DeviceStateService> logger)
    {
        this.serverHost = serverHost;
        this.audioPipeline = audioPipeline;
        this.realtimePublisher = realtimePublisher;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        serverHost.DevicesChanged += OnDevicesChanged;
        serverHost.LogMessage += OnHostLogMessage;
        serverHost.CommandProgressChanged += OnHostCommandProgressChanged;

        RefreshSnapshots();
        AppendGlobalLog("Servico de estado de dispositivos inicializado.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        serverHost.DevicesChanged -= OnDevicesChanged;
        serverHost.LogMessage -= OnHostLogMessage;
        serverHost.CommandProgressChanged -= OnHostCommandProgressChanged;
        return Task.CompletedTask;
    }

    public IReadOnlyList<UiDeviceListItem> GetDevices()
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            return snapshots.Select(snapshot =>
            {
                var presence = DevicesPageFormatting.BuildLifecycle(snapshot, lifecycleThresholds, now);
                return BuildDeviceListItem(snapshot, presence);
            }).ToArray();
        }
    }

    public UiDeviceDetailsViewModel GetDeviceDetails(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return BuildEmptyDetails();
        }

        lock (gate)
        {
            var snapshot = snapshots.FirstOrDefault(item => string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (snapshot is null)
            {
                return BuildEmptyDetails(deviceId.Trim());
            }

            var presence = DevicesPageFormatting.BuildLifecycle(snapshot, lifecycleThresholds, DateTimeOffset.UtcNow);
            var serverLine = $"Servidor: {ResolveServerBaseAddress()}";
            var loopSeries = GetSeries(loopTrendByDeviceId, snapshot.DeviceId, DashboardTrendSampleCapacity);
            var loopHistory = GetSeries(espDashLoopHistoryByDeviceId, snapshot.DeviceId, EspDashHistorySampleCapacity);
            var heapHistory = GetSeries(espDashHeapHistoryByDeviceId, snapshot.DeviceId, EspDashHistorySampleCapacity);
            var logs = GetDeviceLogsUnsafe(snapshot.DeviceId);
            var command = GetCommandStatusUnsafe(snapshot.DeviceId);
            var metrics = DevicesPageFormatting.BuildMetrics(snapshot);

            return new UiDeviceDetailsViewModel
            {
                DeviceId = snapshot.DeviceId,
                Exists = true,
                Name = snapshot.Name,
                Subtitle = DevicesPageFormatting.BuildStatusLine(snapshot, presence),
                RegistrationLine = DevicesPageFormatting.BuildRegistrationLine(snapshot, presence),
                AppLabel = DevicesPageFormatting.BuildSelectedAppLabel(snapshot.Status, snapshot.ActiveAppName),
                ServerLine = serverLine,
                SignalLabel = DevicesPageFormatting.BuildSelectedSignalLabel(snapshot),
                CanRunCommands = presence.CanRunCommands && !command.InProgress,
                CanRemove = !command.InProgress,
                TestLedAvailable = snapshot.TestLedAvailable != false,
                BrightnessValue = DevicesPageFormatting.ResolveBrightnessCap(snapshot),
                BrightnessValueLabel = DevicesPageFormatting.BuildBrightnessValueLabel(snapshot),
                BrightnessStatusLabel = DevicesPageFormatting.BuildBrightnessStatusLabel(snapshot),
                HeartbeatLabel = DevicesPageFormatting.BuildHeartbeatLabel(snapshot),
                LoopTrendSeries = loopSeries,
                LoopTrendPlaceholder = loopSeries.Count == 0
                    ? "Sem historico de CPU ainda."
                    : string.Empty,
                Metrics = metrics,
                EspDash = DevicesPageFormatting.BuildEspDash(snapshot, loopHistory, heapHistory),
                Connectivity = DevicesPageFormatting.BuildConnectivity(snapshot),
                Command = command,
                DeviceLogCount = logs.Count,
                DeviceLogsPlaceholder = logs.Count == 0
                    ? "Sem eventos para este dispositivo ainda."
                    : string.Empty,
            };
        }
    }

    public UiDeviceLogsViewModel GetDeviceLogs(string deviceId)
    {
        lock (gate)
        {
            var normalizedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? string.Empty : deviceId.Trim();
            var deviceLogs = GetDeviceLogsUnsafe(normalizedDeviceId);
            var globalTail = globalLogs.Count <= 100
                ? globalLogs.ToArray()
                : globalLogs.Skip(Math.Max(0, globalLogs.Count - 100)).ToArray();

            return new UiDeviceLogsViewModel
            {
                DeviceId = normalizedDeviceId,
                DeviceEntries = deviceLogs,
                GlobalEntries = globalTail,
                Placeholder = deviceLogs.Count == 0
                    ? "Sem eventos para este dispositivo ainda."
                    : string.Empty,
            };
        }
    }

    public UiHealthResponse GetHealth()
    {
        lock (gate)
        {
            return new UiHealthResponse
            {
                Status = "ok",
                AudioCapturing = audioPipeline.GetStateSnapshot().AudioCapturing,
                DevicesOnline = snapshots.Count(static item => item.Status == DeviceStatus.Online),
                CommandInProgress = commandByDevice.Values.Any(static item => item.InProgress),
                TimestampUtc = DateTimeOffset.UtcNow,
            };
        }
    }

    public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
    {
        var pair = serverHost.CreatePairingCode(ttl);
        AppendGlobalLog($"Codigo de pareamento: {pair.Code} (expira {pair.ExpiresAtUtc:HH:mm:ss} UTC)");
        realtimePublisher.Publish(new
        {
            type = "onboarding-result",
            success = true,
            stage = "pairing",
            message = $"Codigo de pareamento gerado: {pair.Code}",
            pairCode = pair.Code,
        });
        return pair;
    }

    public async Task<UiCommandResponse> RunCommandAsync(UiDeviceCommandRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return new UiCommandResponse
            {
                DeviceId = string.Empty,
                Accepted = false,
                Completed = true,
                Success = false,
                ErrorCode = "invalid_device",
                Message = "Nenhum dispositivo selecionado.",
            };
        }

        var deviceId = request.DeviceId.Trim();
        var operationLabel = string.IsNullOrWhiteSpace(request.OperationLabel)
            ? DescribeCommand(request.CommandType)
            : request.OperationLabel;

        SetCommandStatus(deviceId, inProgress: true, percent: 0, $"Comandos: 0% ({operationLabel})", "queued", null, null, null);
        AppendDeviceLog(deviceId, $"Comando iniciado ({operationLabel}).");

        CommandDispatchResult result;
        try
        {
            result = await serverHost.SendCommandTrackedAsync(
                deviceId,
                request.CommandType,
                request.Parameters,
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "cancelled",
                Message = "Operacao cancelada.",
                ErrorCode = "cancelled",
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao executar comando tracked.");
            result = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "error",
                Message = ex.Message,
                ErrorCode = "exception",
            };
        }

        var finalPercent = Math.Clamp(result.ProgressPercent, 0, 100);
        var finalStatus = BuildFinalCommandStatus(result);
        SetCommandStatus(
            deviceId,
            inProgress: false,
            percent: finalPercent,
            finalStatus,
            string.IsNullOrWhiteSpace(result.Stage) ? "done" : result.Stage,
            result.CommandId,
            result.ErrorCode,
            result.Message);

        var completionMessage = BuildResultLogMessage(result);
        AppendDeviceLog(deviceId, completionMessage);
        AppendGlobalLog(completionMessage);

        realtimePublisher.Publish(new
        {
            type = "command-progress",
            deviceId,
            commandId = result.CommandId,
            stage = result.Stage ?? "done",
            percent = finalPercent,
            status = finalStatus,
            success = result.Success,
            errorCode = result.ErrorCode,
            message = result.Message ?? completionMessage,
        });

        return new UiCommandResponse
        {
            DeviceId = deviceId,
            Accepted = result.Accepted,
            Completed = result.Completed,
            Success = result.Success,
            CommandId = result.CommandId,
            Stage = result.Stage,
            ErrorCode = result.ErrorCode,
            Message = result.Message,
        };
    }
    public async Task<UiRemoveDeviceResponse> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new UiRemoveDeviceResponse
            {
                DeviceId = string.Empty,
                Removed = false,
                Message = "Nenhum dispositivo selecionado.",
            };
        }

        var normalizedDeviceId = deviceId.Trim();
        DeviceSnapshot? snapshot;
        lock (gate)
        {
            snapshot = snapshots.FirstOrDefault(item => string.Equals(item.DeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase));
        }

        var wasOnline = snapshot?.Status == DeviceStatus.Online;
        var revokeAttempted = false;
        var revokeSucceeded = false;
        string? revokeError = null;

        if (wasOnline)
        {
            revokeAttempted = true;
            var revokeResult = await RunCommandAsync(
                new UiDeviceCommandRequest
                {
                    DeviceId = normalizedDeviceId,
                    CommandType = DeviceCommandType.RevokeAndRestart,
                    OperationLabel = "revogar/reiniciar",
                },
                cancellationToken).ConfigureAwait(false);

            revokeSucceeded = revokeResult.Accepted && revokeResult.Completed && revokeResult.Success;
            revokeError = revokeResult.Message ?? revokeResult.ErrorCode;
        }

        var removed = serverHost.RemoveDevice(normalizedDeviceId);
        RefreshSnapshots();

        if (!removed)
        {
            return new UiRemoveDeviceResponse
            {
                DeviceId = normalizedDeviceId,
                Removed = false,
                WasOnline = wasOnline,
                RevokeAttempted = revokeAttempted,
                RevokeSucceeded = revokeSucceeded,
                RevokeError = revokeError,
                Message = "Falha ao remover dispositivo.",
            };
        }

        var message = wasOnline
            ? revokeSucceeded
                ? $"Dispositivo revogado e removido: {normalizedDeviceId}"
                : $"Dispositivo removido localmente; revogacao nao concluida: {normalizedDeviceId}"
            : $"Dispositivo removido: {normalizedDeviceId}";

        AppendGlobalLog(message);
        AppendDeviceLog(normalizedDeviceId, message);

        return new UiRemoveDeviceResponse
        {
            DeviceId = normalizedDeviceId,
            Removed = true,
            WasOnline = wasOnline,
            RevokeAttempted = revokeAttempted,
            RevokeSucceeded = revokeSucceeded,
            RevokeError = revokeError,
            Message = message,
        };
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        RefreshSnapshots();
    }

    private void OnHostLogMessage(object? sender, string message)
    {
        AppendGlobalLog(message);
    }

    private void OnHostCommandProgressChanged(object? sender, DeviceCommandProgressMessage progress)
    {
        if (string.IsNullOrWhiteSpace(progress.DeviceId) || string.IsNullOrWhiteSpace(progress.CommandId))
        {
            return;
        }

        var deviceId = progress.DeviceId.Trim();
        var percent = Math.Clamp(progress.ProgressPercent, 0, 100);
        var status = $"Comandos: {percent}% ({DescribeStage(progress.Stage)})";
        SetCommandStatus(deviceId, inProgress: true, percent, status, progress.Stage, progress.CommandId, null, progress.Message);

        var commandKey = $"{deviceId}::{progress.CommandId}";
        var shouldLog = true;
        lock (gate)
        {
            if (lastProgressPercentByCommandKey.TryGetValue(commandKey, out var previousPercent) && previousPercent >= percent)
            {
                shouldLog = false;
            }
            else
            {
                lastProgressPercentByCommandKey[commandKey] = percent;
            }
        }

        if (shouldLog)
        {
            var progressMessage = !string.IsNullOrWhiteSpace(progress.Message)
                ? $"[{DescribeStage(progress.Stage)}] {progress.Message}"
                : $"[{DescribeStage(progress.Stage)}] Progresso de comando: {percent}%";
            AppendDeviceLog(deviceId, progressMessage);
        }

        realtimePublisher.Publish(new
        {
            type = "command-progress",
            deviceId,
            commandId = progress.CommandId,
            stage = progress.Stage ?? "processing",
            percent,
            status,
            message = progress.Message ?? string.Empty,
        });
    }

    private void RefreshSnapshots()
    {
        IReadOnlyList<DeviceSnapshot> latest;
        try
        {
            latest = serverHost.GetDevicesSnapshot().ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao obter snapshot de dispositivos.");
            return;
        }

        lock (gate)
        {
            RecordDeviceLifecycleEventsUnsafe(snapshots, latest);
            snapshots = latest;
            UpdateTelemetryHistoryUnsafe(latest);
        }
    }

    private void UpdateTelemetryHistoryUnsafe(IReadOnlyList<DeviceSnapshot> latest)
    {
        foreach (var snapshot in latest)
        {
            var deviceId = snapshot.DeviceId;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            var currentSequence = snapshot.TelemetrySequence;
            if (lastTelemetrySequenceByDeviceId.TryGetValue(deviceId, out var previousSequence)
                && previousSequence.HasValue
                && currentSequence.HasValue
                && previousSequence.Value == currentSequence.Value)
            {
                continue;
            }

            lastTelemetrySequenceByDeviceId[deviceId] = currentSequence;

            if (!loopTrendByDeviceId.TryGetValue(deviceId, out var loopTrend))
            {
                loopTrend = new Queue<int>(DashboardTrendSampleCapacity);
                loopTrendByDeviceId[deviceId] = loopTrend;
            }

            if (!espDashLoopHistoryByDeviceId.TryGetValue(deviceId, out var loopHistory))
            {
                loopHistory = new Queue<int>(EspDashHistorySampleCapacity);
                espDashLoopHistoryByDeviceId[deviceId] = loopHistory;
            }

            if (!espDashHeapHistoryByDeviceId.TryGetValue(deviceId, out var heapHistory))
            {
                heapHistory = new Queue<int>(EspDashHistorySampleCapacity);
                espDashHeapHistoryByDeviceId[deviceId] = heapHistory;
            }

            var loopLoad = snapshot.LoopLoadPercent is int rawLoop ? Math.Clamp(rawLoop, 0, 100) : 0;
            var heapKb = snapshot.FreeHeapBytes is long freeHeap
                ? Math.Clamp((int)Math.Round(freeHeap / 1024d), 0, 4096)
                : 0;

            EnqueueWithLimit(loopTrend, loopLoad, DashboardTrendSampleCapacity);
            EnqueueWithLimit(loopHistory, loopLoad, EspDashHistorySampleCapacity);
            EnqueueWithLimit(heapHistory, heapKb, EspDashHistorySampleCapacity);
        }
    }

    private void RecordDeviceLifecycleEventsUnsafe(IReadOnlyList<DeviceSnapshot> previous, IReadOnlyList<DeviceSnapshot> next)
    {
        var previousById = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in previous)
        {
            if (!string.IsNullOrWhiteSpace(item.DeviceId))
            {
                previousById[item.DeviceId] = item;
            }
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.Now;

        foreach (var current in next)
        {
            if (string.IsNullOrWhiteSpace(current.DeviceId))
            {
                continue;
            }

            var deviceId = current.DeviceId;
            seenIds.Add(deviceId);
            previousById.TryGetValue(deviceId, out var previousSnapshot);

            var isOnline = current.Status == DeviceStatus.Online;
            if (previousSnapshot is null)
            {
                if (isOnline)
                {
                    AppendDeviceLogUnsafe(deviceId, "Dispositivo autenticado e online.", now);
                    awaitingFirstTelemetryByDevice.Add(deviceId);
                }
            }
            else
            {
                var wasOnline = previousSnapshot.Status == DeviceStatus.Online;
                if (!wasOnline && isOnline)
                {
                    AppendDeviceLogUnsafe(deviceId, "Dispositivo autenticado e online.", now);
                    if (previousSnapshot.Status == DeviceStatus.Offline)
                    {
                        AppendDeviceLogUnsafe(deviceId, "Dispositivo voltou a aparecer apos ficar offline.", now);
                    }

                    awaitingFirstTelemetryByDevice.Add(deviceId);
                }
                else if (wasOnline && !isOnline)
                {
                    AppendDeviceLogUnsafe(deviceId, "Dispositivo ficou offline.", now);
                    awaitingFirstTelemetryByDevice.Remove(deviceId);
                }
            }

            if (isOnline
                && awaitingFirstTelemetryByDevice.Contains(deviceId)
                && HasFreshTelemetrySample(previousSnapshot, current))
            {
                AppendDeviceLogUnsafe(deviceId, "Primeira telemetria recebida apos reconexao.", now);
                awaitingFirstTelemetryByDevice.Remove(deviceId);
            }

            if (previousSnapshot is null)
            {
                if (!string.IsNullOrWhiteSpace(current.WifiState))
                {
                    AppendDeviceLogUnsafe(deviceId, $"Wi-Fi estado: {current.WifiState}.", now);
                }

                if (current.ProvisioningPortalActive == true)
                {
                    AppendDeviceLogUnsafe(deviceId, "Portal de provisioning ativo.", now);
                }

                if (!string.IsNullOrWhiteSpace(current.LastWifiEvent))
                {
                    AppendDeviceLogUnsafe(deviceId, $"Evento conectividade: {current.LastWifiEvent}.", now);
                }
            }
            else
            {
                if (!string.Equals(previousSnapshot.WifiState, current.WifiState, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(current.WifiState))
                {
                    AppendDeviceLogUnsafe(deviceId, $"Wi-Fi estado: {current.WifiState}.", now);
                }

                if (previousSnapshot.ProvisioningPortalActive != current.ProvisioningPortalActive
                    && current.ProvisioningPortalActive.HasValue)
                {
                    var portalState = current.ProvisioningPortalActive.Value ? "ativo" : "inativo";
                    AppendDeviceLogUnsafe(deviceId, $"Portal de provisioning: {portalState}.", now);
                }

                if (!string.Equals(previousSnapshot.LastWifiEvent, current.LastWifiEvent, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(current.LastWifiEvent))
                {
                    AppendDeviceLogUnsafe(deviceId, $"Evento conectividade: {current.LastWifiEvent}.", now);
                }
            }
        }

        awaitingFirstTelemetryByDevice.RemoveWhere(id => !seenIds.Contains(id));
    }

    private static bool HasFreshTelemetrySample(DeviceSnapshot? previous, DeviceSnapshot current)
    {
        if (!current.LastTelemetryUtc.HasValue)
        {
            return false;
        }

        if (previous is null)
        {
            return true;
        }

        return !previous.LastTelemetryUtc.HasValue || current.LastTelemetryUtc > previous.LastTelemetryUtc;
    }

    private static void EnqueueWithLimit(Queue<int> queue, int value, int limit)
    {
        if (queue.Count >= limit)
        {
            _ = queue.Dequeue();
        }

        queue.Enqueue(value);
    }

    private UiDeviceListItem BuildDeviceListItem(DeviceSnapshot snapshot, LifecyclePresentation presence)
    {
        return new UiDeviceListItem
        {
            DeviceId = snapshot.DeviceId,
            Name = snapshot.Name,
            Profile = snapshot.Profile,
            Status = snapshot.Status.ToString().ToLowerInvariant(),
            IsRegistered = snapshot.IsRegistered,
            LastSeenUtc = snapshot.LastSeenUtc,
            FirstSeenUtc = snapshot.FirstSeenUtc,
            LastTelemetryUtc = snapshot.LastTelemetryUtc,
            LastAuthUtc = snapshot.LastAuthUtc,
            ConfigState = snapshot.ConfigState.ToString().ToLowerInvariant(),
            LastKnownIp = snapshot.LastKnownIp,
            LastKnownRssi = snapshot.LastKnownRssi,
            UptimeSeconds = snapshot.UptimeSeconds,
            LoopLoadPercent = snapshot.LoopLoadPercent,
            FreeHeapBytes = snapshot.FreeHeapBytes,
            LargestHeapBlockBytes = snapshot.LargestHeapBlockBytes,
            PsramAvailable = snapshot.PsramAvailable,
            FreePsramBytes = snapshot.FreePsramBytes,
            LargestPsramBlockBytes = snapshot.LargestPsramBlockBytes,
            WifiConnected = snapshot.WifiConnected,
            WifiState = snapshot.WifiState,
            ProvisioningPortalActive = snapshot.ProvisioningPortalActive,
            AuxLedAvailable = snapshot.AuxLedAvailable,
            TestLedAvailable = snapshot.TestLedAvailable,
            LastWifiEvent = snapshot.LastWifiEvent,
            StreamLastSequence = snapshot.StreamLastSequence,
            StreamFramesReceived = snapshot.StreamFramesReceived,
            StreamFramesApplied = snapshot.StreamFramesApplied,
            StreamSequenceGapCount = snapshot.StreamSequenceGapCount,
            StreamInvalidFrameCount = snapshot.StreamInvalidFrameCount,
            FirmwareVersion = snapshot.FirmwareVersion,
            TelemetrySequence = snapshot.TelemetrySequence,
            BrightnessCap = snapshot.BrightnessCap,
            BrightnessRequested = snapshot.BrightnessRequested,
            BrightnessApplied = snapshot.BrightnessApplied,
            TestLedEnabled = snapshot.TestLedEnabled,
            TestLedDuty = snapshot.TestLedDuty,
            ActiveAppId = snapshot.ActiveAppId,
            ActiveAppName = snapshot.ActiveAppName,
            BoardModel = snapshot.BoardModel,
            PanelType = snapshot.PanelType,
            StatusLine = DevicesPageFormatting.BuildStatusLine(snapshot, presence),
            RegistrationLine = DevicesPageFormatting.BuildRegistrationLine(snapshot, presence),
            AppLabel = DevicesPageFormatting.BuildSelectedAppLabel(snapshot.Status, snapshot.ActiveAppName),
            SignalLabel = DevicesPageFormatting.BuildSelectedSignalLabel(snapshot),
            LifecyclePrimaryState = presence.PrimaryStateLabel,
            LifecycleSecondaryState = presence.SecondaryStateLabel,
            LifecycleLastSeenLabel = presence.LastSeenLabel,
            CanRunCommands = presence.CanRunCommands,
        };
    }

    private UiDeviceDetailsViewModel BuildEmptyDetails(string? deviceId = null)
    {
        return new UiDeviceDetailsViewModel
        {
            DeviceId = deviceId ?? string.Empty,
            Exists = false,
            Command = GetCommandStatusUnsafe(deviceId),
            DeviceLogsPlaceholder = "Selecione um dispositivo para ver logs do dispositivo.",
        };
    }

    private UiCommandStatusViewModel GetCommandStatusUnsafe(string? deviceId)
    {
        if (!string.IsNullOrWhiteSpace(deviceId)
            && commandByDevice.TryGetValue(deviceId.Trim(), out var byDevice))
        {
            return byDevice;
        }

        return lastCommandStatus;
    }

    private IReadOnlyList<string> GetDeviceLogsUnsafe(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Array.Empty<string>();
        }

        return deviceLogsById.TryGetValue(deviceId.Trim(), out var entries)
            ? entries.ToArray()
            : Array.Empty<string>();
    }

    private IReadOnlyList<int> GetSeries(Dictionary<string, Queue<int>> source, string deviceId, int maxCount)
    {
        if (!source.TryGetValue(deviceId, out var queue))
        {
            return Array.Empty<int>();
        }

        if (queue.Count <= maxCount)
        {
            return queue.ToArray();
        }

        return queue.Skip(Math.Max(0, queue.Count - maxCount)).ToArray();
    }

    private void SetCommandStatus(
        string deviceId,
        bool inProgress,
        int percent,
        string status,
        string? stage,
        string? commandId,
        string? errorCode,
        string? message)
    {
        lock (gate)
        {
            var snapshot = new UiCommandStatusViewModel
            {
                InProgress = inProgress,
                Percent = Math.Clamp(percent, 0, 100),
                Status = status,
                Stage = stage,
                DeviceId = deviceId,
                CommandId = commandId,
                ErrorCode = errorCode,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };

            commandByDevice[deviceId] = snapshot;
            lastCommandStatus = snapshot;

            if (!inProgress && !string.IsNullOrWhiteSpace(message))
            {
                AppendDeviceLogUnsafe(deviceId, message, DateTimeOffset.Now);
            }
        }
    }

    private void AppendDeviceLog(string deviceId, string message)
    {
        lock (gate)
        {
            AppendDeviceLogUnsafe(deviceId, message, DateTimeOffset.Now);
        }
    }

    private void AppendDeviceLogUnsafe(string deviceId, string message, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!deviceLogsById.TryGetValue(deviceId, out var entries))
        {
            entries = new List<string>();
            deviceLogsById[deviceId] = entries;
        }

        entries.Add($"[{now:HH:mm:ss}] {message}");
        TrimToLimit(entries, MaxDeviceLogEntries);
    }

    private void AppendGlobalLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (gate)
        {
            globalLogs.Add($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
            TrimToLimit(globalLogs, MaxGlobalLogEntries);
        }
    }

    private static void TrimToLimit(List<string> entries, int limit)
    {
        if (entries.Count <= limit)
        {
            return;
        }

        var removeCount = entries.Count - limit;
        entries.RemoveRange(0, removeCount);
    }

    private static string ResolveServerBaseAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostName();
            if (string.IsNullOrWhiteSpace(host))
            {
                host = "127.0.0.1";
            }

            return $"{host}:5174";
        }
        catch
        {
            return "127.0.0.1:5174";
        }
    }

    private static string DescribeCommand(DeviceCommandType commandType)
        => commandType switch
        {
            DeviceCommandType.EnterProvisioning => "entrar em provisioning",
            DeviceCommandType.RevokeAndRestart => "revogar/reiniciar",
            DeviceCommandType.TestLed => "controlar LED auxiliar",
            DeviceCommandType.InstallApp => "instalar app",
            DeviceCommandType.ActivateApp => "ativar app",
            DeviceCommandType.SetAppConfig => "configurar app",
            DeviceCommandType.SetBrightness => "ajustar brilho do painel",
            _ => "comando",
        };

    private static string DescribeStage(string? stage)
    {
        return string.IsNullOrWhiteSpace(stage) ? "processando" : stage;
    }

    private static string BuildFinalCommandStatus(CommandDispatchResult result)
    {
        if (result.Success)
        {
            return "Comandos: concluido";
        }

        return result.ErrorCode switch
        {
            "timeout" => "Comandos: sem resposta do dispositivo",
            "device_offline" => "Comandos: dispositivo offline",
            "send_error" => "Comandos: erro de rede",
            _ => "Comandos: erro",
        };
    }

    private static string BuildResultLogMessage(CommandDispatchResult result)
    {
        if (result.Success)
        {
            return $"Comando concluido com sucesso ({result.Stage ?? "ok"}) para {result.DeviceId}.";
        }

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            return $"Comando falhou para {result.DeviceId}: {result.Message}";
        }

        return $"Comando falhou para {result.DeviceId}.";
    }
}
