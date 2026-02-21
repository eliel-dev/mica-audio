# Modulo Firmware Matrix Portal S3

## Objetivo

Firmware do dispositivo para conectar ao servidor local, receber stream `bins64` e executar comandos remotos.

## Responsabilidades

- Provisionamento Wi-Fi e host do servidor.
- Conexao WebSocket para stream/comandos.
- ACK/progresso de comandos.
- Telemetria periodica (RSSI, IP, versao, app ativo).

## Fluxo de execucao

1. Boot carrega preferencias em NVS.
2. Conecta Wi-Fi e WebSocket.
3. Recebe `StreamFrameV1`, atualiza `gBins/gLevel/gServerBrightness` e desenha barras espelhadas no painel HUB75.
4. Envia telemetria periodica e ACK de comando.

## Perfis de build

- `matrixportal_s3_stable`
- `matrixportal_s3_dma_exp`

## Pontos de alteracao frequente

- Parser de comando WS (`onWsEvent`).
- Telemetria e ACK.
- Render da matriz e limites de brilho (`initMatrixDisplay` e `drawBars`).

## Riscos e efeitos colaterais

- Mudar formato de frame sem atualizar app quebra stream.
- Perfil DMA pode impactar estabilidade de Wi-Fi dependendo do hardware.

## Checklist apos alteracao

- Flash manual de firmware.
- Pareamento com app.
- Comando de teste LED.
- Stream de audio em tempo real.

## Referencias de codigo

- [main.cpp kStreamFrameSize](../../../firmware/matrixportal-s3/src/main.cpp#L20) - assinatura: `constexpr size_t kStreamFrameSize = 81;`
- [initMatrixDisplay](../../../firmware/matrixportal-s3/src/main.cpp#L177) - assinatura: `bool initMatrixDisplay()`
- [onWsEvent](../../../firmware/matrixportal-s3/src/main.cpp#L415) - assinatura: `void onWsEvent(WStype_t type, uint8_t *payload, size_t len)`
- [drawBars](../../../firmware/matrixportal-s3/src/main.cpp#L593) - assinatura: `void drawBars()`
- [platformio.ini stable](../../../firmware/matrixportal-s3/platformio.ini#L1) - assinatura: `[env:matrixportal_s3_stable]`
- [platformio.ini dma_exp](../../../firmware/matrixportal-s3/platformio.ini#L1) - assinatura: `[env:matrixportal_s3_dma_exp]`

## Backlinks no codigo

- `firmware/matrixportal-s3/src/main.cpp`
