# Modulo Device.Server + Device.Protocol

## Objetivo

Fornecer servidor HTTP/WS embutido para pareamento, comando e stream de frames para dispositivos ESP32.

## Responsabilidades

- HTTP API `/api/v1/*` para info, pair, command-ack e OTA metadata/download.
- WebSocket `/ws/v1/stream` para comandos e telemetria/progresso.
- Sessao de comandos rastreados com timeout.
- Sessao OTA com TTL e validacao de token.

## Fluxo de execucao

1. `DeviceServerHost.StartAsync` sobe web app local.
2. Dispositivo pareia via HTTP e recebe token.
3. App envia comandos tracked (`SendCommandTrackedAsync`).
4. Advanced host correlaciona ACK/progresso por `commandId`.
5. `BroadcastFrame` distribui stream para sockets conectados.

## Pontos de alteracao frequente

- Novos comandos (`DeviceCommandType` + `CommandTypeToWire`).
- Endpoint novo em `/api/v1/*`.
- Politica de timeout/comando e progresso.
- Estrutura de DTOs em `Device.Protocol/Models`.

## Riscos e efeitos colaterais

- Qualquer mudanca no wire protocol exige compatibilidade com firmware.
- Timeout curto demais gera falso offline.
- Mudanca de token/session pode invalidar devices em campo.

## Checklist apos alteracao

- Subir app e validar `/api/v1/health`.
- Parear device e enviar comando simples.
- Validar logs de progresso e timeout.
- Testar OTA metadata/download quando habilitado.

## Referencias de codigo

- [IDeviceServerHost](../../../src/Device.Server/Hosting/IDeviceServerHost.cs#L6) - assinatura: `public interface IDeviceServerHost`
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L17) - assinatura: `public sealed partial class DeviceServerHost`
- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L37) - assinatura: `Task StartAsync(ServerConfig, CancellationToken)`
- [DeviceServerHost.SendCommandTrackedAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L186) - assinatura: `Task<CommandDispatchResult> SendCommandTrackedAsync(...)`
- [SendTrackedCommandCoreAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L22) - assinatura: `Task<CommandDispatchResult> SendTrackedCommandCoreAsync(...)`
- [HandleFirmwareLatestAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L203) - assinatura: `IResult HandleFirmwareLatestAsync(HttpContext ctx)`
- [HandleFirmwareDownloadAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L244) - assinatura: `IResult HandleFirmwareDownloadAsync(HttpContext ctx)`
- [HandleOtaResultAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L289) - assinatura: `Task<IResult> HandleOtaResultAsync(HttpContext ctx)`
- [DeviceCommandRequest](../../../src/Device.Protocol/Models/DeviceCommandRequest.cs#L3) - assinatura: `public sealed class DeviceCommandRequest`
- [DeviceCommandProgressMessage](../../../src/Device.Protocol/Models/DeviceCommandProgressMessage.cs#L3) - assinatura: `public sealed class DeviceCommandProgressMessage`
- [StreamFrameV1](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L5) - assinatura: `public static class StreamFrameV1`

## Backlinks no codigo

- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
