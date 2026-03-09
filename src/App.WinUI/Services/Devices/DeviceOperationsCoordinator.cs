using App.WinUI.Services;
using App.WinUI.Services.Logging;
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
    private readonly DeviceLogBook logBook = new(MaxLogEntries, MaxDeviceLogEntries);
    private readonly DeviceCommandTracker commandTracker = new();
    private readonly DeviceRefreshCoordinator refreshCoordinator = new();
    private readonly DeviceLifecycleThresholdProvider lifecycleThresholdProvider;
    private readonly DeviceCommandDispatcher commandDispatcher;
    private readonly Timer refreshTimer;
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
        lifecycleThresholdProvider = new DeviceLifecycleThresholdProvider(settingsRepository, settingsDomainService);
        commandDispatcher = new DeviceCommandDispatcher(integration, CommandTimeout);
        refreshTimer = new Timer(OnRefreshTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        integration.DevicesChanged += OnDevicesChanged;
        integration.LogMessage += OnLogMessage;
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

    public IReadOnlyList<string> GetDeviceLogs(string deviceId)
        => logBook.GetDeviceLogs(deviceId);

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
            return DeviceCommandDispatcher.CreateInvalidDeviceResult();
        }

        var normalizedDeviceId = deviceId.Trim();
        var operationLabel = DeviceOperationsText.DescribeCommand(commandType);

        if (!commandTracker.TryQueue(normalizedDeviceId, operationLabel, out var busyResult))
        {
            return busyResult!;
        }

        RaiseStateChanged();
        AppendDeviceLog(normalizedDeviceId, $"Comando iniciado ({operationLabel}).");
        AppendLog($"Comando iniciado ({operationLabel}) para {normalizedDeviceId}.");

        var result = await commandDispatcher
            .DispatchAsync(normalizedDeviceId, commandType, parameters, cancellationToken)
            .ConfigureAwait(false);

        commandTracker.Complete(normalizedDeviceId, result);
        RaiseStateChanged();

        var resultMessage = DeviceOperationsText.BuildResultLogMessage(result);
        AppendDeviceLog(normalizedDeviceId, resultMessage);
        AppendLog(resultMessage);
        return result;
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
        AppendLog(message);
    }

    private void OnHostCommandProgressChanged(object? sender, DeviceCommandProgressMessage progress)
    {
        if (!commandTracker.ApplyProgress(progress, out var normalizedDeviceId, out var progressMessage))
        {
            return;
        }

        AppendDeviceLog(normalizedDeviceId, progressMessage);
        RaiseStateChanged();
        AppendLog(progressMessage);
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
                DeviceListChanged?.Invoke(this, EventArgs.Empty);
            }

            if (update.Changed || forcePublish)
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
            refreshCoordinator.ExitRefresh();
        }
    }

    private void AppendDeviceLog(string deviceId, string message)
    {
        if (logBook.AppendDevice(deviceId, message))
        {
            RaiseStateChanged();
        }
    }

    private void AppendLog(string message)
    {
        if (logBook.AppendGlobal(message))
        {
            CentralLogStore?.Append(LogCategory.Devices, LogSeverity.Info, message);
            RaiseStateChanged();
        }
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
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
