# Modulo Firmware HUB75 (DevKitC-1 128x64)

## Fluxo de execucao

1. conecta ao servidor local
2. recebe `StreamFrameV2` tipo `1` (`bins128`) ou tipo `2` (`frame 128x64 RGB565`)
3. renderiza `drawBars` ou `drawFrame128x64`
4. reporta `boardModel = esp32s3_devkitc1` e `panelType = hub75_p2_5_128x64_smd2121_scan32`

## Perfil oficial

1. O unico firmware ativo da base e `dma_exp`.
2. O antigo perfil `stable` foi removido do fluxo oficial.

## Pontos de alteracao frequente

1. `platformio.ini` para largura, altura e o unico env oficial
2. `main.cpp` para parsing do stream e desenho
3. artefato BIN embarcado no app

## Referencias de codigo

- [main.cpp](../../../firmware/matrixportal-s3/src/main.cpp#L1)
- [platformio.ini](../../../firmware/matrixportal-s3/platformio.ini#L1)
