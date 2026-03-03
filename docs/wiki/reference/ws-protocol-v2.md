# Referencia - WS Protocol v2

`StreamFrameV2` e o contrato binario ativo do stream HUB75.

## Painel canonico

- `hub75_p2_5_128x64_smd2121_scan32`
- `128x64`
- RGB565 nativo

## Estrutura StreamFrameV2

O protocolo tem dois tipos de mensagem ativos:
1. `messageType = 1` para `bins128`
2. `messageType = 2` para `frame128x64 RGB565`

## Mensagem tipo 1 - bins128

Layout:
1. `version` (1 byte) = `2`
2. `messageType` (1 byte) = `1`
3. `sequence` (4 bytes, little-endian)
4. `timestampQpc` (8 bytes, little-endian)
5. `level` (1 byte)
6. `bins128` (128 bytes)
7. `brightness` (1 byte)
8. `flags` (1 byte)

Tamanho total: `145` bytes.

## Mensagem tipo 2 - frame128x64 RGB565

Layout:
1. `version` (1 byte) = `2`
2. `messageType` (1 byte) = `2`
3. `sequence` (4 bytes, little-endian)
4. `timestampQpc` (8 bytes, little-endian)
5. `brightness` (1 byte)
6. `pixelsRgb565` (`128 * 64 * 2 = 16384` bytes)
7. `flags` (1 byte)

Tamanho total: `16400` bytes.

## Referencias

- [StreamFrameV2](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L1)
- [Firmware onWsEvent](../../../firmware/matrixportal-s3/src/main.cpp#L1)
