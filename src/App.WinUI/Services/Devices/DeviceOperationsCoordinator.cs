using App.WinUI.Services;
using Device.Protocol.Models;
using System.Globalization;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
internal sealed class DeviceOperationsCoordinator : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private const int MaxLogEntries = 600;
    private const int MaxDeviceLogEntries = 100;
    private const int SafeBrightnessMin = 30;
    private const int SafeBrightnessMax = 160;

    private readonly IDeviceOperationsRuntime integration;
    private readonly SettingsRepository? settingsRepository;
    private readonly AppSettingsDomainService? settingsDomainService;
    private readonly object gate = new();
    private readonly List<string> logs = new();
    private readonly Dictionary<string, List<string>> deviceLogsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> awaitingFirstTelemetryByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DeviceSnapshot> devicesSnapshot = new();
    private readonly Dictionary<string, CommandExecutionContext> commandByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer refreshTimer;
    private DeviceLifecycleThresholds lifecycleThresholds = DeviceLifecycleThresholds.Default;
    private bool lifecycleThresholdsLoaded;

    private int refreshInFlight;
    private bool refreshActive;
    private bool commandInProgress;
    private int commandPercent;
    private string commandStatus = "Comandos: pronto";
    private string? lastCommandDeviceId;
    private DateTimeOffset lastRefreshUtc;
    private bool disposed;

    public DeviceOperationsCoordinator(DeviceIntegrationService integration, SettingsRepository settingsRepository, AppSettingsDomainService settingsDomainService)
        : this(new DeviceOperationsRuntime(integration), settingsRepository, settingsDomainService)
    {
    }

    internal DeviceOperationsCoordinator(
        IDeviceOperationsRuntime integration,
        SettingsRepository? settingsRepository,
        AppSettingsDomainService? settingsDomainService)
    {
        this.integration = integration;
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;
        refreshTimer = new Timer(OnRefreshTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (settingsRepository is null || settingsDomainService is null)
        {
            lifecycleThresholds = DeviceLifecycleThresholds.Default;
            lifecycleThresholdsLoaded = true;
        }

        integration.DevicesChanged += OnDevicesChanged;
        integration.LogMessage += OnLogMessage;
        integration.CommandProgressChanged += OnHostCommandProgressChanged;
    }

    public event EventHandler? StateChanged;

    public event EventHandler? DeviceListChanged;

    public DeviceOperationsState GetStateSnapshot()
    {
        lock (gate)
        {
            var commandSnapshot = new Dictionary<string, DeviceCommandExecutionState>(StringComparer.OrdinalIgnoreCase);
            foreach (var (deviceId, context) in commandByDevice)
            {
                commandSnapshot[deviceId] = context.ToSnapshot();
            }

            return new DeviceOperationsState
            {
                CommandInProgress = commandInProgress,
                CommandPercent = commandPercent,
                CommandStatus = commandStatus,
                LastCommandDeviceId = lastCommandDeviceId,
                CommandByDevice = commandSnapshot,
                DeviceListSnapshot = devicesSnapshot.ToArray(),
                LastRefreshUtc = lastRefreshUtc,
                ServerBaseAddress = integration.GetServerBaseAddress(),
                Logs = logs.ToArray(),
            };
        }
    }

    public IReadOnlyList<string> GetDeviceLogs(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Array.Empty<string>();
        }

        lock (gate)
        {
            if (!deviceLogsById.TryGetValue(deviceId.Trim(), out var entries) || entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            return entries.ToArray();
        }
    }

    public void SetDevicesPageVisible(bool visible)
    {
        lock (gate)
        {
            refreshActive = visible;
        }

        if (visible)
        {
            refreshTimer.Change(TimeSpan.Zero, RefreshInterval);
            _ = RefreshDevicesAsync(forcePublish: true);
            return;
        }

        refreshTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void RequestRefresh()
    {
        _ = RefreshDevicesAsync(forcePublish: true);
    }

    public string GetServerBaseAddress() => integration.GetServerBaseAddress();

    public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
    {
        var pairing = integration.CreatePairingCode(ttl);
        AppendLog($"Codigo de pareamento: {pairing.Code} (expira {pairing.ExpiresAtUtc:HH:mm:ss} UTC)");
        return pairing;
    }

    public bool RemoveDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        var normalizedDeviceId = deviceId.Trim();
        var removed = integration.RemoveDevice(normalizedDeviceId);
        if (!removed)
        {
            return false;
        }

        AppendDeviceLog(normalizedDeviceId, "Dispositivo removido do registro local.");
        AppendLog($"Dispositivo removido do registro local: {normalizedDeviceId}");
        RequestRefresh();
        return true;
    }

    // DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
    public Task<CommandDispatchResult> RunCommandAsync(
        string deviceId,
        DeviceCommandType commandType,
        CancellationToken cancellationToken = default)
    {
        return RunCommandCoreAsync(deviceId, commandType, parameters: null, cancellationToken);
    }

    public Task<CommandDispatchResult> RunCommandAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken = default)
    {
        return RunCommandCoreAsync(deviceId, commandType, parameters, cancellationToken);
    }

    public Task<CommandDispatchResult> InstallAppAsync(string deviceId, DeviceAppCommandPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var parameters = payload.ToParameters();
        return RunCommandAsync(deviceId, DeviceCommandType.InstallApp, parameters, cancellationToken);
    }

    public Task<CommandDispatchResult> ActivateAppAsync(string deviceId, string appId, string? appName = null, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["appId"] = appId,
        };

        if (!string.IsNullOrWhiteSpace(appName))
        {
            parameters["displayName"] = appName;
        }

        return RunCommandAsync(deviceId, DeviceCommandType.ActivateApp, parameters, cancellationToken);
    }

    public Task<CommandDispatchResult> SetAppConfigAsync(string deviceId, string appId, string configJson, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["appId"] = appId,
            ["configJson"] = configJson,
        };

        return RunCommandAsync(deviceId, DeviceCommandType.SetAppConfig, parameters, cancellationToken);
    }

    public Task<CommandDispatchResult> SetBrightnessAsync(string deviceId, int brightness, CancellationToken cancellationToken = default)
    {
        var normalized = Math.Clamp(brightness, SafeBrightnessMin, SafeBrightnessMax);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brightness"] = normalized.ToString(CultureInfo.InvariantCulture),
        };

        return RunCommandAsync(deviceId, DeviceCommandType.SetBrightness, parameters, cancellationToken);
    }

    public Task<CommandDispatchResult> TriggerTestLedAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return RunCommandAsync(deviceId, DeviceCommandType.TestLed, parameters: null, cancellationToken);
    }

    public Task<CommandDispatchResult> SetTestLedEnabledAsync(string deviceId, bool enabled, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["enabled"] = enabled ? "true" : "false",
        };

        return RunCommandAsync(deviceId, DeviceCommandType.TestLed, parameters, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        refreshTimer.Dispose();
        integration.DevicesChanged -= OnDevicesChanged;
        integration.LogMessage -= OnLogMessage;
        integration.CommandProgressChanged -= OnHostCommandProgressChanged;
    }

    private async Task<CommandDispatchResult> RunCommandCoreAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new CommandDispatchResult
            {
                DeviceId = string.Empty,
                Accepted = false,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "invalid",
                Message = "Nenhum dispositivo selecionado.",
                ErrorCode = "invalid_device",
            };
        }

        var normalizedDeviceId = deviceId.Trim();
        var operationLabel = DescribeCommand(commandType);

        lock (gate)
        {
            if (commandByDevice.TryGetValue(normalizedDeviceId, out var runningCommand))
            {
                return new CommandDispatchResult
                {
                    DeviceId = normalizedDeviceId,
                    Accepted = false,
                    Completed = true,
                    Success = false,
                    ProgressPercent = Math.Clamp(runningCommand.Percent, 0, 100),
                    Stage = "busy",
                    Message = "Ja existe uma operacao em andamento para este dispositivo.",
                    ErrorCode = "busy",
                };
            }

            commandByDevice[normalizedDeviceId] = new CommandExecutionContext
            {
                DeviceId = normalizedDeviceId,
                Percent = 0,
                Status = $"Comandos: 0% ({operationLabel})",
                Stage = "queued",
            };

            commandInProgress = commandByDevice.Count > 0;
            commandPercent = 0;
            commandStatus = $"Comandos: 0% ({operationLabel})";
            lastCommandDeviceId = normalizedDeviceId;
        }

        RaiseStateChanged();
        AppendDeviceLog(normalizedDeviceId, $"Comando iniciado ({operationLabel}).");
        AppendLog($"Comando iniciado ({operationLabel}) para {normalizedDeviceId}.");

        CommandDispatchResult result;
        try
        {
            result = await integration
                .SendCommandTrackedAsync(normalizedDeviceId, commandType, parameters, CommandTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new CommandDispatchResult
            {
                DeviceId = normalizedDeviceId,
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
            result = new CommandDispatchResult
            {
                DeviceId = normalizedDeviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = 0,
                Stage = "error",
                Message = ex.Message,
                ErrorCode = "exception",
            };
        }

        lock (gate)
        {
            var finalPercent = Math.Clamp(result.ProgressPercent, 0, 100);
            if (commandByDevice.TryGetValue(normalizedDeviceId, out var context))
            {
                finalPercent = Math.Clamp(Math.Max(context.Percent, finalPercent), 0, 100);
                context.Percent = finalPercent;
                context.Status = BuildFinalCommandStatus(result);
                context.Stage = result.Stage;
                context.ErrorCode = result.ErrorCode;

                if (!string.IsNullOrWhiteSpace(result.CommandId))
                {
                    context.CommandId = result.CommandId;
                }

                commandByDevice.Remove(normalizedDeviceId);
            }

            commandInProgress = commandByDevice.Count > 0;
            commandPercent = finalPercent;
            commandStatus = BuildFinalCommandStatus(result);
            lastCommandDeviceId = normalizedDeviceId;
        }

        RaiseStateChanged();
        AppendDeviceLog(normalizedDeviceId, BuildResultLogMessage(result));
        AppendLog(BuildResultLogMessage(result));
        return result;
    }

    private void OnRefreshTimerTick(object? _)
    {
        lock (gate)
        {
            if (!refreshActive)
            {
                return;
            }
        }

        _ = RefreshDevicesAsync(forcePublish: false);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        _ = RefreshDevicesAsync(forcePublish: false);
    }

    private void OnLogMessage(object? sender, string message)
    {
        AppendLog(message);
    }

    private void OnHostCommandProgressChanged(object? sender, DeviceCommandProgressMessage progress)
    {
        if (string.IsNullOrWhiteSpace(progress.DeviceId) || string.IsNullOrWhiteSpace(progress.CommandId))
        {
            return;
        }

        var normalizedDeviceId = progress.DeviceId.Trim();
        var shouldRaise = false;
        var progressPercent = 0;

        lock (gate)
        {
            if (!commandByDevice.TryGetValue(normalizedDeviceId, out var context))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(context.CommandId))
            {
                context.CommandId = progress.CommandId;
            }

            if (!string.Equals(context.CommandId, progress.CommandId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            context.Percent = Math.Clamp(Math.Max(context.Percent, progress.ProgressPercent), 0, 100);
            context.Status = BuildLiveCommandStatus(progress);
            context.Stage = progress.Stage;
            progressPercent = context.Percent;
            shouldRaise = true;

            if (string.Equals(lastCommandDeviceId, normalizedDeviceId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(lastCommandDeviceId))
            {
                commandPercent = context.Percent;
                commandStatus = context.Status;
                lastCommandDeviceId = normalizedDeviceId;
            }
        }

        if (shouldRaise)
        {
            var progressMessage = !string.IsNullOrWhiteSpace(progress.Message)
                ? $"[{DescribeStage(progress.Stage)}] {progress.Message}"
                : $"[{DescribeStage(progress.Stage)}] Progresso de comando: {progressPercent}%";

            AppendDeviceLog(normalizedDeviceId, progressMessage);
            RaiseStateChanged();
            AppendLog(progressMessage);
        }
    }

    private async Task RefreshDevicesAsync(bool forcePublish)
    {
        if (Interlocked.CompareExchange(ref refreshInFlight, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await EnsureLifecycleThresholdsLoadedAsync().ConfigureAwait(false);

            // DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
            var nextSnapshot = DeviceListVisibilityPolicy.BuildVisibleList(
                integration.GetDevices(),
                lifecycleThresholds,
                DateTimeOffset.UtcNow);

            var changed = false;

            lock (gate)
            {
                lastRefreshUtc = DateTimeOffset.UtcNow;
                var nonOnlinePresent = nextSnapshot.Any(static d => d.Status != DeviceStatus.Online);

                if (forcePublish || nonOnlinePresent || !AreSnapshotsEquivalent(devicesSnapshot, nextSnapshot))
                {
                    RecordDeviceLifecycleEventsLocked(devicesSnapshot, nextSnapshot);
                    devicesSnapshot.Clear();
                    devicesSnapshot.AddRange(nextSnapshot);
                    changed = true;
                }
            }

            if (changed)
            {
                DeviceListChanged?.Invoke(this, EventArgs.Empty);
            }

            if (changed || forcePublish)
            {
                RaiseStateChanged();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Falha ao atualizar lista de dispositivos: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref refreshInFlight, 0);
        }
    }

    private async Task EnsureLifecycleThresholdsLoadedAsync()
    {
        if (lifecycleThresholdsLoaded)
        {
            return;
        }

        var repository = settingsRepository;
        var domainService = settingsDomainService;
        if (repository is null || domainService is null)
        {
            lifecycleThresholds = DeviceLifecycleThresholds.Default;
            lifecycleThresholdsLoaded = true;
            return;
        }

        try
        {
            var loadedSettings = await repository.LoadAsync().ConfigureAwait(false);
            var settings = domainService.Migrate(loadedSettings);
            lifecycleThresholds = DeviceLifecycleThresholds.FromSettings(settings);
        }
        catch
        {
            lifecycleThresholds = DeviceLifecycleThresholds.Default;
        }
        finally
        {
            lifecycleThresholdsLoaded = true;
        }
    }

    private static bool AreSnapshotsEquivalent(List<DeviceSnapshot> current, DeviceSnapshot[] next)
    {
        if (current.Count != next.Length)
        {
            return false;
        }

        for (var i = 0; i < current.Count; i++)
        {
            var a = current[i];
            var b = next[i];
            if (!string.Equals(a.DeviceId, b.DeviceId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                || a.Status != b.Status
                || !string.Equals(a.Profile, b.Profile, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.FirmwareVersion, b.FirmwareVersion, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.LastKnownIp, b.LastKnownIp, StringComparison.OrdinalIgnoreCase)
                || a.LastKnownRssi != b.LastKnownRssi
                || a.UptimeSeconds != b.UptimeSeconds
                || a.LoopLoadPercent != b.LoopLoadPercent
                || a.FreeHeapBytes != b.FreeHeapBytes
                || a.LargestHeapBlockBytes != b.LargestHeapBlockBytes
                || a.PsramAvailable != b.PsramAvailable
                || a.FreePsramBytes != b.FreePsramBytes
                || a.LargestPsramBlockBytes != b.LargestPsramBlockBytes
                || a.WifiConnected != b.WifiConnected
                || !string.Equals(a.WifiState, b.WifiState, StringComparison.OrdinalIgnoreCase)
                || a.ProvisioningPortalActive != b.ProvisioningPortalActive
                || a.AuxLedAvailable != b.AuxLedAvailable
                || a.TestLedAvailable != b.TestLedAvailable
                || !string.Equals(a.LastWifiEvent, b.LastWifiEvent, StringComparison.OrdinalIgnoreCase)
                || a.TelemetrySequence != b.TelemetrySequence
                || a.BrightnessCap != b.BrightnessCap
                || a.BrightnessRequested != b.BrightnessRequested
                || a.BrightnessApplied != b.BrightnessApplied
                || a.TestLedEnabled != b.TestLedEnabled
                || a.TestLedDuty != b.TestLedDuty
                || !string.Equals(a.ActiveAppId, b.ActiveAppId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.ActiveAppName, b.ActiveAppName, StringComparison.Ordinal)
                || !string.Equals(a.BoardModel, b.BoardModel, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.PanelType, b.PanelType, StringComparison.OrdinalIgnoreCase)
                || a.IsRegistered != b.IsRegistered
                || a.FirstSeenUtc != b.FirstSeenUtc
                || a.LastTelemetryUtc != b.LastTelemetryUtc
                || a.LastAuthUtc != b.LastAuthUtc
                || a.ConfigState != b.ConfigState)
            {
                return false;
            }
        }

        return true;
    }

    private void RecordDeviceLifecycleEventsLocked(IReadOnlyList<DeviceSnapshot> previous, IReadOnlyList<DeviceSnapshot> next)
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
                    AppendDeviceLogLocked(deviceId, "Dispositivo autenticado e online.", now);
                    awaitingFirstTelemetryByDevice.Add(deviceId);
                }
            }
            else
            {
                var wasOnline = previousSnapshot.Status == DeviceStatus.Online;
                if (!wasOnline && isOnline)
                {
                    AppendDeviceLogLocked(deviceId, "Dispositivo autenticado e online.", now);
                    if (previousSnapshot.Status == DeviceStatus.Offline)
                    {
                        AppendDeviceLogLocked(deviceId, "Dispositivo voltou a aparecer apos ficar offline.", now);
                    }

                    awaitingFirstTelemetryByDevice.Add(deviceId);
                }
                else if (wasOnline && !isOnline)
                {
                    AppendDeviceLogLocked(deviceId, "Dispositivo ficou offline.", now);
                    awaitingFirstTelemetryByDevice.Remove(deviceId);
                }
            }

            if (isOnline
                && awaitingFirstTelemetryByDevice.Contains(deviceId)
                && HasFreshTelemetrySample(previousSnapshot, current))
            {
                AppendDeviceLogLocked(deviceId, "Primeira telemetria recebida apos reconexao.", now);
                awaitingFirstTelemetryByDevice.Remove(deviceId);
            }

            if (previousSnapshot is null)
            {
                if (!string.IsNullOrWhiteSpace(current.WifiState))
                {
                    AppendDeviceLogLocked(deviceId, $"Wi-Fi estado: {current.WifiState}.", now);
                }

                if (current.ProvisioningPortalActive == true)
                {
                    AppendDeviceLogLocked(deviceId, "Portal de provisioning ativo.", now);
                }

                if (!string.IsNullOrWhiteSpace(current.LastWifiEvent))
                {
                    AppendDeviceLogLocked(deviceId, $"Evento conectividade: {current.LastWifiEvent}.", now);
                }
            }
            else
            {
                if (!string.Equals(previousSnapshot.WifiState, current.WifiState, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(current.WifiState))
                {
                    AppendDeviceLogLocked(deviceId, $"Wi-Fi estado: {current.WifiState}.", now);
                }

                if (previousSnapshot.ProvisioningPortalActive != current.ProvisioningPortalActive
                    && current.ProvisioningPortalActive.HasValue)
                {
                    var portalState = current.ProvisioningPortalActive.Value ? "ativo" : "inativo";
                    AppendDeviceLogLocked(deviceId, $"Portal de provisioning: {portalState}.", now);
                }

                if (!string.Equals(previousSnapshot.LastWifiEvent, current.LastWifiEvent, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(current.LastWifiEvent))
                {
                    AppendDeviceLogLocked(deviceId, $"Evento conectividade: {current.LastWifiEvent}.", now);
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

    private static string BuildLiveCommandStatus(DeviceCommandProgressMessage progress)
    {
        var stage = DescribeStage(progress.Stage);
        return $"Comandos: {Math.Clamp(progress.ProgressPercent, 0, 100)}% ({stage})";
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
        if (string.IsNullOrWhiteSpace(stage))
        {
            return "processando";
        }

        return stage;
    }

    private void AppendDeviceLog(string deviceId, string message)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (gate)
        {
            AppendDeviceLogLocked(deviceId.Trim(), message, DateTimeOffset.Now);
        }

        RaiseStateChanged();
    }

    private void AppendDeviceLogLocked(string deviceId, string message, DateTimeOffset now)
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

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (gate)
        {
            var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {message}";
            logs.Add(line);
            TrimToLimit(logs, MaxLogEntries);
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
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

    private sealed class CommandExecutionContext
    {
        public string DeviceId { get; init; } = string.Empty;

        public int Percent { get; set; }

        public string Status { get; set; } = "Comandos: pronto";

        public string? Stage { get; set; }

        public string? CommandId { get; set; }

        public string? ErrorCode { get; set; }

        public DeviceCommandExecutionState ToSnapshot()
        {
            return new DeviceCommandExecutionState
            {
                InProgress = true,
                Percent = Math.Clamp(Percent, 0, 100),
                Status = Status,
                Stage = Stage,
                CommandId = CommandId,
                ErrorCode = ErrorCode,
            };
        }
    }
}

internal interface IDeviceOperationsRuntime
{
    event EventHandler? DevicesChanged;

    event EventHandler<string>? LogMessage;

    event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

    string GetServerBaseAddress();

    PairingCodeInfo CreatePairingCode(TimeSpan ttl);

    bool RemoveDevice(string deviceId);

    IReadOnlyList<DeviceSnapshot> GetDevices();

    Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed class DeviceOperationsRuntime : IDeviceOperationsRuntime
{
    private readonly DeviceIntegrationService integration;

    public DeviceOperationsRuntime(DeviceIntegrationService integration)
    {
        this.integration = integration;
    }

    public event EventHandler? DevicesChanged
    {
        add => integration.DevicesChanged += value;
        remove => integration.DevicesChanged -= value;
    }

    public event EventHandler<string>? LogMessage
    {
        add => integration.LogMessage += value;
        remove => integration.LogMessage -= value;
    }

    public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
    {
        add => integration.Host.CommandProgressChanged += value;
        remove => integration.Host.CommandProgressChanged -= value;
    }

    public string GetServerBaseAddress() => integration.GetServerBaseAddress();

    public PairingCodeInfo CreatePairingCode(TimeSpan ttl) => integration.CreatePairingCode(ttl);

    public bool RemoveDevice(string deviceId) => integration.RemoveDevice(deviceId);

    public IReadOnlyList<DeviceSnapshot> GetDevices() => integration.GetDevices();

    public Task<CommandDispatchResult> SendCommandTrackedAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return integration.Host.SendCommandTrackedAsync(deviceId, commandType, parameters, timeout, cancellationToken);
    }
}

