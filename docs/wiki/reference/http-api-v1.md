# HTTP API v1

Referencia resumida do servidor local embutido em `DeviceServerHost`.

## Endpoints principais

- `GET /api/v1/server/info`
- `POST /api/v1/pair`
- `GET /api/v1/device/config`
- `POST /api/v1/device/command-ack`
- `GET /api/v1/health`

## Regras

- Requisicoes de device exigem token valido.
- Endpoints OTA/remotos de firmware nao fazem parte da API atual.
- Erros devem retornar status HTTP e mensagem curta.

## Referencias de codigo

- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [HandlePairAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [HandleDeviceConfig](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [HandleCommandAckAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
