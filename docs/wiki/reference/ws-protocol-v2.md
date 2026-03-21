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

### O que sao os 128 bytes do campo bins128

Cada byte representa a amplitude normalizada (0 = silencio, 255 = maximo) de uma faixa de frequencia do espectro de audio:

- A FFT de 2048 pontos sobre o PCM gera 1025 bins de potencia.
- `LogBandMapper` agrega esses bins em bandas logaritmicas.
- `LedPayloadFactory.ResampleSpectrumBins` remapeia interpolando as bandas para exatamente 128 valores, correspondendo coluna a coluna aos 128 pixels de largura do painel HUB75.
- Cada valor `float` (0.0-1.0) e convertido para `byte` (0-255) antes do envio.
- O indice `0` representa a frequencia mais baixa (graves) e o indice `127` a mais alta (agudos).

### Semantica oficial de `flags`

- `flags[7:3] = styleId` (`0..31`)
- `flags[2:0] = paletteFamilyId` (`0..7`)
- `flags = 0` continua valido e preserva o comportamento legado do firmware.

#### `styleId`

1. `0 = legacy-fallback`
2. `1 = wave-mirror`
3. `2 = mirror-lines`
4. `3 = mirror-blocks`
5. `4 = classic-bars`
6. `5 = flow-line`
7. `6 = history-scan`
8. `7 = radial-orbit`
9. `8 = atmosphere`
10. `9 = launchpad-grid`

#### `paletteFamilyId`

1. `0 = canonical`
2. `1 = rainbow`
3. `2 = sunset`
4. `3 = arctic`
5. `4 = neon`
6. `5 = aurora`
7. `6 = plasma`
8. `7 = mono`

### Politica operacional atual

- O host desktop resolve `presetId + rendererId -> flags` antes de serializar o pacote `Bins128`.
- O firmware HUB75 usa `styleId/paletteFamilyId` apenas no tipo `1`:
  - `Frame128x64 RGB565` ignora essa semantica.
- O objetivo do contrato e distinguir poucas familias fisicas nativas no painel, nao recriar o preview WinUI pixel a pixel.

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
- [Bins128VisualFlags](../../../src/Device.Protocol/Stream/Bins128VisualFlags.cs#L1)
- [Firmware onWsEvent](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
