# WS Protocol v1

Referencia do canal WebSocket entre servidor e firmware.

## Endpoint

- `/ws/v1/stream`

Autenticacao preferencial via headers (`X-Device-Id` + `X-Device-Token`, ou `Authorization: Bearer`).
Compatibilidade legado: query token e aceita temporariamente apenas quando `AllowLegacyWebSocketQueryToken=true`.

## Tipos de mensagem

1. Binaria server -> device: `StreamFrameV1` tipo `1` (`bins64 + level + brightness`).
2. Binaria server -> device: `StreamFrameV1` tipo `2` (`frame 64x32 RGB565 + brightness`).
3. Texto device -> server: telemetria, progresso e ACK de comando.
4. Comandos tracked server -> device (texto): `install_app`, `activate_app`, `set_app_config`.
5. Mensagens de texto fragmentadas sao reagrupadas ate `EndOfMessage` com limite (`MaxWebSocketMessageBytes`).

## Telemetria (device -> server)

Campos relevantes no payload JSON:

- `deviceId`
- `rssi`
- `firmwareVersion`
- `ipAddress`
- `activeAppId`
- `activeAppName`
- `boardModel` (opcional)
- `panelType` (opcional)

Regra de compatibilidade:

- firmware legado sem `boardModel/panelType` continua suportado.

## Estrutura StreamFrameV1

- `version` (1 byte)
- `messageType` (1 byte)
- `sequence` (uint32 LE)
- `timestampQpc` (uint64 LE)
- `level` (byte)
- `bins64` (64 bytes)
- `brightness` (byte)
- `flags` (byte)

Tamanho total do payload: `81` bytes.

## Estrutura StreamFrameV1 RGB565

- `version` (1 byte)
- `messageType` (1 byte, valor `2`)
- `sequence` (uint32 LE)
- `timestampQpc` (uint64 LE)
- `brightness` (byte)
- `pixelsRgb565` (`64 * 32 * 2` bytes, little-endian por pixel)
- `flags` (byte)

Tamanho total do payload: `4112` bytes.

## Parametros de app config

- `set_app_config` usa `parameters.configJson` (JSON serializado no app desktop).
- `install_app` pode incluir `parameters.configJson` quando houver draft salvo.
- O firmware persiste `activeAppConfig` em `Preferences`.

## Validacao no firmware

- Frames com `version` ou `messageType` inesperados sao ignorados.
- Tamanho minimo do payload e validado antes de atualizar `gBins` (tipo `1`) ou `gFrameRgb565` (tipo `2`).
- Comando desconhecido retorna ACK de erro sem derrubar sessao.

## Referencias de codigo

- [StreamFrameV1](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L1)
- [DeviceTelemetryMessage](../../../src/Device.Protocol/Models/DeviceTelemetryMessage.cs#L1)
- [DeviceCommandRequest](../../../src/Device.Protocol/Models/DeviceCommandRequest.cs#L1)
- [DeviceCommandProgressMessage](../../../src/Device.Protocol/Models/DeviceCommandProgressMessage.cs#L1)
- [DeviceServerHost.Advanced WS handler](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L1)
- [Firmware onWsEvent](../../../firmware/matrixportal-s3/src/main.cpp#L1)
