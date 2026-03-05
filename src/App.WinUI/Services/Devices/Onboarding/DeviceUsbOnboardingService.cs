using App.WinUI.Infrastructure.Serial;
using App.WinUI.Services.Firmware;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Devices.Onboarding;

internal interface IDeviceUsbOnboardingService
{
    Task<DeviceOnboardingResult> RunAsync(
        DeviceOnboardingRequest request,
        IProgress<DeviceOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

// DOCS: docs/wiki/guides/setup-new-device.md#passos
internal sealed class DeviceUsbOnboardingService : IDeviceUsbOnboardingService
{
    private static readonly TimeSpan PairingCodeTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan VerifyTimeout = TimeSpan.FromSeconds(45);
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly PrecompiledFirmwareService firmwareService;
    private readonly IEspToolFlashService flashService;
    private readonly ISerialProvisioningClient serialProvisioningClient;
    private readonly ILogger<DeviceUsbOnboardingService> logger;

    public DeviceUsbOnboardingService(
        DeviceOperationsCoordinator deviceOps,
        PrecompiledFirmwareService firmwareService,
        IEspToolFlashService flashService,
        ISerialProvisioningClient serialProvisioningClient,
        ILogger<DeviceUsbOnboardingService> logger)
    {
        this.deviceOps = deviceOps;
        this.firmwareService = firmwareService;
        this.flashService = flashService;
        this.serialProvisioningClient = serialProvisioningClient;
        this.logger = logger;
    }

    public async Task<DeviceOnboardingResult> RunAsync(
        DeviceOnboardingRequest request,
        IProgress<DeviceOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Ssid))
        {
            return Fail("ssid_required", "SSID obrigatorio para onboarding.");
        }

        if (string.IsNullOrWhiteSpace(request.PortName))
        {
            return Fail("port_required", "Porta COM obrigatoria para onboarding.");
        }

        if (!firmwareService.TryResolveSource(
                PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                "dma_exp",
                out var firmwareOption,
                out var firmwarePath,
                out var firmwareError))
        {
            return Fail("firmware_missing", firmwareError);
        }

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Flashing,
            Message = $"Firmware selecionado: {firmwareOption.FileName}",
            Percent = 0,
        });

        var flash = await flashService
            .FlashAsync(request.PortName, firmwarePath, progress, cancellationToken)
            .ConfigureAwait(false);

        if (!flash.Success)
        {
            return Fail("flash_failed", flash.Message);
        }

        var pairing = deviceOps.CreatePairingCode(PairingCodeTtl);
        var serverBaseUrl = deviceOps.GetServerBaseAddress();
        var requestId = Guid.NewGuid().ToString("N");

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Pairing,
            Message = "Pareamento automatico iniciado.",
            Percent = 60,
        });

        var serialResult = await serialProvisioningClient
            .ProvisionAsync(
                request.PortName,
                new SerialProvisioningRequest
                {
                    RequestId = requestId,
                    Ssid = request.Ssid,
                    Password = request.Password,
                    ServerBaseUrl = serverBaseUrl,
                    PairCode = pairing.Code,
                },
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        if (!serialResult.Success)
        {
            return Fail(serialResult.ErrorCode ?? "serial_provision_failed", serialResult.Message);
        }

        if (string.IsNullOrWhiteSpace(serialResult.DeviceId))
        {
            return Fail("device_id_missing", "Provisionamento concluido, mas sem deviceId para verificacao.");
        }

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Verifying,
            Message = "Aguardando dispositivo ficar online no dashboard...",
            Percent = 90,
        });

        var online = await WaitForDeviceOnlineAsync(serialResult.DeviceId, cancellationToken).ConfigureAwait(false);
        if (!online)
        {
            return Fail("device_verify_timeout", "Timeout aguardando dispositivo online apos provisionamento.");
        }

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Done,
            Message = $"Dispositivo {serialResult.DeviceId} conectado com sucesso.",
            Percent = 100,
        });

        return new DeviceOnboardingResult
        {
            Success = true,
            DeviceId = serialResult.DeviceId,
            Message = $"Dispositivo {serialResult.DeviceId} pronto para uso.",
        };
    }

    private async Task<bool> WaitForDeviceOnlineAsync(string deviceId, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + VerifyTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            deviceOps.RequestRefresh();

            var state = deviceOps.GetStateSnapshot();
            var snapshot = state.DeviceListSnapshot.FirstOrDefault(item =>
                string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            if (snapshot is not null
                && snapshot.Status == DeviceStatus.Online)
            {
                return true;
            }

            await Task.Delay(800, cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning("Timeout ao verificar device online apos onboarding USB. deviceId={DeviceId}", deviceId);
        return false;
    }

    private static DeviceOnboardingResult Fail(string code, string message)
    {
        return new DeviceOnboardingResult
        {
            Success = false,
            ErrorCode = code,
            Message = message,
        };
    }
}
