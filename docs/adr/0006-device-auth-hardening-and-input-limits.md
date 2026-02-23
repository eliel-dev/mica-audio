# ADR 0006 - Hardening de autenticacao de device e limites de input

## Contexto

A auditoria de seguranca identificou riscos em autenticacao de token, parsing de payloads e processamento de mensagens WebSocket fragmentadas.
Tambem havia necessidade de manter compatibilidade temporaria com firmware em campo que ainda usa query token no handshake WS.

## Decisao

1. HTTP (`/api/v1/*`) passa a aceitar token somente por header (`X-Device-Token` ou `Authorization: Bearer`).
2. WebSocket aceita query token apenas em modo legado controlado por `AllowLegacyWebSocketQueryToken`.
3. O servidor aplica limite de body JSON (`MaxJsonBodyBytes`) e limite de mensagem WS (`MaxWebSocketMessageBytes`).
4. Mensagens WS de texto sao reagrupadas por frame ate `EndOfMessage`, com encerramento por `MessageTooBig` ao exceder limite.
5. Headers HTTP defensivos sao aplicados globalmente (`nosniff`, `DENY`, `no-referrer`, `no-store`).

## Consequencias

- Reduz superficie de exfiltracao de token em endpoints HTTP.
- Diminui risco de DoS por body/mensagem excessiva.
- Mantem compatibilidade transitória para firmware legado no WS.
- Exige plano de deprecacao: N (legado on), N+1 (default off), N+2 (remocao).

## Status

Aceita

## Data

2026-02-23

## Referencias

- docs/wiki/modules/device-server-protocol.md
- docs/wiki/reference/http-api-v1.md
- docs/wiki/reference/ws-protocol-v1.md
- src/Device.Server/Hosting/DeviceServerHost.cs
- src/Device.Server/Hosting/DeviceServerHost.Advanced.cs
- src/Device.Protocol/Contracts/ServerConfig.cs
