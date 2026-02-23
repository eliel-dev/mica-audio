# Modulo Device.Server + Device.Protocol

## Objetivo

Fornecer servidor HTTP/WS embutido para pareamento, comando e stream de frames para dispositivos ESP32.

## Responsabilidades

- HTTP API `/api/v1/*` para info, pair, command-ack e health.
- WebSocket `/ws/v1/stream` para comandos e telemetria/progresso.
- Sessao de comandos rastreados com timeout.
- Controle de acesso de rede e rate limiting por endpoint critico.

## Fluxo de execucao

1. `DeviceServerHost.StartAsync` sobe web app local.
2. Dispositivo pareia via HTTP e recebe token.
3. App envia comandos tracked (`SendCommandTrackedAsync`).
4. Advanced host correlaciona ACK/progresso por `commandId`.
5. `BroadcastFrame` distribui stream para sockets conectados.
6. Headers de resposta defensivos (`nosniff`, `DENY`, `no-referrer`, `no-store`) sao aplicados globalmente.

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
- HTTP (`/api/v1/*`): aceita somente `X-Device-Token` ou `Authorization: Bearer`.;
- WebSocket (`/ws/v1/stream`): aceita headers e, temporariamente, query token legado quando `AllowLegacyWebSocketQueryToken=true`.

4. Anti-abuso de pareamento:
- contador por IP/janela com resposta `429 pairing_rate_limited`.

5. Limites de payload:
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

- Qualquer mudanca no wire protocol exige compatibilidade com firmware.
- Timeout curto demais gera falso offline.
- Mudanca de token/session pode invalidar devices em campo.
- Filtro de rede/CIDR mal configurado pode bloquear dispositivos legitimos.

## Checklist apos alteracao

- Subir app e validar `/api/v1/health`.
- Validar pareamento normal e tentativa em burst (429 esperado no abuso).
- Parear device e enviar comando simples.
- Validar logs de progresso e timeout.
- Confirmar que stream continua estavel.

## Referencias de codigo

- [IDeviceServerHost](../../../src/Device.Server/Hosting/IDeviceServerHost.cs#L1) - assinatura: `public interface IDeviceServerHost`
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1) - assinatura: `Task StartAsync(ServerConfig, CancellationToken)`
- [DeviceServerHost.SendCommandTrackedAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1) - assinatura: `Task<CommandDispatchResult> SendCommandTrackedAsync(...)`
- [SendTrackedCommandCoreAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L1) - assinatura: `Task<CommandDispatchResult> SendTrackedCommandCoreAsync(...)`
- [ServerConfig](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L1) - assinatura: `public sealed class ServerConfig`
- [DeviceCommandRequest](../../../src/Device.Protocol/Models/DeviceCommandRequest.cs#L1) - assinatura: `public sealed class DeviceCommandRequest`
- [DeviceCommandProgressMessage](../../../src/Device.Protocol/Models/DeviceCommandProgressMessage.cs#L1) - assinatura: `public sealed class DeviceCommandProgressMessage`
- [StreamFrameV1](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L1) - assinatura: `public static class StreamFrameV1`

## Backlinks no codigo

- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Protocol/Contracts/ServerConfig.cs`


