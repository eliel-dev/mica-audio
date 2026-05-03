# Referencia - Code Index

## Direcao Atual

- Fluxo oficial: `WinUI -> MicaAudio.Server -> ESP32-S3`.
- O WinUI e remote-only e nao hospeda server embedded.
- O runtime autoritativo de paineis server-capable vive no `MicaAudio.Server`.
- O ESP continua runtime de display conectado ao servidor por MQTT/WS/HTTP.

## App WinUI

- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [RemoteDeviceServerSecretStore](../../../src/App.WinUI/Services/Devices/RemoteDeviceServerSecretStore.cs#L1)
- [PanelsStore](../../../src/App.WinUI/Services/Panels/PanelsStore.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [PanelsFrameComposer adapter](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)
- [AppSettingsDomainService](../../../src/App.WinUI/Services/AppSettingsDomainService.cs#L1)

## Client Remoto

- [IDeviceServerClient](../../../src/Device.Client.Abstractions/IDeviceServerClient.cs#L1)
- [IDeviceServerClientRuntime](../../../src/Device.Client.Abstractions/IDeviceServerClientRuntime.cs#L1)
- [IDeviceFrameTransport](../../../src/Device.Client.Abstractions/IDeviceFrameTransport.cs#L1)
- [IDeviceClientSessionManager](../../../src/Device.Client.Abstractions/IDeviceClientSessionManager.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [RemoteDeviceFrameTransport](../../../src/Device.Client.Remote/RemoteDeviceFrameTransport.cs#L1)
- [RemoteDeviceServerRuntime](../../../src/Device.Client.Remote/RemoteDeviceServerRuntime.cs#L1)

## Server E Protocol

- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [DeviceServerHost.Admin](../../../src/Device.Server/Hosting/DeviceServerHost.Admin.cs#L1)
- [DeviceServerHost.Routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1)
- [DeviceServerHost.PanelsBatches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1)
- [IDeviceServerHost](../../../src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs#L1)
- [IPanelsBatchStore](../../../src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs#L1)
- [IPanelLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IPanelLibraryStore.cs#L1)
- [IMediaLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IMediaLibraryStore.cs#L1)
- [IPanelRuntimeStateStore](../../../src/Device.Server.Abstractions/Hosting/IPanelRuntimeStateStore.cs#L1)
- [IPanelRuntimeStatusStore](../../../src/Device.Server.Abstractions/Hosting/IPanelRuntimeStatusStore.cs#L1)
- [InMemoryPanelRuntimeStateStore](../../../src/Device.Server/Hosting/InMemoryPanelRuntimeStateStore.cs#L1)
- [InMemoryPanelRuntimeStatusStore](../../../src/Device.Server/Hosting/InMemoryPanelRuntimeStatusStore.cs#L1)

## Standalone Server

- [MicaAudio.Server](../../../src/MicaAudio.Server/MicaAudio.Server.csproj#L1)
- [Program](../../../src/MicaAudio.Server/Program.cs#L1)
- [MicaAudioServerBootstrap](../../../src/MicaAudio.Server/MicaAudioServerBootstrap.cs#L1)
- [MicaAudioServerRuntime](../../../src/MicaAudio.Server/MicaAudioServerRuntime.cs#L1)
- [MicaAudioServerOptions](../../../src/MicaAudio.Server/MicaAudioServerOptions.cs#L1)
- [StandaloneDeviceRegistryStore](../../../src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs#L1)
- [StandalonePanelLibraryStore](../../../src/MicaAudio.Server/StandalonePanelLibraryStore.cs#L1)
- [StandaloneMediaLibraryStore](../../../src/MicaAudio.Server/StandaloneMediaLibraryStore.cs#L1)
- [StandalonePanelRuntimeStateStore](../../../src/MicaAudio.Server/StandalonePanelRuntimeStateStore.cs#L1)
- [ServerPanelRuntimeService](../../../src/MicaAudio.Server/ServerPanelRuntimeService.cs#L1)
- [ServerPanelMediaSourceResolver](../../../src/MicaAudio.Server/ServerPanelMediaSourceResolver.cs#L1)

## Compositor Compartilhado De Paineis

- [MicaAudio.Panels](../../../src/MicaAudio.Panels/MicaAudio.Panels.csproj#L1)
- [PanelFrameComposer](../../../src/MicaAudio.Panels/PanelFrameComposer.cs#L1)
- [PanelsAnimatedWebpEncoder](../../../src/MicaAudio.Panels/PanelsAnimatedWebpEncoder.cs#L1)
- [Hub75GifDecoder](../../../src/MicaAudio.Panels/Hub75GifDecoder.cs#L1)
- [PanelsMatrixDrawHelpers](../../../src/MicaAudio.Panels/PanelsMatrixDrawHelpers.cs#L1)
- [PanelsMediaCache](../../../src/MicaAudio.Panels/PanelsMediaCache.cs#L1)
- [IPanelMediaSourceResolver](../../../src/MicaAudio.Panels/IPanelMediaSourceResolver.cs#L1)
- [PanelMediaSource](../../../src/MicaAudio.Panels/PanelMediaSource.cs#L1)

## DTOs

- [PanelLibraryDocument](../../../src/Device.Protocol/Models/PanelLibraryDocument.cs#L1)
- [PanelLibraryItem](../../../src/Device.Protocol/Models/PanelLibraryItem.cs#L1)
- [PanelWidgetItem](../../../src/Device.Protocol/Models/PanelWidgetItem.cs#L1)
- [PanelRuntimeStateDocument](../../../src/Device.Protocol/Models/PanelRuntimeStateDocument.cs#L1)
- [PanelRuntimeStatusDocument](../../../src/Device.Protocol/Models/PanelRuntimeStatusDocument.cs#L1)
- [MediaAssetInfo](../../../src/Device.Protocol/Models/MediaAssetInfo.cs#L1)
- [AdminEventMessage](../../../src/Device.Protocol/Models/AdminEventMessage.cs#L1)
- [PanelsBatchCommandPayload](../../../src/Device.Protocol/Models/PanelsBatchCommandPayload.cs#L1)
- [StreamFrameV2](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L1)
- [StreamFrameV3](../../../src/Device.Protocol/Stream/StreamFrameV3.cs#L1)

## Firmware

- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [Firmware session runtime](../../../firmware/esp32s3-devkitc1/src/mica_session.cpp#L1)
- [Firmware panels](../../../firmware/esp32s3-devkitc1/src/mica_panels.cpp#L1)
- [Firmware FS config](../../../firmware/esp32s3-devkitc1/src/mica_fs_config.cpp#L1)
- [Firmware data/config.json](../../../firmware/esp32s3-devkitc1/data/config.json#L1)
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1)

## Observacoes Ativas

- `Device.Client.Embedded` foi removido e nao deve voltar como fallback.
- Settings de servidor expõem apenas URL remota e admin token.
- O servidor standalone persiste biblioteca, midia e runtime de paineis em `StorageRoot`.
- Widgets client-only param com o WinUI; widgets server-capable continuam pelo servidor.
