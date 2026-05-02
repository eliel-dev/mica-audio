# 01 - System Overview

## Objetivo

Descrever o fluxo principal do sistema e onde cada modulo participa.

## Direcao Oficial

- `WinUI` = cliente de administracao/editor remoto.
- `MicaAudio.Server` = control plane, storage, catalogo, runtime autoritativo de paineis server-capable e transporte para o device.
- `ESP32-S3` = runtime de display, conectado ao servidor por MQTT/WS/HTTP.
- Fluxo oficial: `cliente -> servidor remoto -> ESP`.
- Nao existe server embedded no WinUI, fallback embedded ou comunicacao direta cliente-ESP na direcao ativa.

## Fluxo Principal

```text
WinUI -> Admin API/WSS -> MicaAudio.Server -> MQTT/WS/HTTP -> ESP32-S3 HUB75
```

## Audio E Dados Locais

- O visualizador de audio e metricas do PC continuam dependentes do WinUI, porque a captura e os dados existem no cliente.
- Ao fechar o WinUI, esses fluxos param.
- O servidor retoma o ultimo painel server-capable ativo quando houver estado remoto habilitado.

## Paineis Autonomos

- O WinUI salva biblioteca, midias e `PanelRuntimeStateDocument` no servidor remoto.
- O `MicaAudio.Server` carrega o ultimo painel ativo no startup, assume `clientId = server-panels`, ativa `panels-hub75` no device e envia batches/frames compostos.
- Widgets server-capable incluem relogio, midia server-backed, clima/status server-backed e status de servidor/device.
- Widgets client-only sao omitidos no runtime autonomo e reportados em `PanelRuntimeStatusDocument.skippedWidgets`.

## Fluxo Por Modulo

1. `App.WinUI` resolve apenas `RemoteDeviceServerClient`, `RemoteDeviceFrameTransport` e `RemoteDeviceServerRuntime`.
2. `Device.Client.Remote` fala com `/api/v1/admin/*` e `/ws/v1/admin/*`.
3. `Device.Server` expoe Admin API, stores, MQTT/WS/HTTP de device e endpoints de batches.
4. `MicaAudio.Panels` compoe frames HUB75 sem depender de WinUI nem `System.Drawing`.
5. `MicaAudio.Server` hospeda o runtime autonomo de paineis.
6. O firmware ESP32-S3 consome comandos, heartbeat, batches e frames enviados pelo servidor.

## Referencias De Codigo

- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [ServerPanelRuntimeService](../../../src/MicaAudio.Server/ServerPanelRuntimeService.cs#L1)
- [PanelFrameComposer compartilhado](../../../src/MicaAudio.Panels/PanelFrameComposer.cs#L1)
- [PanelRuntimeStateDocument](../../../src/Device.Protocol/Models/PanelRuntimeStateDocument.cs#L1)
- [PanelRuntimeStatusDocument](../../../src/Device.Protocol/Models/PanelRuntimeStatusDocument.cs#L1)

## Backlinks No Codigo

Procure por `DOCS:` nos arquivos acima e no handoff `docs/handoffs/2026-04-30-remote-only-server-panel-runtime.md`.
