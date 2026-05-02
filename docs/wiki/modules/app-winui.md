# Modulo AppWinUI

## Responsabilidades

1. atuar como cliente remoto do `MicaAudio.Server`;
2. editar presets, dispositivos e paineis;
3. salvar configuracao e biblioteca por Admin API;
4. renderizar preview/editor local quando necessario para UX;
5. executar fluxos que dependem de dados locais do PC, como visualizador de audio e metricas.

## Remote-Only

- O WinUI e 100% remote-only.
- O composition root registra apenas:
  - `RemoteDeviceServerClient`;
  - `RemoteDeviceFrameTransport`;
  - `RemoteDeviceServerRuntime`.
- O WinUI nao registra `DeviceServerHost`, stores embedded, adapter embedded, registry local de device ou resolver de public host embedded.
- Settings antigos com `deviceServerMode = Embedded` sao ignorados; a URL remota default e `http://127.0.0.1:5272`.
- O admin token remoto permanece fora do `settings.json`, salvo em `remote-server-secrets.json`.

## Fluxo De Execucao

1. `App.BuildServiceProvider()` cria o cliente remoto.
2. `StartDeviceIntegrationAsync` inicia o runtime remoto de eventos.
3. `DeviceOperationsCoordinator` consulta snapshots e envia comandos pela Admin API.
4. `Esp32S3LedOutput` usa `RemoteDeviceFrameTransport` para fluxos explicitamente dependentes do cliente.
5. `PanelsStore` salva biblioteca/midias no servidor.
6. `PanelsPlaybackService` grava `PanelRuntimeStateDocument`; ele nao compoe nem agenda frames para o device.

## Visualizador E Dados Locais

- O visualizador de audio captura/processa no cliente.
- Ao fechar o WinUI, o visualizador para.
- Se havia painel server-capable suspenso por prioridade do visualizador, o WinUI tenta reabilitar o estado remoto antes de sair.

## Paineis

- O editor WinUI pode gerar preview local via `MicaAudio.Panels`.
- A apresentacao real no ESP pertence ao `MicaAudio.Server`.
- `PanelsPlaybackService` e um controlador remoto: ativa/desativa painel no servidor e le estado remoto.
- O ultimo painel ativo/configurado continua apos fechar o WinUI porque o servidor standalone mantem o runtime.

## Configuracoes

- A tela de configuracoes expoe apenas:
  - `RemoteServerBaseAddress`;
  - admin token.
- Nao existe combo `Embedded` vs `Remote`.
- Alterar URL/token continua exigindo restart nesta etapa.

## Firmware E Setup

- `PrecompiledFirmwareService` continua sendo a fonte local do firmware oficial para download/OTA.
- O servidor remoto e o endpoint que o ESP deve usar no AP de setup.
- A documentacao ativa nao recomenda caminho direto WinUI-ESP.

## Referencias De Codigo

- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [AppSettings](../../../src/MicaAudio.Core/Presets/AppSettings.cs#L1)
- [MicaAudioOptions](../../../src/MicaAudio.Core/Config/MicaAudioOptions.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [RemoteDeviceFrameTransport](../../../src/Device.Client.Remote/RemoteDeviceFrameTransport.cs#L1)
- [RemoteDeviceServerRuntime](../../../src/Device.Client.Remote/RemoteDeviceServerRuntime.cs#L1)
- [PanelsStore](../../../src/App.WinUI/Services/Panels/PanelsStore.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [PanelsFrameComposer adapter](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)

## Handoff

- [2026-04-30 remote-only server panel runtime](../../handoffs/2026-04-30-remote-only-server-panel-runtime.md)
