using App.WinUI.Services.Firmware;
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
internal sealed partial class DeviceUsbOnboardingService : IDeviceUsbOnboardingService
{
    private static readonly TimeSpan PairingCodeTtl = TimeSpan.FromMinutes(10);
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly PrecompiledFirmwareService firmwareService;
    private readonly IEspToolFlashService flashService;
    private readonly ILogger<DeviceUsbOnboardingService> logger;

    public DeviceUsbOnboardingService(
        DeviceOperationsCoordinator deviceOps,
        PrecompiledFirmwareService firmwareService,
        IEspToolFlashService flashService,
        ILogger<DeviceUsbOnboardingService> logger)
    {
        this.deviceOps = deviceOps;
        this.firmwareService = firmwareService;
        this.flashService = flashService;
        this.logger = logger;
    }

    public async Task<DeviceOnboardingResult> RunAsync(
        DeviceOnboardingRequest request,
        IProgress<DeviceOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
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
        LogUsbOnboardingReady(logger, request.PortName, pairing.Code);

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Pairing,
            Message = "Flash concluido. Conecte no AP do dispositivo e use o codigo de pareamento exibido.",
            Percent = 90,
        });

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Done,
            Message = "Flash concluido. Proximo passo: provisionar Wi-Fi via AP do ESP32.",
            Percent = 100,
        });

        return new DeviceOnboardingResult
        {
            Success = true,
            PairCode = pairing.Code,
            Message = $"Firmware gravado. Use o codigo {pairing.Code} no portal AP do dispositivo.",
        };
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

    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Onboarding USB em modo AP concluido. porta={PortName} pairCode={PairCode}")]
    private static partial void LogUsbOnboardingReady(ILogger logger, string portName, string pairCode);
}
