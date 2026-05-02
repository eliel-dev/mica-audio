# Modulo Device.Server + Device.Protocol

## Objetivo

Fornecer o control plane HTTP/WS/MQTT do Mica, persistir estado duravel e transportar para o ESP32 os comandos, batches e frames produzidos pelo servidor remoto.

## Modulo DeviceServer DeviceProtocol

- `Device.Protocol` contem DTOs e contratos wire compartilhados.
- `Device.Server.Abstractions` contem contratos do host standalone.
- `Device.Server` implementa `DeviceServerHost`.
- `MicaAudio.Server` e o processo standalone oficial.
- `App.WinUI` nao registra `DeviceServerHost` nem stores embedded.

## Direcao Oficial

- Fluxo oficial: `cliente -> servidor remoto -> ESP`.
- O servidor remoto pode ser `http://127.0.0.1:5272`, desde que seja o processo standalone `MicaAudio.Server`.
- Nao existe modo embedded, server in-process no WinUI ou fallback de comunicacao direta cliente-ESP.
- O ESP continua conectado ao servidor por MQTT/WS/HTTP e nao renderiza widgets complexos.

## Responsabilidades

- HTTP API `/api/v1/*` para health, info, pair/compatibilidade, command-ack e endpoints de device.
- Broker MQTT embutido para comandos, eventos, status, presence, stats e logs.
- WebSocket `/ws/v1/stream` para stream visual recebido pelo device.
- Admin API por token para WinUI remoto.
- Biblioteca de paineis e midias persistida no servidor.
- Runtime autonomo de paineis no `MicaAudio.Server`.
- Download autenticado de batches `WebP`.
- Dashboard de observabilidade do device.
- Rate limiting, limites de payload e autenticacao.

## Fluxo De Execucao

1. `MicaAudio.Server` carrega configuracao e registra stores.
2. `DeviceServerHost.StartAsync` sobe HTTP, WS e MQTT.
3. O ESP autentica como device e publica presence/status/stats/logs.
4. O WinUI fala apenas com Admin API/WSS remoto.
5. Operacoes de device usam `RemoteDeviceServerClient`.
6. Frames administrativos usam `RemoteDeviceFrameTransport`.
7. Paineis server-capable usam `ServerPanelRuntimeService`.
8. O servidor envia comandos `activate_app`, `session_heartbeat`, `queue_panels_batch` e frames pelo contrato do device.

## Admin API Remota

Endpoints principais:

- `GET /api/v1/admin/devices`
- `POST /api/v1/admin/pairing-codes`
- `DELETE /api/v1/admin/devices/{deviceId}`
- `POST /api/v1/admin/devices/{deviceId}/commands/tracked`
- `GET /api/v1/admin/library/panels`
- `PUT /api/v1/admin/library/panels`
- `POST /api/v1/admin/library/media`
- `GET /api/v1/admin/library/media/{mediaId}`
- `DELETE /api/v1/admin/library/media/{mediaId}`
- `GET /api/v1/admin/panels/runtime`
- `PUT /api/v1/admin/panels/runtime`
- `GET /api/v1/admin/panels/runtime/status`
- `POST /api/v1/admin/panels/batches/{deviceId}/{panelsSessionId}/{batchSequence}`
- `DELETE /api/v1/admin/panels/batches/{deviceId}`

WebSockets admin:

- `WS /ws/v1/admin/events`
- `WS /ws/v1/admin/frames`

## Runtime Remoto De Paineis

- `PanelRuntimeStateDocument` persiste `enabled`, `panelId`, `targetDeviceId` e `updatedAtUtc`.
- `PanelRuntimeStatusDocument` expõe `running`, `panelId`, `targetDeviceId`, `skippedWidgets`, `lastError` e `lastRenderedAtUtc`.
- `IPanelRuntimeStateStore` fica em `Device.Server.Abstractions`.
- `StandalonePanelRuntimeStateStore` persiste `StorageRoot/panels/runtime-state.json`.
- `IPanelRuntimeStatusStore` guarda status em memoria.
- `ServerPanelRuntimeService` usa `clientId = server-panels` para heartbeat/session context.

## Biblioteca Server-First

- `PanelLibraryDocument` usa schema versionado.
- `PanelWidgetItem.RuntimeState` carrega apenas estado server-safe.
- Midias ficam em `IMediaLibraryStore`.
- O WinUI converte caminhos locais em `mediaId`/`mediaIds` antes de salvar para runtime remoto.

## Segurança

- Admin remoto exige `ServerConfig.AdminToken`.
- Device HTTP/WS/MQTT exige credenciais de device.
- Query token legado em WS permanece desligado por default.
- Upload de midia respeita `MaxMediaUploadBytes`.
- Mensagens WS respeitam `MaxWebSocketMessageBytes`.

## Pontos De Alteracao Frequente

- Novos DTOs em `Device.Protocol/Models`.
- Novos endpoints em `DeviceServerHost.Routes`.
- Novos handlers admin em `DeviceServerHost.Admin`.
- Stores em `Device.Server.Abstractions` e `Device.Server/Hosting`.
- Runtime standalone em `MicaAudio.Server`.

## Referencias De Codigo

- [IDeviceServerHost](../../../src/Device.Server.Abstractions/Hosting/IDeviceServerHost.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [DeviceServerHost.Admin](../../../src/Device.Server/Hosting/DeviceServerHost.Admin.cs#L1)
- [DeviceServerHost.Routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [MicaAudioServerBootstrap](../../../src/MicaAudio.Server/MicaAudioServerBootstrap.cs#L1)
- [MicaAudioServerRuntime](../../../src/MicaAudio.Server/MicaAudioServerRuntime.cs#L1)
- [ServerPanelRuntimeService](../../../src/MicaAudio.Server/ServerPanelRuntimeService.cs#L1)
- [PanelRuntimeStateDocument](../../../src/Device.Protocol/Models/PanelRuntimeStateDocument.cs#L1)
- [PanelRuntimeStatusDocument](../../../src/Device.Protocol/Models/PanelRuntimeStatusDocument.cs#L1)
- [PanelWidgetItem](../../../src/Device.Protocol/Models/PanelWidgetItem.cs#L1)
