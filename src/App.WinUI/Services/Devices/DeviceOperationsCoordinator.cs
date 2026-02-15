using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal sealed class DeviceOperationsCoordinator : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OtaCommandTimeout = TimeSpan.FromMinutes(10);
    private const int MaxLogEntries = 600;

    private readonly DeviceIntegrationService integration;
    private readonly object gate = new();
    private readonly List<string> logs = new();
    private readonly List<string> buildLogs = new();
    private readonly List<DeviceSnapshot> devicesSnapshot = new();
    private readonly Timer refreshTimer;

    private int refreshInFlight;
    private bool refreshActive;
    private bool buildInProgress;
    private int buildPercent;
    private string buildStatus = "Build: pronto";
    private bool commandInProgress;
    private int commandPercent;
    private string commandStatus = "Comandos: pronto";
    private string? lastCommandDeviceId;
    private string? activeCommandId;
    private DateTimeOffset lastRefreshUtc;
    private string? lastExportDirectory;
    private string? latestMergedBinPath;
    private bool disposed;

    public DeviceOperationsCoordinator(DeviceIntegrationService integration)
    {
        this.integration = integration;
        refreshTimer = new Timer(OnRefreshTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        integration.DevicesChanged += OnDevicesChanged;
        integration.LogMessage += OnLogMessage;
        integration.Host.CommandProgressChanged += OnHostCommandProgressChanged;
    }

    public event EventHandler? StateChanged;

    public event EventHandler<string>? BuildLogReceived;

    public event EventHandler? DeviceListChanged;

    public DeviceOperationsState GetStateSnapshot()
    {
        lock (gate)
        {
            return new DeviceOperationsState
            {
                BuildInProgress = buildInProgress,
                BuildPercent = buildPercent,
                BuildStatus = buildStatus,
                BuildLogs = buildLogs.ToArray(),
                CommandInProgress = commandInProgress,
                CommandPercent = commandPercent,
                CommandStatus = commandStatus,
                LastCommandDeviceId = lastCommandDeviceId,
                DeviceListSnapshot = devicesSnapshot.ToArray(),
                LastRefreshUtc = lastRefreshUtc,
                LastExportDirectory = lastExportDirectory,
                ServerBaseAddress = integration.GetServerBaseAddress(),
                Logs = logs.ToArray(),
            };
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

    public async Task<CommandDispatchResult> RunCommandAsync(
        string deviceId,
        DeviceCommandType commandType,
        CancellationToken cancellationToken = default)
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

        lock (gate)
        {
            if (commandInProgress)
            {
                return new CommandDispatchResult
                {
                    DeviceId = deviceId,
                    Accepted = false,
                    Completed = true,
                    Success = false,
                    ProgressPercent = commandPercent,
                    Stage = "busy",
                    Message = "Ja existe uma operacao em andamento.",
                    ErrorCode = "busy",
                };
            }

            commandInProgress = true;
            commandPercent = 0;
            commandStatus = $"Comandos: 0% ({DescribeCommand(commandType)})";
            lastCommandDeviceId = deviceId;
            activeCommandId = null;
        }

        RaiseStateChanged();
        AppendLog($"Comando iniciado ({DescribeCommand(commandType)}) para {deviceId}.");

        var timeout = commandType == DeviceCommandType.StartOta ? OtaCommandTimeout : CommandTimeout;

        CommandDispatchResult result;
        try
        {
            result = await integration.Host
                .SendCommandTrackedAsync(deviceId, commandType, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = commandPercent,
                Stage = "cancelled",
                Message = "Operacao cancelada.",
                ErrorCode = "cancelled",
            };
        }
        catch (Exception ex)
        {
            result = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = commandPercent,
                Stage = "error",
                Message = ex.Message,
                ErrorCode = "exception",
            };
        }

        lock (gate)
        {
            commandInProgress = false;
            commandPercent = Math.Clamp(Math.Max(commandPercent, result.ProgressPercent), 0, 100);
            commandStatus = BuildFinalCommandStatus(result);
            if (!string.IsNullOrWhiteSpace(result.CommandId))
            {
                activeCommandId = result.CommandId;
            }
        }

        RaiseStateChanged();
        AppendLog(BuildResultLogMessage(result));
        return result;
    }


    public async Task<CommandDispatchResult> RunCommandAsync(
        string deviceId,
        DeviceCommandType commandType,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken = default)
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

        lock (gate)
        {
            if (commandInProgress)
            {
                return new CommandDispatchResult
                {
                    DeviceId = deviceId,
                    Accepted = false,
                    Completed = true,
                    Success = false,
                    ProgressPercent = commandPercent,
                    Stage = "busy",
                    Message = "Ja existe uma operacao em andamento.",
                    ErrorCode = "busy",
                };
            }

            commandInProgress = true;
            commandPercent = 0;
            commandStatus = $"Comandos: 0% ({DescribeCommand(commandType)})";
            lastCommandDeviceId = deviceId;
            activeCommandId = null;
        }

        RaiseStateChanged();
        AppendLog($"Comando iniciado ({DescribeCommand(commandType)}) para {deviceId}.");

        var timeout = commandType == DeviceCommandType.StartOta ? OtaCommandTimeout : CommandTimeout;

        CommandDispatchResult result;
        try
        {
            result = await integration.Host
                .SendCommandTrackedAsync(deviceId, commandType, parameters, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = commandPercent,
                Stage = "cancelled",
                Message = "Operacao cancelada.",
                ErrorCode = "cancelled",
            };
        }
        catch (Exception ex)
        {
            result = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ProgressPercent = commandPercent,
                Stage = "error",
                Message = ex.Message,
                ErrorCode = "exception",
            };
        }

        lock (gate)
        {
            commandInProgress = false;
            commandPercent = Math.Clamp(Math.Max(commandPercent, result.ProgressPercent), 0, 100);
            commandStatus = BuildFinalCommandStatus(result);
            if (!string.IsNullOrWhiteSpace(result.CommandId))
            {
                activeCommandId = result.CommandId;
            }
        }

        RaiseStateChanged();
        AppendLog(BuildResultLogMessage(result));
        return result;
    }
    public async Task<CommandDispatchResult> StartOtaForDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var mergedPath = ResolveLatestMergedBinPath();
        if (string.IsNullOrWhiteSpace(mergedPath) || !File.Exists(mergedPath))
        {
            var noArtifact = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = false,
                Completed = true,
                Success = false,
                Stage = "ota-artifact-missing",
                Message = "Nenhum firmware consolidado encontrado. Gere um build antes da OTA.",
                ErrorCode = "ota_artifact_missing",
            };

            AppendLog(noArtifact.Message!);
            lock (gate)
            {
                commandStatus = "Comandos: firmware OTA indisponivel";
            }

            RaiseStateChanged();
            return noArtifact;
        }

        var version = Path.GetFileNameWithoutExtension(mergedPath);
        if (!integration.Host.SetOtaArtifact(mergedPath, version))
        {
            var failed = new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = false,
                Completed = true,
                Success = false,
                Stage = "ota-artifact-config",
                Message = "Falha ao preparar artefato OTA no servidor.",
                ErrorCode = "ota_artifact_config_failed",
            };

            AppendLog(failed.Message!);
            return failed;
        }

        AppendLog($"OTA preparada com {Path.GetFileName(mergedPath)} para {deviceId}.");
        return await RunCommandAsync(deviceId, DeviceCommandType.StartOta, cancellationToken).ConfigureAwait(false);
    }

    public Task<CommandDispatchResult> InstallAppAsync(string deviceId, DeviceAppCommandPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

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
    public async Task<string?> BuildAndExportAsync(string profile, CancellationToken cancellationToken = default)
    {
        var alreadyRunning = false;

        lock (gate)
        {
            if (buildInProgress)
            {
                AppendLogLocked("Build ja em andamento. Aguarde concluir.");
                alreadyRunning = true;
            }
            else
            {
                buildInProgress = true;
                buildPercent = 0;
                buildStatus = $"Build: 0% ({profile})";
            }
        }

        if (alreadyRunning)
        {
            RaiseStateChanged();
            return null;
        }

        RaiseStateChanged();
        AppendLog($"Build iniciado: {profile}");

        try
        {
            var request = new FirmwareBuildRequest
            {
                Profile = profile,
                WorkingDirectory = ResolveFirmwareWorkingDirectory(),
            };

            var progress = new Progress<BuildProgressUpdate>(OnBuildProgress);
            var artifacts = await integration.FirmwareBuildService
                .BuildAsync(request, progress, cancellationToken)
                .ConfigureAwait(false);

            OnBuildProgress(new BuildProgressUpdate
            {
                Percent = 96,
                Stage = "export",
                Message = "Exportando pacote de firmware...",
            });

            var exportPath = await integration.FirmwareBuildService
                .ExportAsync(artifacts, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), cancellationToken)
                .ConfigureAwait(false);

            lock (gate)
            {
                latestMergedBinPath = artifacts.MergedBinPath;
                lastExportDirectory = exportPath;
                buildPercent = 100;
                buildStatus = "Build: 100% (concluido)";
            }

            if (!string.IsNullOrWhiteSpace(artifacts.MergedBinPath))
            {
                var version = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
                _ = integration.Host.SetOtaArtifact(artifacts.MergedBinPath, version);
            }

            AppendBuildLog($"Firmware exportado em: {exportPath}");
            RaiseStateChanged();
            return exportPath;
        }
        catch (Exception ex)
        {
            lock (gate)
            {
                buildStatus = "Build: erro";
            }

            AppendBuildLog($"Erro no build/export: {ex.Message}");
            RaiseStateChanged();
            return null;
        }
        finally
        {
            lock (gate)
            {
                buildInProgress = false;
                if (buildStatus == "Build: pronto")
                {
                    buildPercent = 0;
                }
            }

            RaiseStateChanged();
        }
    }

    public string ResolveFirmwareDirectoryToOpen()
    {
        lock (gate)
        {
            if (!string.IsNullOrWhiteSpace(lastExportDirectory) && Directory.Exists(lastExportDirectory))
            {
                return lastExportDirectory;
            }
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MicaAudio", "firmware");
        if (!Directory.Exists(root))
        {
            return root;
        }

        var latest = new DirectoryInfo(root)
            .EnumerateDirectories()
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .FirstOrDefault();

        return latest?.FullName ?? root;
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
        integration.Host.CommandProgressChanged -= OnHostCommandProgressChanged;
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

        var shouldRaise = false;

        lock (gate)
        {
            if (!commandInProgress || !string.Equals(lastCommandDeviceId, progress.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(activeCommandId))
            {
                activeCommandId = progress.CommandId;
            }

            if (!string.Equals(activeCommandId, progress.CommandId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            commandPercent = Math.Clamp(Math.Max(commandPercent, progress.ProgressPercent), 0, 100);
            commandStatus = BuildLiveCommandStatus(progress);
            shouldRaise = true;
        }

        if (shouldRaise)
        {
            RaiseStateChanged();
            if (!string.IsNullOrWhiteSpace(progress.Message))
            {
                AppendLog($"[{DescribeStage(progress.Stage)}] {progress.Message}");
            }
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
            var nextSnapshot = integration.GetDevices()
                .Where(device => device.Status == DeviceStatus.Online && !string.IsNullOrWhiteSpace(device.FirmwareVersion))
                .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(device => device.DeviceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var changed = false;

            lock (gate)
            {
                lastRefreshUtc = DateTimeOffset.UtcNow;

                if (forcePublish || !AreSnapshotsEquivalent(devicesSnapshot, nextSnapshot))
                {
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

    private static bool AreSnapshotsEquivalent(IReadOnlyList<DeviceSnapshot> current, IReadOnlyList<DeviceSnapshot> next)
    {
        if (current.Count != next.Count)
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
                || !string.Equals(a.ActiveAppId, b.ActiveAppId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.ActiveAppName, b.ActiveAppName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void OnBuildProgress(BuildProgressUpdate update)
    {
        lock (gate)
        {
            buildInProgress = !update.IsTerminal;
            buildPercent = Math.Clamp(update.Percent, 0, 100);
            buildStatus = BuildBuildStatus(update);
        }

        if (!string.IsNullOrWhiteSpace(update.Message))
        {
            AppendBuildLog(update.Message);
        }
        else
        {
            RaiseStateChanged();
        }
    }

    private static string BuildBuildStatus(BuildProgressUpdate update)
    {
        var stage = string.IsNullOrWhiteSpace(update.Stage) ? "build" : update.Stage;
        return $"Build: {Math.Clamp(update.Percent, 0, 100)}% ({stage})";
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
            "ota_artifact_missing" => "Comandos: firmware OTA indisponivel",
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
            DeviceCommandType.TestLed => "testar LED",
            DeviceCommandType.StartOta => "atualizar firmware OTA",
            DeviceCommandType.InstallApp => "instalar app",
            DeviceCommandType.ActivateApp => "ativar app",
            DeviceCommandType.SetAppConfig => "configurar app",
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

    private string ResolveFirmwareWorkingDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "firmware", "matrixportal-s3"),
            Path.Combine(Environment.CurrentDirectory, "firmware", "matrixportal-s3"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "firmware", "matrixportal-s3"),
        }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private string? ResolveLatestMergedBinPath()
    {
        lock (gate)
        {
            if (!string.IsNullOrWhiteSpace(latestMergedBinPath) && File.Exists(latestMergedBinPath))
            {
                return latestMergedBinPath;
            }
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MicaAudio", "firmware");
        if (!Directory.Exists(root))
        {
            return null;
        }

        var candidate = new DirectoryInfo(root)
            .EnumerateDirectories()
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Select(directory => Path.Combine(directory.FullName, "matrixportal-s3_merged.bin"))
            .FirstOrDefault(File.Exists);

        lock (gate)
        {
            latestMergedBinPath = candidate;
        }

        return candidate;
    }

    private void AppendBuildLog(string message)
    {
        var line = AppendLog(message, isBuildLog: true);
        BuildLogReceived?.Invoke(this, line);
    }

    private string AppendLog(string message, bool isBuildLog = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        string line;
        lock (gate)
        {
            line = $"[{DateTimeOffset.Now:HH:mm:ss}] {message}";
            logs.Add(line);
            TrimToLimit(logs, MaxLogEntries);

            if (isBuildLog)
            {
                buildLogs.Add(line);
                TrimToLimit(buildLogs, MaxLogEntries);
            }
        }

        RaiseStateChanged();
        return line;
    }

    private void AppendLogLocked(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {message}";
        logs.Add(line);
        TrimToLimit(logs, MaxLogEntries);
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
}




