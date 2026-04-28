# Referencia - Code Index

Pontos principais do cutover HUB75 128x64:

- [LedDefaults](../../../src/MicaAudio.Core/Led/LedDefaults.cs#L1)
- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [StreamFrameV2](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L1)
- [StreamFrameV3](../../../src/Device.Protocol/Stream/StreamFrameV3.cs#L1)
- [VisualUdpFrameV1](../../../src/Device.Protocol/Stream/VisualUdpFrameV1.cs#L1)
- [Esp32S3LedOutput](../../../src/Output/Led/Esp32S3LedOutput.cs#L1)
- [LedFrameDeduplicator](../../../src/Output/Led/LedFrameDeduplicator.cs#L1)
- [SimulatorLedOutput](../../../src/Output/Led/SimulatorLedOutput.cs#L1)
- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L1)
- [Hub75VisualizerFrameRenderer](../../../src/App.WinUI/Services/Visualizer/Hub75VisualizerFrameRenderer.cs#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [FirmwareArtifactManifest](../../../src/App.WinUI/Services/Firmware/FirmwareArtifactManifest.cs#L1)
- [ResolvedFirmwareArtifact](../../../src/App.WinUI/Services/Firmware/ResolvedFirmwareArtifact.cs#L1)
- [Hub75PreviewHelper](../../../src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs#L1)
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [MainPage XAML](../../../src/App.WinUI/Views/MainPage.xaml#L1)
- [MainPage startup helpers](../../../src/App.WinUI/Views/MainPage.Startup.cs#L1)
- [MainPage settings pane helpers](../../../src/App.WinUI/Views/MainPage.SettingsPane.cs#L1)
- [MainPage settings bindings helpers](../../../src/App.WinUI/Views/MainPage.SettingsBindings.cs#L1)
- [MainPage visualizer runtime helpers](../../../src/App.WinUI/Views/MainPage.VisualizerRuntime.cs#L1)
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1)
- [ShellPageContentFactory](../../../src/App.WinUI/Views/ShellPageContentFactory.cs#L1)
- [MonitoringPage](../../../src/App.WinUI/Views/MonitoringPage.xaml.cs#L1)
- [MonitoringPage UI](../../../src/App.WinUI/Views/MonitoringPage.Ui.cs#L1)
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [RemoteDeviceServerSecretStore](../../../src/App.WinUI/Services/Devices/RemoteDeviceServerSecretStore.cs#L1)
- [RemoteDeviceServerConnectionTester](../../../src/App.WinUI/Services/Devices/RemoteDeviceServerConnectionTester.cs#L1)
- [AppStartupDiagnostics](../../../src/App.WinUI/Infrastructure/AppStartupDiagnostics.cs#L1)
- [AppLogStore](../../../src/App.WinUI/Services/Logging/AppLogStore.cs#L1)
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [Firmware session runtime](../../../firmware/esp32s3-devkitc1/src/mica_session.cpp#L1)
- [Firmware visual UDP](../../../firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp#L1)
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1)
- [Board local N16R8](../../../firmware/esp32s3-devkitc1/boards/mica_esp32_s3_devkitc1_n16r8.json#L1)
- [Particao local 3MB APP / 9.9MB FATFS](../../../firmware/esp32s3-devkitc1/partitions/mica_app3M_fat9M_16MB.csv#L1)
- [build-precompiled-firmware.ps1](../../../scripts/build-precompiled-firmware.ps1#L1)
- [MicaAudio.Server](../../../src/MicaAudio.Server/MicaAudio.Server.csproj#L1)
- [MicaAudio.Server Program](../../../src/MicaAudio.Server/Program.cs#L1)
- [MicaAudioServerBootstrap](../../../src/MicaAudio.Server/MicaAudioServerBootstrap.cs#L1)
- [MicaAudioServerOptions](../../../src/MicaAudio.Server/MicaAudioServerOptions.cs#L1)
- [MicaAudioServerRuntime](../../../src/MicaAudio.Server/MicaAudioServerRuntime.cs#L1)
- [StandaloneDeviceRegistryStore](../../../src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs#L1)
- [StandalonePanelLibraryStore](../../../src/MicaAudio.Server/StandalonePanelLibraryStore.cs#L1)
- [StandaloneMediaLibraryStore](../../../src/MicaAudio.Server/StandaloneMediaLibraryStore.cs#L1)
- [MicaAudio.Server Dockerfile](../../../src/MicaAudio.Server/Dockerfile#L1)
- [Docker server redeploy](../../../scripts/docker-server-redeploy.ps1#L1)
- [Render Blueprint](../../../render.yaml#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [DeviceServerHost Admin](../../../src/Device.Server/Hosting/DeviceServerHost.Admin.cs#L1)
- [VisualUdpSender](../../../src/Device.Server/Hosting/VisualUdpSender.cs#L1)
- [DeviceServerHost panels batches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1)
- [IPanelsBatchStore](../../../src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs#L1)
- [PanelsBatchWrite](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchWrite.cs#L1)
- [PanelsBatchEntry](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchEntry.cs#L1)
- [InMemoryPanelsBatchStore](../../../src/Device.Server/Hosting/InMemoryPanelsBatchStore.cs#L1)
- [IPanelLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IPanelLibraryStore.cs#L1)
- [IMediaLibraryStore](../../../src/Device.Server.Abstractions/Hosting/IMediaLibraryStore.cs#L1)
- [InMemoryPanelLibraryStore](../../../src/Device.Server/Hosting/InMemoryPanelLibraryStore.cs#L1)
- [InMemoryMediaLibraryStore](../../../src/Device.Server/Hosting/InMemoryMediaLibraryStore.cs#L1)
- [IDevicePairingStore](../../../src/Device.Server.Abstractions/Hosting/IDevicePairingStore.cs#L1)
- [InMemoryDevicePairingStore](../../../src/Device.Server/Hosting/InMemoryDevicePairingStore.cs#L1)
- [ICommandStateStore](../../../src/Device.Server.Abstractions/Hosting/ICommandStateStore.cs#L1)
- [TrackedCommandState](../../../src/Device.Server.Abstractions/Hosting/TrackedCommandState.cs#L1)
- [InMemoryCommandStateStore](../../../src/Device.Server/Hosting/InMemoryCommandStateStore.cs#L1)
- [ISessionStateStore](../../../src/Device.Server.Abstractions/Hosting/ISessionStateStore.cs#L1)
- [DeviceSessionState](../../../src/Device.Server.Abstractions/Hosting/DeviceSessionState.cs#L1)
- [InMemorySessionStateStore](../../../src/Device.Server/Hosting/InMemorySessionStateStore.cs#L1)
- [DeviceFrameConnection](../../../src/Device.Server/Hosting/DeviceFrameConnection.cs#L1)
- [DeviceFrameConnectionRegistry](../../../src/Device.Server/Hosting/DeviceFrameConnectionRegistry.cs#L1)
- [DeviceRecordMutations](../../../src/Device.Server.Abstractions/Hosting/DeviceRecordMutations.cs#L1)
- [DeviceServerHost dashboard](../../../src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs#L1)
- [DeviceServerHost MQTT](../../../src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs#L1)
- [IDeviceServerHost](../../../src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs#L1)
- [IDeviceServerClient](../../../src/Device.Client.Abstractions/IDeviceServerClient.cs#L1)
- [IDeviceServerClientRuntime](../../../src/Device.Client.Abstractions/IDeviceServerClientRuntime.cs#L1)
- [IDeviceFrameTransport](../../../src/Device.Client.Abstractions/IDeviceFrameTransport.cs#L1)
- [EmbeddedDeviceServerClient](../../../src/Device.Client.Embedded/EmbeddedDeviceServerClient.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [RemoteDeviceFrameTransport](../../../src/Device.Client.Remote/RemoteDeviceFrameTransport.cs#L1)
- [RemoteDeviceServerRuntime](../../../src/Device.Client.Remote/RemoteDeviceServerRuntime.cs#L1)
- [DeviceOfficialFirmwareCatalog](../../../src/Device.Server.Abstractions/Hosting/DeviceOfficialFirmwareCatalog.cs#L1)
- [DeviceMqttTopics](../../../src/Device.Server/Hosting/DeviceMqttTopics.cs#L1)
- [PairDeviceResponse](../../../src/Device.Protocol/Models/PairDeviceResponse.cs#L1)
- [MicaDiscoveryRequestV1](../../../src/Device.Protocol/Models/MicaDiscoveryRequestV1.cs#L1)
- [MicaDiscoveryResponseV1](../../../src/Device.Protocol/Models/MicaDiscoveryResponseV1.cs#L1)
- [PanelLibraryDocument](../../../src/Device.Protocol/Models/PanelLibraryDocument.cs#L1)
- [MediaAssetInfo](../../../src/Device.Protocol/Models/MediaAssetInfo.cs#L1)
- [ServerInfoResponse](../../../src/Device.Protocol/Models/ServerInfoResponse.cs#L1)
- [DevicePresenceMessage](../../../src/Device.Protocol/Models/DevicePresenceMessage.cs#L1)
- [DeviceSessionShadowMessage](../../../src/Device.Protocol/Models/DeviceSessionShadowMessage.cs#L1)
- [DeviceControlPlaneState](../../../src/Device.Protocol/Models/DeviceControlPlaneState.cs#L1)
- [DeviceStatsMessage](../../../src/Device.Protocol/Models/DeviceStatsMessage.cs#L1)
- [DeviceLogMessage](../../../src/Device.Protocol/Models/DeviceLogMessage.cs#L1)
- [AdminDevicesResponse](../../../src/Device.Protocol/Models/AdminDevicesResponse.cs#L1)
- [AdminVisualEndpointsResponse](../../../src/Device.Protocol/Models/AdminVisualEndpointsResponse.cs#L1)
- [DeviceVisualEndpointInfo](../../../src/Device.Protocol/Models/DeviceVisualEndpointInfo.cs#L1)
- [AdminEventMessage](../../../src/Device.Protocol/Models/AdminEventMessage.cs#L1)

Notas ativas:

- Firmware oficial unico: `dma_exp` (DevKitC-1 128x64).
- Direcao oficial: `server = control plane`, `cliente = data plane LAN`, `ESP32 = runtime com ownership por device`.
- `StreamFrameV2` continua como wire legado; `StreamFrameV3` adiciona `ownerEpoch` para stream direto session-aware.
- O env oficial `esp32s3_devkitc1_dma_exp` agora usa board local N16R8 (`16MB + OPI PSRAM + 3MB APP / 9.9MB FATFS`) para evitar drift do board padrao `N8` do PlatformIO.
- O portal AP do firmware mantem `Servidor` editavel apenas como fallback tecnico; o caminho normal usa Wi-Fi salvo + UDP discovery LAN para auto-registro.

Pontos de UI para operacao de devices:

- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage onboarding](../../../src/App.WinUI/Views/DevicesPage.Onboarding.cs#L1)
- [DevicesPage list state](../../../src/App.WinUI/Views/DevicesPage.ListState.cs#L1)
- [DevicesPage preview pump](../../../src/App.WinUI/Views/DevicesPage.PreviewPump.cs#L1)
- [DevicesPage dashboard](../../../src/App.WinUI/Views/DevicesPage.Dashboard.cs#L1)
- [DevicesPage WebView dashboard](../../../src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs#L1)
- [DevicesPage selection](../../../src/App.WinUI/Views/DevicesPage.Selection.cs#L1)
- [DevicesPage UI programatica](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DeviceListRowControl](../../../src/App.WinUI/Views/Controls/DeviceListRowControl.cs#L1)
- [DevicePreviewResolver](../../../src/App.WinUI/Services/Devices/DevicePreviewResolver.cs#L1)
- [DevicePreviewVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DevicePreviewVisibilityPolicy.cs#L1)
- [DeviceMetricsFormatter](../../../src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs#L1)
- [DeviceMetricsPresentation](../../../src/App.WinUI/Services/Devices/DeviceMetricsPresentation.cs#L1)

Pontos de estado e visibilidade de devices:

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [IDeviceServerClient](../../../src/Device.Client.Abstractions/IDeviceServerClient.cs#L1)
- [EmbeddedDeviceServerClient](../../../src/Device.Client.Embedded/EmbeddedDeviceServerClient.cs#L1)
- [IEmbeddedDeviceServerClientRuntime](../../../src/Device.Client.Embedded/IEmbeddedDeviceServerClientRuntime.cs#L1)
- [AppEmbeddedDeviceServerSettingsProvider](../../../src/App.WinUI/Services/Devices/AppEmbeddedDeviceServerSettingsProvider.cs#L1)
- [DeviceRefreshCoordinator](../../../src/App.WinUI/Services/Devices/DeviceRefreshCoordinator.cs#L1)
- [DeviceCommandDispatcher](../../../src/App.WinUI/Services/Devices/DeviceCommandDispatcher.cs#L1)
- [DeviceCommandTracker](../../../src/App.WinUI/Services/Devices/DeviceCommandTracker.cs#L1)
- [DeviceLogBook](../../../src/App.WinUI/Services/Devices/DeviceLogBook.cs#L1)
- [DeviceLogEntry](../../../src/App.WinUI/Services/Devices/DeviceLogEntry.cs#L1)
- [DeviceTelemetryHistoryBook](../../../src/App.WinUI/Services/Devices/DeviceTelemetryHistoryBook.cs#L1)
- [DeviceLifecycleThresholdProvider](../../../src/App.WinUI/Services/Devices/DeviceLifecycleThresholdProvider.cs#L1)
- [DeviceListVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DeviceListVisibilityPolicy.cs#L1)
- [DeviceListRenderDiff](../../../src/App.WinUI/Services/Devices/DeviceListRenderDiff.cs#L1)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [DeviceServerHost dashboard](../../../src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs#L1)
- [DeviceServerHost MQTT](../../../src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs#L1)
- [DeviceServerRuntimeConfig](../../../src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs#L1)
- [IDevicePairingStore](../../../src/Device.Server.Abstractions/Hosting/IDevicePairingStore.cs#L1)
- [InMemoryDevicePairingStore](../../../src/Device.Server/Hosting/InMemoryDevicePairingStore.cs#L1)
- [ICommandStateStore](../../../src/Device.Server.Abstractions/Hosting/ICommandStateStore.cs#L1)
- [TrackedCommandState](../../../src/Device.Server.Abstractions/Hosting/TrackedCommandState.cs#L1)
- [InMemoryCommandStateStore](../../../src/Device.Server/Hosting/InMemoryCommandStateStore.cs#L1)
- [ISessionStateStore](../../../src/Device.Server.Abstractions/Hosting/ISessionStateStore.cs#L1)
- [DeviceSessionState](../../../src/Device.Server.Abstractions/Hosting/DeviceSessionState.cs#L1)
- [InMemorySessionStateStore](../../../src/Device.Server/Hosting/InMemorySessionStateStore.cs#L1)
- [DeviceFrameConnection](../../../src/Device.Server/Hosting/DeviceFrameConnection.cs#L1)
- [DeviceFrameConnectionRegistry](../../../src/Device.Server/Hosting/DeviceFrameConnectionRegistry.cs#L1)
- [DeviceMqttTopics](../../../src/Device.Server/Hosting/DeviceMqttTopics.cs#L1)
- [DeviceRecordMutations](../../../src/Device.Server.Abstractions/Hosting/DeviceRecordMutations.cs#L1)

Pontos de lifecycle leve de devices:

- [DeviceLifecyclePolicy](../../../src/App.WinUI/Services/Devices/DeviceLifecyclePolicy.cs#L1)
- [DeviceLifecyclePresentation](../../../src/App.WinUI/Services/Devices/DeviceLifecyclePresentation.cs#L1)
- [DeviceLifecycleThresholds](../../../src/App.WinUI/Services/Devices/DeviceLifecycleThresholds.cs#L1)
- [DeviceRegistryPresenceNormalizer](../../../src/App.WinUI/Services/Devices/DeviceRegistryPresenceNormalizer.cs#L1)
- [migrate-device-registry-presence-v1.ps1](../../../scripts/migrate-device-registry-presence-v1.ps1#L1)

Observacoes ativas:

- A DevicesPage usa diff incremental para manter a lista estavel e evitar rebuild total em refresh normal.
- A DevicesPage diferencia App ativo (online) de Ultimo app conhecido (offline), com acoes no card de resumo (`Testar LED` e `Reprovisionar Wi-Fi`).
- `Reprovisionar Wi-Fi` envia `enter_provisioning`; `RemoveDevice` fica como limpeza tecnica/admin fora da acao principal da UX.
- A DevicesPage usa apenas miniatura inline da lista para preview de app; o painel da direita nao tem preview maior.
- A DevicesPage agora usa `WebView2` no painel direito e carrega o dashboard HTML local em `/dashboard`, mantendo a lista de devices e as acoes globais de firmware/pairing no shell WinUI nativo.
- O dashboard HTML recebe selecao via `postMessage`, consome `WS /ws/device/{deviceId}` e preserva no host WinUI as acoes reais de brilho, teste de LED e remocao.
- `Configuracoes` agora permanece restrita a `Geral`, preferencia de Mica e logs de erro; diagnostico serial bruto fica em ferramentas externas.
- O DeviceServerHost aplica grace curto de detach WS (500ms) e detach por identidade de socket para reduzir flapping em reconexao rapida.
- O online/offline oficial da UI agora vem do control plane MQTT; WS isolado nao basta mais para marcar device online.
- O snapshot tambem diferencia `LegacyOnly` para firmware que ainda usa WS-texto/HTTP no control plane.
- O hot path visual remoto para `Bins128` agora e `WinUI Remote -> /api/v1/admin/visual-endpoints -> UDP direto no ESP`; `Frame128x64`, paineis/GIFs e endpoints ausentes continuam no fallback `/ws/v1/admin/frames`.
- O hot path visual embedded continua em `Esp32S3LedOutput -> IDeviceFrameTransport -> DeviceServerHost.BroadcastFrame/SendFrame`; por default sai por `/ws/v1/stream`, com UDP server->ESP apenas quando o device anuncia capability e `PreferLanUdpVisualTransport=true`.
- Os contratos consumidos por clients (`IDeviceServerClient`, `IDeviceFrameTransport`, `PanelsBatchRegistration`) vivem em `Device.Client.Abstractions`; `Device.Server.Abstractions` fica restrito ao host embedded/lifecycle.
- A implementacao local desses contratos vive em `Device.Client.Embedded`; o WinUI registra `EmbeddedDeviceServerClient` como `IDeviceServerClient` e `IEmbeddedDeviceServerClientRuntime`, mantendo `DeviceServerHost` apenas no composition root.
- O storage efemero de batches `WebP` vive atras de `IPanelsBatchStore`; o runtime embedded registra `InMemoryPanelsBatchStore` como default, preservando payload em memoria e limite de `4` batches por device.
- O estado efemero de pairing vive atras de `IDevicePairingStore`; o runtime embedded registra `InMemoryDevicePairingStore`, preservando pair codes de uso unico, TTL e tentativas por IP.
- O estado efemero de comandos tracked vive atras de `ICommandStateStore`; o runtime embedded registra `InMemoryCommandStateStore`, preservando progresso, timeout e resultado final em memoria.
- O estado efemero de sessoes vive atras de `ISessionStateStore`; `DeviceSessionState` guarda apenas presenca/snapshot/records, enquanto `DeviceFrameConnectionRegistry` mantem WebSocket e fila de frames como detalhe process-local do `Device.Server`.
- O host local agora tambem expõe `GET /dashboard` e `WS /ws/device/{deviceId}` com DTO dedicado para o WebView.

- `MicaAudio.Server` agora fornece o primeiro host standalone/Docker para smoke local e Render, reaproveitando `DeviceServerHost` sem transformar ainda o WinUI em client remoto.
- O standalone/Docker agora separa bind interno de endereco anunciado: `MICA_SERVER__PUBLICHTTPBASEADDRESS` preserva a porta publica HTTP e `MICA_SERVER__PUBLICHOST` anuncia o host MQTT local/legado.
- Docker local usa `scripts/docker-server-redeploy.ps1` como caminho oficial para rebuild/redeploy, com visual por WS como default, `5274/udp` apenas com `-PreferVisualUdp` e `5275/udp` para discovery LAN; Render/cloud permanecem em HTTP/WSS.
- O server standalone persiste `devices.json`, `panels/panels.json` e blobs/index de midia em `MICA_SERVER__STORAGEROOT`.
- O WinUI agora tambem possui modo `Remote`, opt-in em `Configuracoes`, que usa `Device.Client.Remote` contra `MicaAudio.Server` com token admin DPAPI, botao `Testar servidor remoto` e descoberta de endpoints visuais LAN; `Embedded` permanece default.

Pontos centrais do pipeline de analise e captura:

- [SpectrumAnalyzer](../../../src/Analyzer.Dsp/Analysis/SpectrumAnalyzer.cs#L1)
- [SpectrumSampleWindow](../../../src/Analyzer.Dsp/Analysis/SpectrumSampleWindow.cs#L1)
- [SpectrumPowerProcessor](../../../src/Analyzer.Dsp/Analysis/SpectrumPowerProcessor.cs#L1)
- [SpectrumBandLayout](../../../src/Analyzer.Dsp/Analysis/SpectrumBandLayout.cs#L1)
- [BandAggregationRange](../../../src/Analyzer.Dsp/Analysis/BandAggregationRange.cs#L1)
- [ComplexFftPlan](../../../src/Analyzer.Dsp/Math/ComplexFftPlan.cs#L1)
- [RealFftFloatPlan](../../../src/Analyzer.Dsp/Math/RealFftFloatPlan.cs#L1)
- [SpectrumAnalyzerProcessBenchmark](../../../BenchmarkSuite1/SpectrumAnalyzerProcessBenchmark.cs#L1)
- [WasapiLoopbackCaptureService](../../../src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs#L1)
- [LoopbackCaptureRuntimeConfig](../../../src/Audio.Loopback/Capture/LoopbackCaptureRuntimeConfig.cs#L1)
- [LoopbackFrameFactory](../../../src/Audio.Loopback/Capture/LoopbackFrameFactory.cs#L1)

Observacoes ativas do pipeline:

- `SpectrumAnalyzer` preserva o contrato publico, mas delega janela, FFT/power/weighting e layout de bandas para colaboradores internos testaveis.
- `Analyzer.Dsp` agora reaproveita buffers por instancia, usa `SpectrumSampleWindow` circular, agrega bandas com pesos precomputados e expõe `AnalyzerOutputMode` de forma aditiva no runtime.
- `WasapiLoopbackCaptureService` continua sendo a fronteira publica de captura, mas a normalizacao de runtime e a criacao de `PcmFrame` agora estao isoladas para facilitar testes deterministas.

Pontos centrais de runtime do visualizer e payload:

- [VisualizerRuntimeSettings](../../../src/MicaAudio.Core/Config/VisualizerRuntimeSettings.cs#L1)
- [AnalyzerRuntimeProfile](../../../src/MicaAudio.Core/Config/AnalyzerRuntimeProfile.cs#L1)
- [DeviceLifecycleSettings](../../../src/MicaAudio.Core/Config/DeviceLifecycleSettings.cs#L1)
- [AppSettings](../../../src/MicaAudio.Core/Presets/AppSettings.cs#L1)
- [LedPayloadFactory](../../../src/MicaAudio.Core/Led/LedPayloadFactory.cs#L1)
- [VisualizerAnalyzerConfigFactory](../../../src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs#L1)
- [AppSettingsDomainService](../../../src/App.WinUI/Services/AppSettingsDomainService.cs#L1)

Pontos centrais do runtime de pipeline no app:

- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L1)
- [AudioPipelineFrameProcessor](../../../src/App.WinUI/Services/AudioPipelineFrameProcessor.cs#L1)
- [AudioPipelineOutputRouter](../../../src/App.WinUI/Services/AudioPipelineOutputRouter.cs#L1)
- [AudioPipelineCaptureProfile](../../../src/App.WinUI/Services/AudioPipelineCaptureProfile.cs#L1)
- [MainPage Pipeline helpers](../../../src/App.WinUI/Views/MainPage.Pipeline.cs#L1)

Observacoes ativas do runtime do app:

- `VisualizerRuntimeSettings` e `AnalyzerRuntimeProfile` passaram a ser a fonte unica de defaults/clamp do visualizer no `.NET 10`.
- `VisualizerAnalyzerConfigFactory` e `AnalyzerRuntimeProfile` agora carregam `AnalyzerOutputMode`, mantendo `DisplayAndOutput` como default do modo interativo e permitindo `OutputOnly` sem mudar o wire.
- `LedPayloadFactory` centraliza o remapeamento para `128` bins e evita montagem manual repetida de `LedPayload`.
- `AudioPipelineCoordinator` atua como orquestrador fino; ciclo de vida, roteamento e frame processing estao separados em colaboradores internos.
- `ShellPage` resolve abas de forma lazy e isolada; falha da `MainPage` nao derruba mais a shell inteira.
- `ShellPage` agora tambem resolve a sessao `Monitoramento`, que le sensores locais do PC via HWiNFO64 Shared Memory sem misturar com o runtime de devices.
- `AppStartupDiagnostics` e `MainPage.Startup` concentram breadcrumbs, fallback de preset legado e guard de bootstrap da UI.
- `App` aplica `MicaBackdrop` com base em `AppSettings.UseMicaBackdrop` e consegue alternar entre Mica e superficie solida em runtime sem restart.
- `SettingsPage` concentra apenas `Geral`, preferencia de Mica e acesso aos logs de erro; nao abre porta `COM` nem renderiza logs seriais.
- `AppLogStore` deixou de persistir log operacional completo em `app-logs.json` e agora grava em disco apenas entradas `Error`, no mesmo `crash.log` canonico do app.
- `MonitoringPage` usa leitura local do HWiNFO64 via `HwinfoSharedMemorySource`, com 6 cards compostos orientados a hardware e lista pesquisavel de leituras agrupadas por sensor.
- A reducao de escopo do fix de startup manteve a protecao concentrada na `MainPage`: presets sao sanitizados apenas no caminho de rebuild do analyzer.
- O `Visualizador` agora separa runtime pendente do runtime aplicado e usa debounce unico de `150 ms` para ajustes finos antes do rebuild do analyzer.
- O painel de configuracao do `Visualizador` continua lateral, mas agora usa grupos e linhas de settings inspirados em Fluent 2, com `RendererCombo` e `ContentModeCombo` movidos para a pane e `CommandBar` focado em acoes rapidas.

Pontos centrais da sessao de monitoramento local:

- [HwinfoSharedMemorySource](../../../src/App.WinUI/Services/Monitoring/HwinfoSharedMemorySource.cs#L1)
- [HwinfoSharedMemoryBinaryParser](../../../src/App.WinUI/Services/Monitoring/HwinfoSharedMemoryBinaryParser.cs#L1)
- [MonitoringSnapshotProjector](../../../src/App.WinUI/Services/Monitoring/MonitoringSnapshotProjector.cs#L1)
- [MonitoringKpiSelector](../../../src/App.WinUI/Services/Monitoring/MonitoringKpiSelector.cs#L1)
- [Monitoring contracts](../../../src/App.WinUI/Services/Monitoring/MonitoringContracts.cs#L1)
- [Monitoring hardware resolver](../../../src/App.WinUI/Services/Monitoring/MonitoringHardwareResolver.cs#L1)
- [Windows memory fallback](../../../src/App.WinUI/Services/Monitoring/WindowsMemoryFallbackProvider.cs#L1)
- [Monitoring text normalization](../../../src/App.WinUI/Services/Monitoring/MonitoringTextNormalization.cs#L1)

Observacoes ativas do monitoramento:

- O v1 usa `Global\\HWiNFO_SENS_SM2` + `Global\\HWiNFO_SM2_MUTEX` e faz parsing manual do buffer para evitar `unsafe` no hot path.
- A sessao `Monitoramento` trabalha com snapshot atual apenas: sem historico, sem editor de widgets e sem persistencia de layout.
- O topo do dashboard deriva 6 cards fixos a partir das leituras recebidas: `Uso total`, `Temperatura geral`, `Memoria RAM`, `VRAM GPU`, `Consumo` e `Frequencia`.
- Os cards tentam preservar o nome real do hardware detectado pelo HWiNFO64, como modelo de CPU e GPU, e derivam `Disponivel` quando so existem leituras de `Used + Total`.
- O matching de memoria agora aceita labels localizados do HWiNFO (`Memoria fisica utilizada/disponivel`, `Memoria GPU alocada/disponivel`) e usa fallback local do Windows apenas para `RAM/VRAM` quando a heuristica nao resolve.

Pontos centrais de catalogo compartilhado e widgets:

- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L1)
- [IAppCatalogService](../../../src/App.WinUI/Services/Apps/IAppCatalogService.cs#L1)
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L1)
- [IAppModifierStateStore](../../../src/App.WinUI/Services/Apps/IAppModifierStateStore.cs#L1)
- [AppCatalogCardControl](../../../src/App.WinUI/Views/Controls/AppCatalogCardControl.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)

Pontos centrais da sessao de paineis HUB75:

- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsPage UI](../../../src/App.WinUI/Views/PanelsPage.Ui.cs#L1)
- [PanelsPageViewModel](../../../src/App.WinUI/ViewModels/PanelsPageViewModel.cs#L1)
- [Hub75PanelThumbnailControl](../../../src/App.WinUI/Views/Controls/Hub75PanelThumbnailControl.cs#L1)
- [PanelGalleryCardControl](../../../src/App.WinUI/Views/Controls/PanelGalleryCardControl.cs#L1)
- [Hub75PanelEditorControl](../../../src/App.WinUI/Views/Controls/Hub75PanelEditorControl.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)
- [PanelsStore](../../../src/App.WinUI/Services/Panels/PanelsStore.cs#L1)
- [PanelsStoreDocument](../../../src/App.WinUI/Models/Panels/PanelsStoreDocument.cs#L1)
- [PanelDefinition](../../../src/App.WinUI/Models/Panels/PanelDefinition.cs#L1)
- [PanelWidgetDefinition](../../../src/App.WinUI/Models/Panels/PanelWidgetDefinition.cs#L1)
- [PanelsFrameComposer](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)
- [PanelsMediaCache](../../../src/App.WinUI/Services/Panels/PanelsMediaCache.cs#L1)
- [PanelsAnimatedWebpEncoder](../../../src/App.WinUI/Services/Panels/PanelsAnimatedWebpEncoder.cs#L1)
- [PanelsMatrixDrawHelpers](../../../src/App.WinUI/Services/Panels/PanelsMatrixDrawHelpers.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [PanelsDeviceSessionService](../../../src/App.WinUI/Services/Devices/PanelsDeviceSessionService.cs#L1)
- [PanelsBatchCommandPayload](../../../src/Device.Protocol/Models/PanelsBatchCommandPayload.cs#L1)
- [PanelsBatchRegistration](../../../src/Device.Client.Abstractions/PanelsBatchRegistration.cs#L1)
- [IPanelsBatchStore](../../../src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs#L1)
- [PanelsBatchWrite](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchWrite.cs#L1)
- [PanelsBatchEntry](../../../src/Device.Server.Abstractions/Hosting/PanelsBatchEntry.cs#L1)
- [InMemoryPanelsBatchStore](../../../src/Device.Server/Hosting/InMemoryPanelsBatchStore.cs#L1)
- [PanelLibraryDocument](../../../src/Device.Protocol/Models/PanelLibraryDocument.cs#L1)
- [PanelLibraryItem](../../../src/Device.Protocol/Models/PanelLibraryItem.cs#L1)
- [PanelWidgetItem](../../../src/Device.Protocol/Models/PanelWidgetItem.cs#L1)
- [MediaAssetInfo](../../../src/Device.Protocol/Models/MediaAssetInfo.cs#L1)
- [DeviceServerHost.PanelsBatches](../../../src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs#L1)

Observacoes ativas dos paineis:

- O V1 de `Paineis` continua client-composed no runtime: o ESP32 recebe frame/batch final, enquanto o server vira fonte unica de biblioteca e midias.
- A sessao agora abre em galeria de cards com miniaturas HUB75 `128x64`, toggle `Ativo` por card e editor dedicado dentro da mesma `PanelsPage`.
- O editor trabalha com um unico framebuffer `128x64` e sobreposicao por `ZIndex`; a biblioteca lateral e o ponto unico de descoberta/configuracao de widgets.
- A biblioteca de `Paineis` usa busca + cards do catalogo compartilhado, reaproveita drafts `__local__|appId` como defaults de widget e desabilita itens ainda sem renderer HUB75.
- Os widgets atuais do compositor sao `analogclock` e `gifhub75`.
- A galeria de `Paineis` agora e `static first`: abre com posters lazy, sem compor todos os cards no `Loaded` e sem preview animado local por default.
- O editor entra com preview desligado; a animacao local so e criada quando o usuario ativa o toggle `Preview`.
- `PanelsFrameComposer.CreatePosterAsync(...)` e `PanelsMediaCache` separam poster de playback e reutilizam decodificacao de midia para evitar churn de RAM/CPU.
- `PanelsStore` recupera `panels.json` vazio/corrompido sem derrubar a app, grava com temp+replace e migra automaticamente o cache local para a biblioteca do server quando o server esta vazio.
- O runtime de painel em background usa `30 FPS` como teto de apresentacao, salva `lastSelectedPanelId` e separa estado de widget (`ConfigValues`) do draft local compartilhado de apps.
- `gifhub75` agora resolve animacao por delays reais do arquivo e o cache animado guarda sequencia temporal (`frames + durationMs + totalDurationMs`).
- O transporte HUB75 agora suporta `SendFrame(deviceId, payload)` em paralelo ao broadcast, e `Esp32S3LedOutput` escolhe o destino a partir de `LedOutputConfig.TargetDeviceId`.
- Se o device anunciar `animatedWebpBatchSupported`, `PanelsPlaybackService` passa a gerar lotes `WebP` animados de `1 s / 30 frames`, publicados via `queue_panels_batch` e baixados do `Device.Server` por HTTP autenticado.
- `DeviceServerHost.PanelsBatches` agora delega persistencia efemera a `IPanelsBatchStore`; a implementacao default `InMemoryPanelsBatchStore` mantem um `panelsSessionId` ativo por device, calcula `SHA-256` no registro e retem os `4` batches mais recentes por device.
- O fallback para `Frame128x64` continuo continua automatico quando o device nao suporta batches ou quando o enfileiramento/download falha.
