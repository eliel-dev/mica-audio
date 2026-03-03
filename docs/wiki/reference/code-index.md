# Referencia - Code Index

Pontos principais do cutover HUB75 128x64:

- [LedDefaults](../../../src/MicaAudio.Core/Led/LedDefaults.cs#L1)
- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [StreamFrameV2](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L1)
- [MatrixPortalLedOutput](../../../src/Output/Led/MatrixPortalLedOutput.cs#L1)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L1)
- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [Firmware main.cpp](../../../firmware/matrixportal-s3/src/main.cpp#L1)
- [platformio.ini](../../../firmware/matrixportal-s3/platformio.ini#L1)

Notas ativas:`r`n`r`n- Firmware oficial unico: `dma_exp` (DevKitC-1 128x64).

Pontos de UI para operacao de devices:

- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage UI programatica](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DeviceListRowControl](../../../src/App.WinUI/Views/Controls/DeviceListRowControl.cs#L1)
- [DevicePreviewResolver](../../../src/App.WinUI/Services/Devices/DevicePreviewResolver.cs#L1)
- [DevicePreviewVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DevicePreviewVisibilityPolicy.cs#L1)


Pontos de estado e visibilidade de devices:

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [DeviceListVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DeviceListVisibilityPolicy.cs#L1)
- [DeviceListRenderDiff](../../../src/App.WinUI/Services/Devices/DeviceListRenderDiff.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
Pontos de lifecycle leve de devices:

- [DeviceLifecyclePolicy](../../../src/App.WinUI/Services/Devices/DeviceLifecyclePolicy.cs#L1)
- [DeviceLifecyclePresentation](../../../src/App.WinUI/Services/Devices/DeviceLifecyclePresentation.cs#L1)
- [DeviceLifecycleThresholds](../../../src/App.WinUI/Services/Devices/DeviceLifecycleThresholds.cs#L1)
- [DeviceRegistryPresenceNormalizer](../../../src/App.WinUI/Services/Devices/DeviceRegistryPresenceNormalizer.cs#L1)
- [migrate-device-registry-presence-v1.ps1](../../../scripts/migrate-device-registry-presence-v1.ps1#L1)



- A DevicesPage usa diff incremental para manter a lista estavel e evitar rebuild total em refresh normal.

- A DevicesPage diferencia App ativo (online) de Ultimo app conhecido (offline) e exp?e remocao local via botao Remover.
