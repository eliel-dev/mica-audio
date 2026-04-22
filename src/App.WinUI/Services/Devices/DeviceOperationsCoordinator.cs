using System.Diagnostics;
using System.Globalization;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services.Logging;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#modulo-deviceoperationscoordinator
// DOCS: docs/handoffs/2026-04-22-device-server-client-boundary.md
internal sealed partial class DeviceOperationsCoordinator : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FirmwareUpdateTimeout = TimeSpan.FromMinutes(5);
    private const int MaxLogEntries = 600;
    private const int MaxDeviceLogEntries = 100;
    private const int SafeBrightnessMin = 30;
    private const int SafeBrightnessMax = 160;

    private readonly IDeviceOperationsRuntime integration;
    private readonly DeviceLogBook logBook = new(MaxLogEntries, MaxDeviceLogEntries);
    private readonly DeviceTelemetryHistoryBook telemetryHistoryBook = new(maxSamples: 60);
    private readonly DeviceCommandTracker commandTracker = new();
    private readonly DeviceRefreshCoordinator refreshCoordinator = new();
    private readonly DeviceLifecycleThresholdProvider lifecycleThresholdProvider;
    private readonly DeviceCommandDispatcher commandDispatcher;
    private readonly ILogger<DeviceOperationsCoordinator>? logger;
    private readonly Timer refreshTimer;
    private bool disposed;

    public DeviceOperationsCoordinator(
        IDeviceServerClient integration,
        SettingsRepository? settingsRepository,
        AppSettingsDomainService? settingsDomainService,
        ILogger<DeviceOperationsCoordinator>? logger = null)
        : this(new DeviceOperationsRuntime(integration), settingsRepository, settingsDomainService, logger)
    {
    }

    internal DeviceOperationsCoordinator(
        IDeviceOperationsRuntime integration,
        SettingsRepository? settingsRepository,
        AppSettingsDomainService? settingsDomainService,
        ILogger<DeviceOperationsCoordinator>? logger = null)
    {
        this.integration = integration;
        this.logger = logger;
        lifecycleThresholdProvider = new DeviceLifecycleThresholdProvider(settingsRepository, settingsDomainService);
        commandDispatcher = new DeviceCommandDispatcher(integration, CommandTimeout);
        refreshTimer = new Timer(OnRefreshTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        integration.DevicesChanged += OnDevicesChanged;
        integration.LogMessage += OnLogMessage;
        integration.DeviceLogReceived += OnDeviceLogReceived;
        integration.CommandProgressChanged += OnHostCommandProgressChanged;
    }

    public event EventHandler? StateChanged;

    public event EventHandler? DeviceListChanged;

    internal AppLogStore? CentralLogStore { get; set; }

    public DeviceOperationsState GetStateSnapshot()
    {
        var commandSnapshot = commandTracker.GetSnapshot();
        var refreshSnapshot = refreshCoordinator.GetSnapshot();

        return new DeviceOperationsState
        {
            CommandInProgress = commandSnapshot.CommandInProgress,
            CommandPercent = commandSnapshot.CommandPercent,
            CommandStatus = commandSnapshot.CommandStatus,
            LastCommandDeviceId = commandSnapshot.LastCommandDeviceId,
            CommandByDevice = commandSnapshot.CommandByDevice,
            DeviceListSnapshot = refreshSnapshot.Devices,
            LastRefreshUtc = refreshSnapshot.LastRefreshUtc,
            ServerBaseAddress = integration.GetServerBaseAddress(),
            Logs = logBook.GetGlobalLogs(),
        };
    }

    public IReadOnlyList<DeviceLogEntry> GetDeviceLogs(string deviceId)
        => logBook.GetDeviceLogs(deviceId);

    public DeviceTelemetryHistorySnapshot GetDeviceTelemetryHistory(string deviceId)
        => telemetryHistoryBook.GetHistory(deviceId);

    public void SetDevicesPageVisible(bool visible)
    {
        refreshCoordinator.SetVisible(visible);

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

    internal PairingCodeInfo CreateHiddenPairingCode(TimeSpan ttl)
    {
        return integration.CreatePairingCode(ttl);
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

        logBook.RemoveDevice(normalizedDeviceId);
        telemetryHistoryBook.RemoveDevice(normalizedDeviceId);
        AppendLog($"Dispositivo removido do registro local: {normalizedDeviceId}", category: "device", code: "removed", source: DeviceLogSource.App);
        RequestRefresh();
        return true;
    }

    // DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
    public Task<CommandDispatchResult> RunCommandAsync(
        string deviceId,
        DeviceCommandType commandType,
        CancellationToken cancellationToken = default)
    {
        return RunCommandCoreAsync(deviceId, commandType, parameters: null, timeoutOverride: null, cancellationToken);
    }

    public Task<CommandDispatchResult> RunCommandAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken = default)
    {
        return RunCommandCoreAsync(deviceId, commandType, parameters, timeoutOverride: null, cancellationToken);
    }

    public Task<CommandDispatchResult> InstallAppAsync(string deviceId, DeviceAppCommandPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return RunCommandAsync(deviceId, DeviceCommandType.InstallApp, payload.ToParameters(), cancellationToken);
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
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brightness"] = Math.Clamp(brightness, SafeBrightnessMin, SafeBrightnessMax).ToString(CultureInfo.InvariantCulture),
        };

        return RunCommandAsync(deviceId, DeviceCommandType.SetBrightness, parameters, cancellationToken);
    }

    public Task<CommandDispatchResult> TriggerTestLedAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return RunCommandAsync(deviceId, DeviceCommandType.TestLed, parameters: null, cancellationToken);
    }

    public Task<CommandDispatchResult> UpdateFirmwareAsync(
        string deviceId,
        string targetVersion,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(targetVersion))
        {
            parameters["version"] = targetVersion.Trim();
        }

        return RunCommandCoreAsync(deviceId, DeviceCommandType.UpdateFirmware, parameters, FirmwareUpdateTimeout, cancellationToken);
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
        lifecycleThresholdProvider.Dispose();
        integration.DevicesChanged -= OnDevicesChanged;
        integration.LogMessage -= OnLogMessage;
        integration.DeviceLogReceived -= OnDeviceLogReceived;
        integration.CommandProgressChanged -= OnHostCommandProgressChanged;
    }

    private async Task<CommandDispatchResult> RunCommandCoreAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        TimeSpan? timeoutOverride,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return DeviceCommandDispatcher.CreateInvalidDeviceResult();
        }

        var normalizedDeviceId = deviceId.Trim();
        var operationLabel = DeviceOperationsText.DescribeCommand(commandType);
        using var activity = AppObservability.StartActivity("device.command", AppObservability.DeviceIntegrationComponent);
        activity?.SetTag(AppObservability.DeviceIdKey, normalizedDeviceId);
        activity?.SetTag("commandType", commandType.ToString());
        if (TryGetAppId(parameters, out var appId))
        {
            activity?.SetTag(AppObservability.AppIdKey, appId);
        }

        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.DeviceIntegrationComponent),
            (AppObservability.MicaComponentKey, AppObservability.DeviceIntegrationComponent),
            (AppObservability.OperationKey, "device.command"),
            (AppObservability.DeviceIdKey, normalizedDeviceId),
            ("commandType", commandType.ToString()),
            (AppObservability.AppIdKey, appId));

        if (!commandTracker.TryQueue(normalizedDeviceId, operationLabel, out var busyResult))
        {
            activity?.SetStatus(ActivityStatusCode.Error, busyResult?.ErrorCode ?? "busy");
            if (logger is not null)
            {
                LogDeviceCommandBusy(logger, commandType, normalizedDeviceId);
            }

            return busyResult!;
        }

        RaiseStateChanged();
        AppendDeviceLog(normalizedDeviceId, $"Comando iniciado ({operationLabel}).", category: "command", code: "started");
        AppendLog($"Comando iniciado ({operationLabel}) para {normalizedDeviceId}.", category: "command", code: "started", source: DeviceLogSource.App);
        if (logger is not null)
        {
            LogDeviceCommandStarted(logger, commandType, normalizedDeviceId);
        }

        var result = await commandDispatcher
            .DispatchAsync(normalizedDeviceId, commandType, parameters, timeoutOverride, cancellationToken)
            .ConfigureAwait(false);

        activity?.SetTag(AppObservability.CommandIdKey, result.CommandId);
        activity?.SetTag("command.stage", result.Stage);
        activity?.SetTag("command.success", result.Success);
        if (!result.Success)
        {
            activity?.SetStatus(ActivityStatusCode.Error, result.ErrorCode ?? result.Stage);
            if (logger is not null)
            {
                LogDeviceCommandFailed(logger, commandType, normalizedDeviceId, result.Stage ?? "unknown", result.ErrorCode ?? "none");
            }
        }
        else
        {
            if (logger is not null)
            {
                LogDeviceCommandSucceeded(logger, commandType, normalizedDeviceId, result.CommandId ?? string.Empty);
            }
        }

        commandTracker.Complete(normalizedDeviceId, result);
        RaiseStateChanged();

        var resultMessage = DeviceOperationsText.BuildResultLogMessage(result);
        var resultSeverity = result.Success ? DeviceLogSeverity.Info : DeviceLogSeverity.Warning;
        AppendDeviceLog(normalizedDeviceId, resultMessage, severity: resultSeverity, category: "command", code: result.Stage);
        AppendLog(resultMessage, severity: resultSeverity, category: "command", code: result.Stage, source: DeviceLogSource.App);
        return result;
    }

    private static bool TryGetAppId(IReadOnlyDictionary<string, string>? parameters, out string? appId)
    {
        appId = null;
        if (parameters is null)
        {
            return false;
        }

        if (!parameters.TryGetValue("appId", out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        appId = rawValue.Trim();
        return true;
    }

    private void OnRefreshTimerTick(object? _)
    {
        if (!refreshCoordinator.IsVisible())
        {
            return;
        }

        _ = RefreshDevicesAsync(forcePublish: false);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        _ = RefreshDevicesAsync(forcePublish: false);
    }

    private void OnLogMessage(object? sender, string message)
    {
        AppendLog(message, category: "server", source: DeviceLogSource.Server);
    }

    private void OnDeviceLogReceived(object? sender, DeviceLogMessage log)
    {
        if (logBook.AppendDeviceFirmware(log))
        {
            if (log.Level.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                CentralLogStore?.Append(LogCategory.Devices, LogSeverity.Error, log.Message, log.DeviceId);
            }

            RaiseStateChanged();
        }
    }

    private void OnHostCommandProgressChanged(object? sender, DeviceCommandProgressMessage progress)
    {
        if (!commandTracker.ApplyProgress(progress, out var normalizedDeviceId, out var progressMessage))
        {
            return;
        }

        var severity = progress.Success is false ? DeviceLogSeverity.Warning : DeviceLogSeverity.Info;
        AppendDeviceLog(normalizedDeviceId, progressMessage, severity: severity, category: "command", code: progress.Stage);
        RaiseStateChanged();
        AppendLog(progressMessage, severity: severity, category: "command", code: progress.Stage, source: DeviceLogSource.App);
    }

    private async Task RefreshDevicesAsync(bool forcePublish)
    {
        if (!refreshCoordinator.TryEnterRefresh())
        {
            return;
        }

        try
        {
            var lifecycleThresholds = await lifecycleThresholdProvider.EnsureLoadedAsync().ConfigureAwait(false);
            var nextSnapshot = DeviceListVisibilityPolicy.BuildVisibleList(
                integration.GetDevices(),
                lifecycleThresholds,
                DateTimeOffset.UtcNow);

            var update = refreshCoordinator.Apply(nextSnapshot, forcePublish, DateTimeOffset.UtcNow);
            if (update.Changed)
            {
                logBook.RecordLifecycleEvents(update.PreviousSnapshot, update.CurrentSnapshot, DateTimeOffset.Now);
                telemetryHistoryBook.RecordSnapshots(update.CurrentSnapshot);
                DeviceListChanged?.Invoke(this, EventArgs.Empty);
            }

            if (update.Changed || forcePublish)
            {
                RaiseStateChanged();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Falha ao atualizar lista de dispositivos: {ex.Message}", severity: DeviceLogSeverity.Error, category: "refresh", code: "refresh_failed", source: DeviceLogSource.App);
        }
        finally
        {
            refreshCoordinator.ExitRefresh();
        }
    }

    private void AppendDeviceLog(
        string deviceId,
        string message,
        DeviceLogSeverity severity = DeviceLogSeverity.Info,
        string category = "app",
        string? code = null,
        DeviceLogSource source = DeviceLogSource.App)
    {
        if (logBook.AppendDevice(deviceId, message, severity, category, code, source))
        {
            RaiseStateChanged();
        }
    }

    private void AppendLog(
        string message,
        DeviceLogSeverity severity = DeviceLogSeverity.Info,
        string category = "server",
        string? code = null,
        DeviceLogSource source = DeviceLogSource.Server)
    {
        if (logBook.AppendGlobal(message, severity, category, code, source))
        {
            CentralLogStore?.Append(LogCategory.Devices, MapSeverity(severity), message);
            RaiseStateChanged();
        }
    }

    private static LogSeverity MapSeverity(DeviceLogSeverity severity)
        => severity switch
        {
            DeviceLogSeverity.Warning => LogSeverity.Warning,
            DeviceLogSeverity.Error => LogSeverity.Error,
            _ => LogSeverity.Info,
        };

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    [LoggerMessage(EventId = 1600, Level = LogLevel.Warning, Message = "Comando {CommandType} rejeitado para {DeviceId} porque ja existe outra operacao em andamento.")]
    private static partial void LogDeviceCommandBusy(ILogger logger, DeviceCommandType commandType, string deviceId);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Information, Message = "Comando {CommandType} iniciado para {DeviceId}.")]
    private static partial void LogDeviceCommandStarted(ILogger logger, DeviceCommandType commandType, string deviceId);

    [LoggerMessage(EventId = 1602, Level = LogLevel.Warning, Message = "Comando {CommandType} para {DeviceId} concluiu sem sucesso. stage={Stage} errorCode={ErrorCode}")]
    private static partial void LogDeviceCommandFailed(ILogger logger, DeviceCommandType commandType, string deviceId, string stage, string errorCode);

    [LoggerMessage(EventId = 1603, Level = LogLevel.Information, Message = "Comando {CommandType} para {DeviceId} concluiu com sucesso. commandId={CommandId}")]
    private static partial void LogDeviceCommandSucceeded(ILogger logger, DeviceCommandType commandType, string deviceId, string commandId);
}

internal interface IDeviceOperationsRuntime
{
    event EventHandler? DevicesChanged;

    event EventHandler<string>? LogMessage;

    event EventHandler<DeviceLogMessage>? DeviceLogReceived;

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
    private readonly IDeviceServerClient integration;

    public DeviceOperationsRuntime(IDeviceServerClient integration)
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

    public event EventHandler<DeviceLogMessage>? DeviceLogReceived
    {
        add => integration.DeviceLogReceived += value;
        remove => integration.DeviceLogReceived -= value;
    }

    public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
    {
        add => integration.CommandProgressChanged += value;
        remove => integration.CommandProgressChanged -= value;
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
        return integration.SendCommandTrackedAsync(deviceId, commandType, parameters, timeout, cancellationToken);
    }
}
