# Referencia - Code Index

Pontos principais do cutover HUB75 128x64:

- [LedDefaults](../../../src/MicaAudio.Core/Led/LedDefaults.cs#L1)
- [LedPayload](../../../src/MicaAudio.Core/Led/LedPayload.cs#L1)
- [StreamFrameV2](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L1)
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
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [AppStartupDiagnostics](../../../src/App.WinUI/Infrastructure/AppStartupDiagnostics.cs#L1)
- [AppLogStore](../../../src/App.WinUI/Services/Logging/AppLogStore.cs#L1)
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L1)
- [DeviceServerHost MQTT](../../../src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs#L1)
- [DeviceMqttTopics](../../../src/Device.Server/Hosting/DeviceMqttTopics.cs#L1)
- [PairDeviceResponse](../../../src/Device.Protocol/Models/PairDeviceResponse.cs#L1)
- [ServerInfoResponse](../../../src/Device.Protocol/Models/ServerInfoResponse.cs#L1)
- [DevicePresenceMessage](../../../src/Device.Protocol/Models/DevicePresenceMessage.cs#L1)
- [DeviceControlPlaneState](../../../src/Device.Protocol/Models/DeviceControlPlaneState.cs#L1)

Notas ativas:

- Firmware oficial unico: `dma_exp` (DevKitC-1 128x64).
- O portal AP do firmware voltou a expor `Servidor` editavel; aceita URL completa ou `host[:porta]` e preserva host salvo valido em erro manual.

Pontos de UI para operacao de devices:

- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage onboarding](../../../src/App.WinUI/Views/DevicesPage.Onboarding.cs#L1)
- [DevicesPage list state](../../../src/App.WinUI/Views/DevicesPage.ListState.cs#L1)
- [DevicesPage preview pump](../../../src/App.WinUI/Views/DevicesPage.PreviewPump.cs#L1)
- [DevicesPage dashboard](../../../src/App.WinUI/Views/DevicesPage.Dashboard.cs#L1)
- [DevicesPage selection](../../../src/App.WinUI/Views/DevicesPage.Selection.cs#L1)
- [DevicesPage UI programatica](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DeviceListRowControl](../../../src/App.WinUI/Views/Controls/DeviceListRowControl.cs#L1)
- [DevicePreviewResolver](../../../src/App.WinUI/Services/Devices/DevicePreviewResolver.cs#L1)
- [DevicePreviewVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DevicePreviewVisibilityPolicy.cs#L1)
- [DeviceMetricsFormatter](../../../src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs#L1)
- [DeviceMetricsPresentation](../../../src/App.WinUI/Services/Devices/DeviceMetricsPresentation.cs#L1)

Pontos de estado e visibilidade de devices:

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [DeviceRefreshCoordinator](../../../src/App.WinUI/Services/Devices/DeviceRefreshCoordinator.cs#L1)
- [DeviceCommandDispatcher](../../../src/App.WinUI/Services/Devices/DeviceCommandDispatcher.cs#L1)
- [DeviceCommandTracker](../../../src/App.WinUI/Services/Devices/DeviceCommandTracker.cs#L1)
- [DeviceLogBook](../../../src/App.WinUI/Services/Devices/DeviceLogBook.cs#L1)
- [DeviceLifecycleThresholdProvider](../../../src/App.WinUI/Services/Devices/DeviceLifecycleThresholdProvider.cs#L1)
- [DeviceListVisibilityPolicy](../../../src/App.WinUI/Services/Devices/DeviceListVisibilityPolicy.cs#L1)
- [DeviceListRenderDiff](../../../src/App.WinUI/Services/Devices/DeviceListRenderDiff.cs#L1)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [DeviceServerHost MQTT](../../../src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs#L1)
- [DeviceServerRuntimeConfig](../../../src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs#L1)
- [DeviceMqttTopics](../../../src/Device.Server/Hosting/DeviceMqttTopics.cs#L1)
- [DeviceSession](../../../src/Device.Server/Hosting/DeviceSession.cs#L1)
- [PendingTrackedCommand](../../../src/Device.Server/Hosting/PendingTrackedCommand.cs#L1)

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
- A DevicesPage usa painel seguro de status + logs por device selecionado, e nao monta mais o dashboard avancado estilo ESP-Dash no caminho padrao.
- O DeviceServerHost aplica grace curto de detach WS (500ms) e detach por identidade de socket para reduzir flapping em reconexao rapida.
- O online/offline oficial da UI agora vem do control plane MQTT; WS isolado nao basta mais para marcar device online.
- O snapshot tambem diferencia `LegacyOnly` para firmware que ainda usa WS-texto/HTTP no control plane.
- O hot path visual continua em `Esp32S3LedOutput -> DeviceServerHost.BroadcastFrame -> /ws/v1/stream`.

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
- `AppStartupDiagnostics` e `MainPage.Startup` concentram breadcrumbs, fallback de preset legado e guard de bootstrap da UI.
- `App` aplica `MicaBackdrop` com base em `AppSettings.UseMicaBackdrop` e consegue alternar entre Mica e superficie solida em runtime sem restart.
- `SettingsPage` foi simplificada para uma unica superficie `Geral`, sem viewer de logs; o card `Logs de erro` apenas abre a pasta do `crash.log`.
- `AppLogStore` deixou de persistir log operacional completo em `app-logs.json` e agora grava em disco apenas entradas `Error`, no mesmo `crash.log` canonico do app.
- A reducao de escopo do fix de startup manteve a protecao concentrada na `MainPage`: presets sao sanitizados apenas no caminho de rebuild do analyzer.
- O `Visualizador` agora separa runtime pendente do runtime aplicado e usa debounce unico de `150 ms` para ajustes finos antes do rebuild do analyzer.
- O painel de configuracao do `Visualizador` continua lateral, mas agora usa grupos e linhas de settings inspirados em Fluent 2, com `RendererCombo` e `ContentModeCombo` movidos para a pane e `CommandBar` focado em acoes rapidas.

Pontos centrais de catalogo e deploy de apps:

- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L1)
- [AppsPage Catalog](../../../src/App.WinUI/Views/AppsPage.Catalog.cs#L1)
- [AppsPage RuntimePreview](../../../src/App.WinUI/Views/AppsPage.RuntimePreview.cs#L1)
- [AppsPage Modifiers](../../../src/App.WinUI/Views/AppsPage.Modifiers.cs#L1)
- [AppsPage Deployment](../../../src/App.WinUI/Views/AppsPage.Deployment.cs#L1)
- [AppCatalogCardControl](../../../src/App.WinUI/Views/Controls/AppCatalogCardControl.cs#L1)
- [AppRuntimeHost](../../../src/App.WinUI/Services/Apps/AppRuntimeHost.cs#L1)
