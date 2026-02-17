# Code Index

Indice expandido para localizar rapidamente pontos de alteracao por pagina, servico e protocolo.

## App pages (WinUI)

- [App](../../../src/App.WinUI/App.xaml.cs#L14)
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L8)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L26)
- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L9)
- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L10)
- [ServerPage](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L8)

## UI services

- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L10)
- [AppSettingsDomainService](../../../src/App.WinUI/Services/AppSettingsDomainService.cs#L7)
- [DeviceIntegrationService](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L10)
- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L5)
- [FirmwareBuildService](../../../src/App.WinUI/Services/Devices/FirmwareBuildService.cs#L7)
- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L6)
- [AppDeploymentService](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L7)

## Audio, DSP and visual

- [WasapiLoopbackCaptureService](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L10)
- [SpectrumAnalyzer](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L9)
- [LogBandMapper](../../../src/Analyzer.Dsp/Analysis/LogBandMapper.cs#L7)
- [EnvelopeSmoother](../../../src/Analyzer.Dsp/Analysis/EnvelopeSmoother.cs#L6)
- [VisualizerEngine](../../../src/Visual.Win2D/Engine/VisualizerEngine.cs#L10)
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L5)

## Output

- [ILedOutput](../../../src/Output/Led/ILedOutput.cs#L6)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L6)
- [MatrixPortalLedOutput](../../../src/Output/Led/MatrixPortalLedOutput.cs#L9)

## Device server and protocol

- [IDeviceServerHost](../../../src/Device.Server/Hosting/IDeviceServerHost.cs#L6)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L17)
- [DeviceServerHost.Advanced](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L10)
- [DeviceCommandRequest](../../../src/Device.Protocol/Models/DeviceCommandRequest.cs#L3)
- [DeviceCommandProgressMessage](../../../src/Device.Protocol/Models/DeviceCommandProgressMessage.cs#L3)
- [StreamFrameV1](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L5)

## Firmware and build

- [main.cpp](../../../firmware/matrixportal-s3/src/main.cpp#L1)
- [platformio.ini](../../../firmware/matrixportal-s3/platformio.ini#L1)
- [dev-run.ps1](../../../scripts/dev-run.ps1#L1)
- [docs-validate.ps1](../../../scripts/docs-validate.ps1#L1)
- [docs-structural-gate.ps1](../../../scripts/docs-structural-gate.ps1#L1)
- [ai-governance-check.ps1](../../../scripts/ai-governance-check.ps1#L1)
- [git-hooks-bootstrap.ps1](../../../scripts/git-hooks-bootstrap.ps1#L1)
- [release.yml](../../../.github/workflows/release.yml#L1)
- [sign-release.ps1](../../../scripts/sign-release.ps1#L1)
- [MicaAudio.Installer.wixproj](../../../installer/MicaAudio.Installer/MicaAudio.Installer.wixproj#L1)
- [Product.wxs](../../../installer/MicaAudio.Installer/Product.wxs#L1)
- [MicaAudio.Bundle.wixproj](../../../installer/MicaAudio.Bundle/MicaAudio.Bundle.wixproj#L1)
- [Bundle.wxs](../../../installer/MicaAudio.Bundle/Bundle.wxs#L1)

## Governanca IA

- [AGENTS](../../../AGENTS.md#L1)
- [AI index](../ai/README.md)
- [AI contract YAML](ai-contract.v1.yaml)
- [AI contract schema](ai-contract.schema.json)
- [Handoffs](../../../docs/handoffs/README.md#L1)

## Persistencia

- [AppSettings](../../../src/MicaAudio.Core/Presets/AppSettings.cs#L5)
- [SettingsRepository](../../../src/App.WinUI/Services/SettingsRepository.cs#L6)
- [PresetRepository](../../../src/App.WinUI/Services/PresetRepository.cs#L6)

