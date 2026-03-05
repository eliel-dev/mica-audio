using Device.Server.Hosting;

namespace App.Headless.Services;

internal interface IUsbOnboardingService
{
    Task<IReadOnlyList<UiOnboardingPortViewModel>> ListPortsAsync(CancellationToken cancellationToken = default);
    Task<UiOnboardingStartResponse> StartAsync(string portName, CancellationToken cancellationToken = default);
    UiOnboardingStatusViewModel GetStatus();
}

internal sealed class UsbOnboardingService : IUsbOnboardingService
{
    private static readonly TimeSpan PairingCodeTtl = TimeSpan.FromMinutes(10);

    private readonly object gate = new();
    private readonly ISerialPortCatalogService serialPortCatalog;
    private readonly IFirmwareBinaryResolver firmwareBinaryResolver;
    private readonly IEspToolFlashService flashService;
    private readonly IDeviceServerHost deviceServerHost;
    private readonly IUiRealtimePublisher realtimePublisher;
    private readonly ILogger<UsbOnboardingService> logger;

    private HeadlessOnboardingStatus status = new()
    {
        InProgress = false,
        Stage = HeadlessOnboardingStage.Idle,
        Message = "Aguardando onboarding.",
        Percent = 0,
        Completed = false,
        Success = false,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private Task? runningTask;

    public UsbOnboardingService(
        ISerialPortCatalogService serialPortCatalog,
        IFirmwareBinaryResolver firmwareBinaryResolver,
        IEspToolFlashService flashService,
        IDeviceServerHost deviceServerHost,
        IUiRealtimePublisher realtimePublisher,
        ILogger<UsbOnboardingService> logger)
    {
        this.serialPortCatalog = serialPortCatalog;
        this.firmwareBinaryResolver = firmwareBinaryResolver;
        this.flashService = flashService;
        this.deviceServerHost = deviceServerHost;
        this.realtimePublisher = realtimePublisher;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<UiOnboardingPortViewModel>> ListPortsAsync(CancellationToken cancellationToken = default)
    {
        var ports = await serialPortCatalog.ListAsync(includeAllPorts: false, cancellationToken).ConfigureAwait(false);
        return ports.Select(port => new UiOnboardingPortViewModel
        {
            PortName = port.PortName,
            DisplayName = port.DisplayName,
            PnpDeviceId = port.PnpDeviceId,
            VidPid = port.VidPid,
            IsPreferredDevice = port.IsPreferredDevice,
            PriorityRank = port.PriorityRank,
            Label = port.Label,
        }).ToArray();
    }

    public Task<UiOnboardingStartResponse> StartAsync(string portName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return Task.FromResult(new UiOnboardingStartResponse
            {
                Accepted = false,
                Status = "invalid",
                Message = "Selecione uma porta COM valida para continuar.",
                Snapshot = GetStatus(),
            });
        }

        lock (gate)
        {
            if (status.InProgress)
            {
                return Task.FromResult(new UiOnboardingStartResponse
                {
                    Accepted = false,
                    Status = "busy",
                    Message = "Onboarding ja esta em andamento.",
                    Snapshot = MapStatus(status),
                });
            }

            status = status with
            {
                InProgress = true,
                Stage = HeadlessOnboardingStage.SelectPort,
                Message = $"Iniciando onboarding USB na porta {portName}...",
                Percent = 0,
                PortName = portName.Trim(),
                Completed = false,
                Success = false,
                ErrorCode = null,
                PairCode = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };

            runningTask = Task.Run(() => RunOnboardingAsync(status.PortName!, CancellationToken.None), CancellationToken.None);
        }

        PublishProgress(status);

        return Task.FromResult(new UiOnboardingStartResponse
        {
            Accepted = true,
            Status = "started",
            Message = "Onboarding iniciado.",
            Snapshot = GetStatus(),
        });
    }

    public UiOnboardingStatusViewModel GetStatus()
    {
        lock (gate)
        {
            return MapStatus(status);
        }
    }

    private async Task RunOnboardingAsync(string portName, CancellationToken cancellationToken)
    {
        try
        {
            if (!firmwareBinaryResolver.TryResolve(out var firmwarePath, out var firmwareError))
            {
                Fail("firmware_missing", firmwareError);
                return;
            }

            UpdateStatus(new HeadlessOnboardingProgress
            {
                Stage = HeadlessOnboardingStage.Flashing,
                Message = $"Firmware selecionado: {Path.GetFileName(firmwarePath)}",
                Percent = 0,
            });

            var progress = new Progress<HeadlessOnboardingProgress>(UpdateStatus);
            var flashResult = await flashService
                .FlashAsync(portName, firmwarePath, progress, cancellationToken)
                .ConfigureAwait(false);

            if (!flashResult.Success)
            {
                Fail(flashResult.ErrorCode ?? "flash_failed", flashResult.Message);
                return;
            }

            var pairing = deviceServerHost.CreatePairingCode(PairingCodeTtl);

            UpdateStatus(new HeadlessOnboardingProgress
            {
                Stage = HeadlessOnboardingStage.Pairing,
                Message = $"Flash concluido. Codigo de pareamento: {pairing.Code}.",
                Percent = 92,
            });

            lock (gate)
            {
                status = status with
                {
                    InProgress = false,
                    Stage = HeadlessOnboardingStage.Done,
                    Message = $"Firmware gravado. Use o codigo {pairing.Code} no portal AP do dispositivo.",
                    Percent = 100,
                    Completed = true,
                    Success = true,
                    ErrorCode = null,
                    PairCode = pairing.Code,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
            }

            PublishProgress(status);
            realtimePublisher.Publish(new
            {
                type = "onboarding-result",
                success = true,
                stage = "done",
                pairCode = pairing.Code,
                message = status.Message,
            });
        }
        catch (OperationCanceledException)
        {
            Fail("cancelled", "Onboarding cancelado.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha no onboarding USB.");
            Fail("onboarding_failed", ex.Message);
        }
    }

    private void UpdateStatus(HeadlessOnboardingProgress progress)
    {
        lock (gate)
        {
            status = status with
            {
                InProgress = true,
                Stage = progress.Stage,
                Message = progress.Message,
                Percent = Math.Clamp(progress.Percent, 0, 100),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        PublishProgress(status);
    }

    private void Fail(string code, string message)
    {
        lock (gate)
        {
            status = status with
            {
                InProgress = false,
                Stage = HeadlessOnboardingStage.Failed,
                Message = message,
                Percent = Math.Clamp(status.Percent, 0, 100),
                Completed = true,
                Success = false,
                ErrorCode = code,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        PublishProgress(status);
        realtimePublisher.Publish(new
        {
            type = "onboarding-result",
            success = false,
            stage = "failed",
            errorCode = code,
            message,
        });
    }

    private void PublishProgress(HeadlessOnboardingStatus snapshot)
    {
        realtimePublisher.Publish(new
        {
            type = "onboarding-progress",
            stage = snapshot.Stage.ToString().ToLowerInvariant(),
            message = snapshot.Message,
            percent = snapshot.Percent,
            inProgress = snapshot.InProgress,
            completed = snapshot.Completed,
            portName = snapshot.PortName,
        });
    }

    private static UiOnboardingStatusViewModel MapStatus(HeadlessOnboardingStatus source)
    {
        return new UiOnboardingStatusViewModel
        {
            InProgress = source.InProgress,
            Stage = source.Stage.ToString().ToLowerInvariant(),
            Message = source.Message,
            Percent = source.Percent,
            PortName = source.PortName,
            Completed = source.Completed,
            Success = source.Success,
            ErrorCode = source.ErrorCode,
            PairCode = source.PairCode,
            UpdatedAtUtc = source.UpdatedAtUtc,
        };
    }
}
