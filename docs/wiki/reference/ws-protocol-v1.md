# WS Protocol v1

Referencia do canal WebSocket entre servidor e firmware.

## Endpoint

- `/ws/v1/stream?deviceId=...&token=...`

## Tipos de mensagem

1. Binaria server -> device: `StreamFrameV1` (`bins64 + level + brightness`).
2. Texto device -> server: telemetria, progresso e ACK de comando.

## Estrutura StreamFrameV1

- `version` (1 byte)
- `messageType` (1 byte)
- `sequence` (uint32 LE)
- `timestampQpc` (uint64 LE)
- `level` (byte)
- `bins64` (64 bytes)
- `brightness` (byte)
- `flags` (byte)

## Referencias de codigo

- [StreamFrameV1](../../../src/Device.Protocol/Stream/StreamFrameV1.cs#L1)
- [DeviceCommandRequest](../../../src/Device.Protocol/Models/DeviceCommandRequest.cs#L1)
- [DeviceCommandProgressMessage](../../../src/Device.Protocol/Models/DeviceCommandProgressMessage.cs#L1)
- [DeviceServerHost.Advanced WS handler](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L1)
