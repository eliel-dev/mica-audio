# HTTP API v1

Referencia resumida do servidor local embutido em `DeviceServerHost`.

## Endpoints principais

- `GET /api/v1/server/info`
- `POST /api/v1/pair`
- `GET /api/v1/device/config`
- `POST /api/v1/device/command-ack`
- `GET /api/v1/health`

## Regras de seguranca atuais

1. Rate limiting ativo:
- `POST /api/v1/pair`
- `POST /api/v1/device/command-ack`
- handshake de `/ws/v1/stream`

2. Restricao de rede por padrao:
- somente loopback + redes privadas quando `RestrictToPrivateNetworks=true`.
- allowlist CIDR opcional via `AllowedCidrs`.

3. Autenticacao de device:
- prioridade para `X-Device-Token` e `Authorization: Bearer ...`.
- fallback de query string (`token=`) apenas para compatibilidade legada.

4. Pairing:
- codigo com TTL e uso unico.
- limite adicional de tentativas por IP/janela (anti-abuso).

## Status e erros comuns

- `401 Unauthorized`: token ausente/invalido.
- `403 Forbidden`: origem de rede fora da politica.
- `429 Too Many Requests`: limite de taxa atingido.
- `400 Bad Request`: payload invalido ou pairing code expirado.

## Referencias de codigo

- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [HandlePairAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [HandleDeviceConfig](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [HandleCommandAckAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [ServerConfig](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L1)
