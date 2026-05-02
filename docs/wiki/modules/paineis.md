# Modulo Paineis

## Objetivo

A sessao `Paineis` permite criar layouts HUB75 `128x64`, salvar biblioteca/midias no servidor e ativar o ultimo painel configurado em um device alvo.

## Direcao Oficial

- Fluxo oficial: `WinUI -> MicaAudio.Server -> ESP32-S3`.
- O WinUI nao conversa diretamente com o ESP.
- O WinUI nao hospeda server embedded e nao envia frames ao device.
- O `MicaAudio.Server` e o runtime autoritativo de paineis server-capable.
- O ESP recebe comandos, batches `WebP` ou frames pelo caminho do servidor.

## Galeria E Editor

- A shell expoe a aba `Paineis`.
- O editor trabalha sobre paineis HUB75 `128x64`.
- O WinUI gera preview/editor local para UX, mas a apresentacao real no device pertence ao servidor.
- `Salvar` persiste biblioteca e midias remotas e, se o painel editado estiver ativo, atualiza o estado remoto para o mesmo device.

## Persistencia Do Layout

- Estado autoritativo:
  - `GET /api/v1/admin/library/panels`
  - `PUT /api/v1/admin/library/panels`
  - `POST /api/v1/admin/library/media`
  - `GET /api/v1/admin/library/media/{mediaId}`
  - `DELETE /api/v1/admin/library/media/{mediaId}`
- Estado de runtime:
  - `GET /api/v1/admin/panels/runtime`
  - `PUT /api/v1/admin/panels/runtime`
  - `GET /api/v1/admin/panels/runtime/status`
- O cache local `%APPDATA%\MicaAudio\panels\panels.json` permanece apenas como cache/migracao/offline editor.
- `PanelWidgetItem.RuntimeState` guarda estado server-safe, como `mediaId` ou `mediaIds`.
- Caminhos locais (`sourcePath`) nao sao enviados como contrato de runtime para o servidor; no save, o WinUI envia midias para a biblioteca remota e publica ids de midia.

## Compositor Compartilhado

- `MicaAudio.Panels` concentra o compositor, helpers de desenho, decoder e encoder `WebP`.
- O projeto compartilhado nao depende de WinUI nem `System.Drawing`.
- Renderers server-capable atuais:
  - `analogclock`;
  - `gifhub75` com midia resolvida por `mediaId`/`mediaIds`;
  - `weather`/`accuweather` quando houver estado/config server-backed;
  - `status`.
- Widgets client-only, como metricas do PC e visualizador de audio, sao omitidos no runtime autonomo e aparecem em `skippedWidgets`.

## Runtime Autonomo No Servidor

- `ServerPanelRuntimeService` roda dentro do `MicaAudio.Server`.
- No startup, o servidor carrega `PanelRuntimeStateDocument`.
- Se `enabled=true`, `panelId` e `targetDeviceId` forem validos, o servidor:
  - carrega a biblioteca persistida;
  - cria sessao do compositor compartilhado;
  - assume `clientId = server-panels`;
  - envia heartbeat/session context;
  - ativa `panels-hub75` no device;
  - registra e envia batches `WebP` quando suportado;
  - usa frame transport remoto como fallback tecnico do servidor quando necessario.
- Ao fechar o WinUI, o servidor continua exibindo o ultimo painel server-capable ativo.
- Se todos os widgets forem client-only ou omitidos, o servidor renderiza fallback simples e reporta `skippedWidgets`.

## Visualizador E Dados Do Cliente

- O visualizador de audio e metricas do PC continuam dependentes do WinUI.
- Quando o WinUI inicia visualizador HUB75, o painel remoto pode ser suspenso pelo cliente.
- Ao parar o visualizador ou fechar o WinUI com painel suspenso, o WinUI reabilita o estado remoto salvo para o servidor retomar.

## Referencias De Codigo

- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsStore](../../../src/App.WinUI/Services/Panels/PanelsStore.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [PanelsFrameComposer adapter WinUI](../../../src/App.WinUI/Services/Panels/PanelsFrameComposer.cs#L1)
- [PanelFrameComposer compartilhado](../../../src/MicaAudio.Panels/PanelFrameComposer.cs#L1)
- [PanelsAnimatedWebpEncoder compartilhado](../../../src/MicaAudio.Panels/PanelsAnimatedWebpEncoder.cs#L1)
- [ServerPanelRuntimeService](../../../src/MicaAudio.Server/ServerPanelRuntimeService.cs#L1)
- [ServerPanelMediaSourceResolver](../../../src/MicaAudio.Server/ServerPanelMediaSourceResolver.cs#L1)
- [StandalonePanelRuntimeStateStore](../../../src/MicaAudio.Server/StandalonePanelRuntimeStateStore.cs#L1)
- [IDeviceServerClient](../../../src/Device.Client.Abstractions/IDeviceServerClient.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [PanelLibraryDocument](../../../src/Device.Protocol/Models/PanelLibraryDocument.cs#L1)
- [PanelRuntimeStateDocument](../../../src/Device.Protocol/Models/PanelRuntimeStateDocument.cs#L1)
- [PanelRuntimeStatusDocument](../../../src/Device.Protocol/Models/PanelRuntimeStatusDocument.cs#L1)
- [PanelWidgetItem](../../../src/Device.Protocol/Models/PanelWidgetItem.cs#L1)

## Handoff

- [2026-04-30 remote-only server panel runtime](../../handoffs/2026-04-30-remote-only-server-panel-runtime.md)
