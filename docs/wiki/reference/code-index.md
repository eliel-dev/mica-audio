# Referencia - Code Index

Pontos principais do cutover HUB75 128x64:

- [LedDefaults](../../../src/MicaAudio.Core/Led/LedDefaults.cs#L1)
- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [StreamFrameV2](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L1)
- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1)

Notas ativas:

- Firmware oficial unico: `dma_exp` (DevKitC-1 128x64).

Pontos de UI para operacao de devices:

- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage UI programatica](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DeviceListRowControl](../../../src/App.WinUI/Views/Controls/DeviceListRowControl.cs#L1)
- [DevicePreviewResolver](../../../src/App.WinUI/Services/Devices/DevicePreviewResolver.cs#L1)
- [DevicePreviewVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DevicePreviewVisibilityPolicy.cs#L1)
- [DeviceMetricsFormatter](../../../src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs#L1)
- [DeviceMetricsPresentation](../../../src/App.WinUI/Services/Devices/DeviceMetricsPresentation.cs#L1)

Pontos de estado e visibilidade de devices:

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [DeviceListVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DeviceListVisibilityPolicy.cs#L1)
- [DeviceListRenderDiff](../../../src/App.WinUI/Services/Devices/DeviceListRenderDiff.cs#L1)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)

Pontos de lifecycle leve de devices:

- [DeviceLifecyclePolicy](../../../src/App.WinUI/Services/Devices/DeviceLifecyclePolicy.cs#L1)
- [DeviceLifecyclePresentation](../../../src/App.WinUI/Services/Devices/DeviceLifecyclePresentation.cs#L1)
- [DeviceLifecycleThresholds](../../../src/App.WinUI/Services/Devices/DeviceLifecycleThresholds.cs#L1)
- [DeviceRegistryPresenceNormalizer](../../../src/App.WinUI/Services/Devices/DeviceRegistryPresenceNormalizer.cs#L1)
- [migrate-device-registry-presence-v1.ps1](../../../scripts/migrate-device-registry-presence-v1.ps1#L1)

Observacoes ativas:

- A DevicesPage usa diff incremental para manter a lista estavel e evitar rebuild total em refresh normal.
- A DevicesPage diferencia App ativo (online) de Ultimo app conhecido (offline), com acoes no card de resumo (`Testar LED` e `Remover`).
- O botao `Remover` consolida o fluxo: online tenta revogar/reiniciar e remove local; offline remove apenas local.
- A DevicesPage usa apenas miniatura inline da lista para preview de app; o painel da direita nao tem preview maior.
- A DevicesPage usa dashboard ESP + logs por device selecionado, com assinatura/cache para reduzir flicker.
- O DeviceServerHost aplica grace curto de detach WS (500ms) e detach por identidade de socket para reduzir flapping em reconexao rapida.
