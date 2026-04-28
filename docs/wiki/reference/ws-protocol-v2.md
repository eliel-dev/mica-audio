# Referencia - WS Protocol v2

`StreamFrameV2` continua sendo o contrato binario legado do stream HUB75. `StreamFrameV3` passa a ser o contrato owner-bound para o data plane direto com ownership explicito por `ownerEpoch`.

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

## Estrutura StreamFrameV3

`StreamFrameV3` preserva os mesmos `messageType`s, mas adiciona `ownerEpoch` ao cabecalho.

### Mensagem tipo 1 - bins128 owner-bound

Layout:
1. `version` (1 byte) = `3`
2. `messageType` (1 byte) = `1`
3. `sequence` (4 bytes, little-endian)
4. `ownerEpoch` (4 bytes, little-endian)
5. `timestampQpc` (8 bytes, little-endian)
6. `level` (1 byte)
7. `bins128` (128 bytes)
8. `brightness` (1 byte)
9. `flags` (1 byte)

Tamanho total: `149` bytes.

### Mensagem tipo 2 - frame128x64 RGB565 owner-bound

Layout:
1. `version` (1 byte) = `3`
2. `messageType` (1 byte) = `2`
3. `sequence` (4 bytes, little-endian)
4. `ownerEpoch` (4 bytes, little-endian)
5. `timestampQpc` (8 bytes, little-endian)
6. `brightness` (1 byte)
7. `pixelsRgb565` (`128 * 64 * 2 = 16384` bytes)
8. `flags` (1 byte)

Tamanho total: `16404` bytes.

## Politica de transicao

- `StreamFrameV2` continua aceito como baseline legado.
- Quando houver owner ativo no device, o caminho oficial de stream direto passa a exigir `StreamFrameV3` com `ownerEpoch` atual.
- `StreamFrameV2` permanece como compatibilidade enquanto nao houver owner ativo ou enquanto o caminho legado ainda estiver em uso.

## UDP Visual v1

`VisualUdpFrameV1` e um envelope LAN-only para transportar `StreamFrameV2`/`StreamFrameV3` tipo `1` (`Bins128`) sem passar pelo WebSocket do device. No modo `Remote`, o caminho oficial e WinUI -> ESP direto pela LAN usando os endpoints descobertos em `/api/v1/admin/visual-endpoints`. O caminho WS continua sendo fallback e o unico caminho para `Frame128x64 RGB565` nesta entrega.

Layout do datagrama:

1. `magic` (4 bytes ASCII) = `MICA`
2. `version` (1 byte) = `1`
3. `reserved` (1 byte) = `0`
4. `sequence` (4 bytes, little-endian)
5. `payloadLength` (2 bytes, little-endian)
6. `payload` = `StreamFrameV2` tipo `1` ou `StreamFrameV3` tipo `1`
7. `tag` = primeiros `16` bytes de `HMAC-SHA256(token, header + payload)`

Politicas travadas:

- UDP direto do WinUI so e usado quando o endpoint admin informa device online, `LanIpAddress` valido, token do device, `visualUdpPort` e `visualUdpMode = bins128`.
- UDP server->ESP continua opt-in por `PreferLanUdpVisualTransport=true`, para diagnostico local; no fluxo Docker padrao ele permanece desligado.
- O firmware aceita apenas `visualUdpMode = bins128` e descarta payload desconhecido, HMAC invalido ou sequencia antiga.
- `Frame128x64 RGB565` permanece em WS/WebP batch; nao deve ser enviado como datagrama UDP bruto por risco de fragmentacao IP.
- Render/cloud continua HTTPS/WSS; UDP direto e apenas para PC WinUI e ESP na mesma LAN.

## Referencias

- [StreamFrameV2](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L1)
- [StreamFrameV3](../../../src/Device.Protocol/Stream/StreamFrameV3.cs#L1)
- [VisualUdpFrameV1](../../../src/Device.Protocol/Stream/VisualUdpFrameV1.cs#L1)
- [Bins128VisualFlags](../../../src/Device.Protocol/Stream/Bins128VisualFlags.cs#L1)
- [Firmware stream network](../../../firmware/esp32s3-devkitc1/src/mica_network.cpp#L1)
- [Firmware UDP receiver](../../../firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp#L1)
