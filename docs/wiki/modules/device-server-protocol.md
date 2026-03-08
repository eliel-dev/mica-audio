# Modulo Device.Server + Device.Protocol

## Objetivo

Fornecer servidor HTTP/WS embutido para pareamento, comando e stream de frames para dispositivos ESP32.

## Responsabilidades

- HTTP API `/api/v1/*` para info, pair, command-ack e health.
- WebSocket `/ws/v1/stream` para comandos e telemetria/progresso.
- Sessao de comandos rastreados com timeout.
- Normalizacao interna de `ServerConfig` para limites, timeouts e CIDRs.
- Controle temporal deterministico via `TimeProvider` no pairing, snapshots e timeouts tracked.
- Encaminhamento de comandos de operacao do device (`test_led`, `set_brightness`, `install/activate/set_app_config`).
- Controle de acesso de rede e rate limiting por endpoint critico.
- Persistencia de metadados de hardware (`BoardModel`, `PanelType`) por dispositivo.
- Pass-through de telemetria de conectividade (`wifiState`, `provisioningPortalActive`, `auxLedAvailable`, `testLedAvailable`, `lastWifiEvent`).

## Fluxo de execucao

1. `DeviceServerHost.StartAsync` sobe web app local.
2. Dispositivo pareia via HTTP e recebe token.
3. `PairDeviceRequest` pode informar `BoardModel` e `PanelType`.
4. Telemetria WS atualiza `FirmwareVersion`, app ativo, RSSI e metadados de hardware.
5. App envia comandos tracked (`SendCommandTrackedAsync`).
6. `PendingTrackedCommandStore` e `PendingTrackedCommand` correlacionam ACK/progresso por `commandId`.
7. `DeviceSession` consolida invariantes de `DeviceRecord` e snapshot online/offline.
8. `BroadcastFrame` distribui stream para sockets conectados.

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
- WebSocket (`/ws/v1/stream`): aceita `X-Device-Id` + `X-Device-Token` (ou `Authorization: Bearer`).
- Query token legado em WS permanece disponivel apenas por compatibilidade quando `AllowLegacyWebSocketQueryToken=true`.
- Default de seguranca: `AllowLegacyWebSocketQueryToken=false`.

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
- Normalizacao de runtime em `DeviceServerRuntimeConfig`.
- Transicoes de estado em `DeviceRecordMutations` e `DeviceSession`.

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
- [DeviceServerHost.Routes](../../../src/Device.Server/Hosting/DeviceServerHost.Routes.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerRuntimeConfig](../../../src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs#L1) - assinatura: `internal sealed class DeviceServerRuntimeConfig`
- [DeviceSession](../../../src/Device.Server/Hosting/DeviceSession.cs#L1) - assinatura: `internal sealed class DeviceSession`
- [PendingTrackedCommand](../../../src/Device.Server/Hosting/PendingTrackedCommand.cs#L1) - assinatura: `internal sealed class PendingTrackedCommand`
- [PairDeviceRequest](../../../src/Device.Protocol/Models/PairDeviceRequest.cs#L1) - assinatura: `public sealed class PairDeviceRequest`
- [DeviceTelemetryMessage](../../../src/Device.Protocol/Models/DeviceTelemetryMessage.cs#L1) - assinatura: `public sealed class DeviceTelemetryMessage`
- [DeviceRecord](../../../src/Device.Protocol/Models/DeviceRecord.cs#L1) - assinatura: `public sealed class DeviceRecord`
- [DeviceSnapshot](../../../src/Device.Protocol/Models/DeviceSnapshot.cs#L1) - assinatura: `public sealed class DeviceSnapshot`
- [ServerConfig](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L1) - assinatura: `public sealed class ServerConfig`

## Backlinks no codigo

- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs`
- `src/Device.Server/Hosting/DeviceSession.cs`
- `src/Device.Server/Hosting/PendingTrackedCommand.cs`
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

## Atualizacao 2026-03 - Brilho + LED auxiliar + heartbeat de telemetria

- O comando wire `set_brightness` foi adicionado para controlar `brightnessCap` por dispositivo.
- O comando wire `test_led` mantem comportamento principal de pulso curto sem parametros.
- `test_led` continua aceitando parametro legado `enabled=true|false` em compatibilidade operacional.
- A telemetria agora inclui:
  - `telemetrySequence`
  - `brightnessCap`
  - `brightnessRequested`
  - `brightnessApplied`
  - `testLedEnabled`
  - `testLedDuty`
  - `testLedAvailable`
- `DeviceServerHost` faz pass-through desses campos para `DeviceRecord`/`DeviceSnapshot`, preservando compatibilidade com firmware legado (campos `nullable`).

## Atualizacao 2026-03 - Mitigacao de flapping de sessao WS

- O detach de socket agora e seguro por identidade da conexao: somente o socket atualmente anexado pode transicionar a sessao para desconectada.
- Foi adicionado grace period curto de 500ms apos detach para absorver reconexoes rapidas sem alternancia visual online/offline na UI.
- O objetivo e reduzir falso flapping quando o firmware reconecta em janela curta.

## Atualizacao 2026-03 - RSK-002 cutover de auth WS

- O fallback de query token legado no WS foi mantido apenas como mecanismo de rollback, mas desligado por default.
- O host carrega `AllowLegacyWebSocketQueryToken` via `settings.json` do app.
- Em incidente de campo, o rollback pode reativar temporariamente o legado sem recompilar:
  - `%AppData%\\MicaAudio\\settings.json`
  - `"AllowLegacyWebSocketQueryToken": true`

## Atualizacao 2026-03 - Hotfix P0 de conectividade (Wi-Fi/AP)

- O protocolo de telemetria manteve compatibilidade e recebeu 5 campos opcionais:
  - `wifiState`
  - `provisioningPortalActive`
  - `auxLedAvailable`
  - `testLedAvailable`
  - `lastWifiEvent`
- `DeviceServerHost` faz pass-through desses campos para `DeviceRecord` e `DeviceSnapshot` sem normalizacao destrutiva.
- `test_led` preserva compatibilidade legado, mas pode responder erro operacional explicito quando nenhum LED de teste esta disponivel no hardware:
  - `errorCode = "test_led_unavailable"`

## Atualizacao 2026-03 - Onboarding USB sem mudanca no wire WS/HTTP

- O onboarding de novo dispositivo passou a ter etapa USB (`mica.serial.v1`) entre app e firmware.
- Nao houve mudanca nos contratos publicos WS/HTTP do servidor:
  - `/api/v1/pair` permanece o endpoint de pareamento;
  - `/ws/v1/stream` permanece para sessao e telemetria.
- O serial onboarding apenas automatiza o preenchimento de host/credenciais/pair code antes da sessao WS.

## Atualizacao 2026-03 - Refactor core-first do host em .NET 10

- `DeviceServerHost` foi reduzido para orquestracao do host ASP.NET Core e passou a mapear endpoints em route groups via `DeviceServerHost.Routes`.
- O estado interno foi separado em colaboradores dedicados:
  - `DeviceSessionRegistry`
  - `DevicePairingState`
  - `PendingTrackedCommandStore`
  - `DeviceRecordMutations`
- A logica temporal sensivel agora usa `TimeProvider` em:
  - expiracao de pairing code;
  - janela de tentativas por IP;
  - grace period de detach;
  - snapshots online/offline;
  - espera de comandos tracked fora do caminho `TimeProvider.System`.
- O wire HTTP/WS permaneceu congelado:
  - mesmos paths;
  - mesmos DTOs;
  - mesmos comandos wire.

