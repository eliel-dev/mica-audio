# Modulo Device.Server + Device.Protocol

## Objetivo

Fornecer servidor HTTP/WS embutido para pareamento, comando e stream de frames para dispositivos ESP32.

## Responsabilidades

- HTTP API `/api/v1/*` para info, pair, command-ack e health.
- WebSocket `/ws/v1/stream` para comandos e telemetria/progresso.
- Sessao de comandos rastreados com timeout.
- Controle de acesso de rede e rate limiting por endpoint critico.
- Persistencia de metadados de hardware (`BoardModel`, `PanelType`) por dispositivo.

## Fluxo de execucao

1. `DeviceServerHost.StartAsync` sobe web app local.
2. Dispositivo pareia via HTTP e recebe token.
3. `PairDeviceRequest` pode informar `BoardModel` e `PanelType`.
4. Telemetria WS atualiza `FirmwareVersion`, app ativo, RSSI e metadados de hardware.
5. App envia comandos tracked (`SendCommandTrackedAsync`).
6. `DeviceServerHost.Advanced` correlaciona ACK/progresso por `commandId`.
7. `BroadcastFrame` distribui stream para sockets conectados.

## Politicas de seguranca

1. Rate limiting:
- `/api/v1/pair` (janela por minuto)
- `/api/v1/device/command-ack` (janela por segundo)
- handshake de `/ws/v1/stream` (janela por minuto)

2. Rede permitida:
- loopback sempre liberado;
- por padrao apenas IP privado quando `RestrictToPrivateNetworks=true`;
- allowlist CIDR opcional em `AllowedCidrs`.

3. Autenticacao:
- HTTP (`/api/v1/*`): aceita somente `X-Device-Token` ou `Authorization: Bearer`.
- WebSocket (`/ws/v1/stream`): aceita headers e, temporariamente, query token legado quando `AllowLegacyWebSocketQueryToken=true`.

4. Limites de payload:
- body JSON limitado por `MaxJsonBodyBytes` (default 64KB).
- mensagem WS limitada por `MaxWebSocketMessageBytes` (default 64KB).
- mensagens WS fragmentadas sao reagrupadas ate `EndOfMessage`.

## Pontos de alteracao frequente

- Novos comandos (`DeviceCommandType` + `CommandTypeToWire`).
- Endpoint novo em `/api/v1/*`.
- Politica de timeout/comando e progresso.
- Estrutura de DTOs em `Device.Protocol/Models`.
- Politicas de seguranca em `ServerConfig`.

## Riscos e efeitos colaterais

- Mudanca no wire protocol exige compatibilidade com firmware.
- Timeout curto demais gera falso offline.
- Mudanca de token/session pode invalidar devices em campo.
- Filtro de rede/CIDR mal configurado pode bloquear dispositivos legitimos.

## Checklist apos alteracao

- Subir app e validar `/api/v1/health`.
- Validar pareamento com e sem `BoardModel`/`PanelType`.
- Validar telemetria atualizando metadados de hardware.
- Validar pareamento em burst (429 esperado no abuso).
- Confirmar que stream continua estavel.

## Referencias de codigo

- [IDeviceServerHost](../../../src/Device.Server/Hosting/IDeviceServerHost.cs#L1) - assinatura: `public interface IDeviceServerHost`
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.Advanced](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [PairDeviceRequest](../../../src/Device.Protocol/Models/PairDeviceRequest.cs#L1) - assinatura: `public sealed class PairDeviceRequest`
- [DeviceTelemetryMessage](../../../src/Device.Protocol/Models/DeviceTelemetryMessage.cs#L1) - assinatura: `public sealed class DeviceTelemetryMessage`
- [DeviceRecord](../../../src/Device.Protocol/Models/DeviceRecord.cs#L1) - assinatura: `public sealed class DeviceRecord`
- [DeviceSnapshot](../../../src/Device.Protocol/Models/DeviceSnapshot.cs#L1) - assinatura: `public sealed class DeviceSnapshot`
- [ServerConfig](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L1) - assinatura: `public sealed class ServerConfig`

## Backlinks no codigo

- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Protocol/Models/PairDeviceRequest.cs`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`

## Atualizacao 2026-03 - Presenca Leve e Carimbos de Sessao

- `LastAuthUtc` e preenchido quando a autenticacao WebSocket e concluida com sucesso em `HandleWebSocketAsync`, apos `TryAuthenticate(..., AuthContext.WebSocket, ...)` validar o token.
- `LastTelemetryUtc` e preenchido apenas no processamento de telemetria em `HandleIncomingWsTextAsync`.
- Esses carimbos tem semanticas diferentes e nao devem ser misturados:
  - `LastAuthUtc` = sessao autenticada estabelecida
  - `LastTelemetryUtc` = telemetria recente recebida
- A estrategia deliberadamente continua leve:
  - sem `shadow`
  - sem timeline de lifecycle
  - sem inferir `nao configurado` automaticamente

## Atualizacao 2026-03 - Telemetria v2 pass-through

- A mensagem de telemetria WS agora transporta tambem:
  - `uptimeSeconds`
  - `loopLoadPercent`
  - `freeHeapBytes`
  - `largestHeapBlockBytes`
  - `psramAvailable`
  - `freePsramBytes`
  - `largestPsramBlockBytes`
  - `wifiConnected`
- O servidor mantem comportamento pass-through para esses campos (sem clamp ou renormalizacao no host).
- Sanitizacao de `largest*BlockBytes` permanece restrita ao firmware emissor.
- Detalhes de contrato e semantica: [device-telemetry-v2-fields](../reference/device-telemetry-v2-fields.md#objetivo).

