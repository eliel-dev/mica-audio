# Modulo Firmware HUB75 (Matrix Portal S3 + ESP32-S3 DevKitC-1)

## Objetivo

Firmware do dispositivo para conectar ao servidor local, receber stream `bins64` (tipo `1`) ou frame RGB565 `64x32` (tipo `2`) e executar comandos remotos.

## Responsabilidades

- Provisionamento Wi-Fi e host do servidor.
- Conexao WebSocket para stream/comandos.
- ACK/progresso de comandos.
- Telemetria periodica (RSSI, IP, versao, app ativo, board e painel).
- Render de barras espelhadas (tipo `1`) e render de frame completo RGB565 (tipo `2`) em painel HUB75.

## Fluxo de execucao

1. Boot carrega preferencias em NVS.
2. Conecta Wi-Fi e WebSocket.
3. Recebe `StreamFrameV1`, valida cabecalho (`version` e `messageType`) e atualiza `gBins` (tipo `1`) ou `gFrameRgb565` (tipo `2`).
4. Renderiza no HUB75: `drawBars` para tipo `1` e `drawFrame64x32` para tipo `2`.
5. Envia telemetria periodica e ACK de comando, incluindo `boardModel` e `panelType`.
6. Quando o app desktop para o runtime GIF, um frame legado tipo `1` zerado desativa `frame-mode` imediatamente.

## Variantes de placa

- `matrixportal_s3` (pinagem Matrix Portal S3)
- `esp32s3_devkitc1` (pinagem equivalente DevKitC-1 v1.0 / WROOM-1)

Selecao em build por macro `MICA_BOARD_VARIANT_*`.

## Perfis de build

- `matrixportal_s3_stable`
- `matrixportal_s3_dma_exp`
- `esp32s3_devkitc1_stable`
- `esp32s3_devkitc1_dma_exp`
- variantes `_release` para hardening minimo de producao.

## Hardening minimo (dev vs release)

- Perfil `dev`: logs serial detalhados para diagnostico.
- Perfil `release`: reduz exposicao de detalhes de runtime e usa flags de seguranca separadas no build.
- Fluxo de stream ignora payload binario invalido sem travar loop principal.

## Pontos de alteracao frequente

- Parser de comando WS (`onWsEvent`).
- Telemetria e ACK.
- Pinmap por variante em `main.cpp`.
- Perfis de build em `platformio.ini`.

## Riscos e efeitos colaterais

- Mudar formato de frame sem atualizar app quebra stream.
- Perfil DMA pode impactar estabilidade de Wi-Fi dependendo do hardware.
- Alterar provisionamento sem fallback pode prender device offline.
- Firmware antigo ignora `messageType=2`; app deve manter preview local e aviso de compatibilidade.

## Checklist apos alteracao

- Flash manual de firmware.
- Pareamento com app.
- Comando de teste LED.
- Stream de audio em tempo real.
- Validacao de reconexao apos queda de WS.

## Referencias de codigo

- [main.cpp](../../../firmware/matrixportal-s3/src/main.cpp#L1) - assinatura: `void onWsEvent(...)`
- [platformio.ini matrixportal stable](../../../firmware/matrixportal-s3/platformio.ini#L18) - assinatura: `[env:matrixportal_s3_stable]`
- [platformio.ini devkit stable](../../../firmware/matrixportal-s3/platformio.ini#L39) - assinatura: `[env:esp32s3_devkitc1_stable]`
- [platformio.ini devkit dma](../../../firmware/matrixportal-s3/platformio.ini#L49) - assinatura: `[env:esp32s3_devkitc1_dma_exp]`

## Backlinks no codigo

- `firmware/matrixportal-s3/src/main.cpp`
- `firmware/matrixportal-s3/platformio.ini`
