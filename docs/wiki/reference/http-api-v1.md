# HTTP API v1

Referencia resumida do servidor local embutido em `DeviceServerHost`.

## Endpoints principais

- `GET /api/v1/server/info`
- `POST /api/v1/pair`
- `GET /api/v1/device/config`
- `POST /api/v1/device/command-ack`
- `GET /api/v1/health`

## Pair request (v1 estendido)

`POST /api/v1/pair` aceita, alem de `PairingCode` e `DeviceName`, os campos opcionais:

- `BoardModel` (ex.: `matrixportal_s3`, `esp32s3_devkitc1`)
- `PanelType` (ex.: `hub75_64x32`)

Regra de compatibilidade:

- payload legado sem esses campos continua valido.

## Regras de seguranca atuais

1. Rate limiting ativo:
- `POST /api/v1/pair`
- `POST /api/v1/device/command-ack`
- handshake de `/ws/v1/stream`

2. Restricao de rede por padrao:
- somente loopback + redes privadas quando `RestrictToPrivateNetworks=true`.
- allowlist CIDR opcional via `AllowedCidrs`.

3. Autenticacao de device:
- HTTP aceita `X-Device-Token` ou `Authorization: Bearer ...`.
- query string com token **nao** e aceita em endpoints HTTP.

4. Pairing:
- codigo com TTL e uso unico.
- limite adicional de tentativas por IP/janela (anti-abuso).

5. Limites de body:
- `MaxJsonBodyBytes` (default 64KB) aplicado globalmente no servidor e validado nos endpoints criticos (`/pair`, `/device/command-ack`).

## Status e erros comuns

- `401 Unauthorized`: token ausente/invalido.
- `403 Forbidden`: origem de rede fora da politica.
- `429 Too Many Requests`: limite de taxa atingido.
- `400 Bad Request`: payload invalido ou pairing code expirado.
- `413 Payload Too Large`: body acima de `MaxJsonBodyBytes`.

## Referencias de codigo

- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L47)
- [HandlePairAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L371)
- [HandleDeviceConfig](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L471)
- [HandleCommandAckAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L494)
- [PairDeviceRequest](../../../src/Device.Protocol/Models/PairDeviceRequest.cs#L1)
- [ServerConfig](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L1)
