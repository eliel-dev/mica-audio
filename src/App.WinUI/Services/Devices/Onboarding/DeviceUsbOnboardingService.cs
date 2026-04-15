using System.Diagnostics;
using System.Net;
using App.WinUI.Infrastructure.Observability;
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
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-04---rollback-para-ap-first-estavel
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
        using var activity = AppObservability.StartActivity("device-onboarding.run", AppObservability.AppComponent);
        activity?.SetTag(AppObservability.PortNameKey, request.PortName);
        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "device-onboarding.run"),
            (AppObservability.PortNameKey, request.PortName));

        try
        {
            if (string.IsNullOrWhiteSpace(request.PortName))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "port_required");
                return Fail("port_required", "Porta COM obrigatoria para onboarding.");
            }

            if (!TryResolveProvisioningServerBaseAddress(deviceOps.GetServerBaseAddress(), out var serverBaseAddress, out var serverAddressError))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "server_unavailable");
                return Fail("server_unavailable", serverAddressError);
            }

            var firmwareRefresh = await firmwareService
                .EnsureOfficialFirmwareFreshAsync(
                    PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                    PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                    "dma_exp",
                    cancellationToken)
                .ConfigureAwait(false);
            if (!firmwareRefresh.IsFresh || firmwareRefresh.ResolvedArtifact is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "firmware_missing");
                return Fail("firmware_missing", firmwareRefresh.FailureReason);
            }

            var artifact = firmwareRefresh.ResolvedArtifact;

            if (!string.Equals(artifact.Manifest.ControlPlane, PrecompiledFirmwareService.RequiredControlPlane, StringComparison.OrdinalIgnoreCase))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "firmware_incompatible");
                return Fail(
                    "firmware_incompatible",
                    $"Firmware precompilado incompativel com o control plane atual. Esperado '{PrecompiledFirmwareService.RequiredControlPlane}', encontrado '{artifact.Manifest.ControlPlane}'.");
            }

            progress?.Report(new DeviceOnboardingProgress
            {
                Stage = DeviceOnboardingStage.Flashing,
                Message = $"Firmware selecionado: {artifact.Option.FileName} ({artifact.Manifest.FirmwareVersion})",
                Percent = 0,
            });

            var flash = await flashService
                .FlashAsync(request.PortName, artifact.FirmwarePath, progress, cancellationToken)
                .ConfigureAwait(false);

            if (!flash.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, flash.ExitCode == 0 ? "flash_failed" : $"flash_exit_{flash.ExitCode}");
                LogUsbOnboardingFlashFailed(logger);
                return Fail("flash_failed", flash.Message);
            }

            var pairing = deviceOps.CreateHiddenPairingCode(PairingCodeTtl);
            activity?.SetTag("pairing.code.issued", true);
            LogUsbOnboardingPairingReady(logger, request.PortName, serverBaseAddress);

            progress?.Report(new DeviceOnboardingProgress
            {
                Stage = DeviceOnboardingStage.Pairing,
                Message = "Flash concluido. Abra o AP MicaAudio-Setup-xxxx e finalize o provisioning com o pair code.",
                Percent = 100,
            });

            progress?.Report(new DeviceOnboardingProgress
            {
                Stage = DeviceOnboardingStage.Done,
                Message = "Flash concluido. O proximo passo e conectar ao AP do ESP32 e informar o pair code.",
                Percent = 100,
            });

            LogUsbOnboardingSucceeded(logger, request.PortName);
            return new DeviceOnboardingResult
            {
                Success = true,
                PairCode = pairing.Code,
                Message = $"Firmware gravado. Conecte ao Wi-Fi MicaAudio-Setup-xxxx e informe o servidor {serverBaseAddress} com o pair code exibido.",
            };
        }
        catch (Exception ex)
        {
            AppObservability.SetException(activity, ex);
            LogUsbOnboardingException(logger, ex);
            throw;
        }
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

    private static bool TryResolveProvisioningServerBaseAddress(string rawServerBaseAddress, out string normalizedServerBaseAddress, out string errorMessage)
    {
        normalizedServerBaseAddress = string.Empty;
        errorMessage = string.Empty;

        if (!Uri.TryCreate(rawServerBaseAddress, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            errorMessage = "Servidor local invalido para onboarding. Reinicie o app e tente novamente.";
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Servidor local nao exposto na rede. Conecte o PC a uma interface LAN ou Wi-Fi valida antes de provisionar o device.";
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var address)
            && (IPAddress.IsLoopback(address) || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address)))
        {
            errorMessage = "Servidor local nao exposto na rede. Conecte o PC a uma interface LAN ou Wi-Fi valida antes de provisionar o device.";
            return false;
        }

        normalizedServerBaseAddress = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Onboarding USB pronto para AP-first. porta={PortName} server={ServerBaseAddress}")]
    private static partial void LogUsbOnboardingPairingReady(ILogger logger, string portName, string serverBaseAddress);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Warning, Message = "Onboarding USB falhou no flash.")]
    private static partial void LogUsbOnboardingFlashFailed(ILogger logger);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Information, Message = "Onboarding USB concluiu o flash e gerou pair code. porta={PortName}")]
    private static partial void LogUsbOnboardingSucceeded(ILogger logger, string portName);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Warning, Message = "Onboarding USB falhou com excecao.")]
    private static partial void LogUsbOnboardingException(ILogger logger, Exception exception);
}
