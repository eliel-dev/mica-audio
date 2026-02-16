# HTTP API v1

Referencia resumida do servidor local embutido em `DeviceServerHost`.

## Endpoints principais

- `GET /api/v1/server/info`
- `POST /api/v1/pair`
- `GET /api/v1/device/config`
- `POST /api/v1/device/command-ack`
- `GET /api/v1/device/firmware/latest`
- `GET /api/v1/device/firmware/download`
- `POST /api/v1/device/ota/result`
- `GET /api/v1/health`

## Regras

- Requisicoes de device exigem token valido.
- OTA usa sessao de download com TTL curto.
- Erros devem retornar status HTTP e mensagem curta.

## Referencias de codigo

- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L37)
- [HandleFirmwareLatestAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L203)
- [HandleFirmwareDownloadAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L244)
- [HandleOtaResultAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L289)