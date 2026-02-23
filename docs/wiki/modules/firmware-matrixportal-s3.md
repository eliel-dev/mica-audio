# Modulo Firmware Matrix Portal S3

## Objetivo

Firmware do dispositivo para conectar ao servidor local, receber stream `bins64` (tipo `1`) ou frame RGB565 `64x32` (tipo `2`) e executar comandos remotos.

## Responsabilidades

- Provisionamento Wi-Fi e host do servidor.
- Conexao WebSocket para stream/comandos.
- ACK/progresso de comandos.
- Telemetria periodica (RSSI, IP, versao, app ativo).
- Render de barras espelhadas (tipo `1`) e render de frame completo RGB565 (tipo `2`) em painel HUB75 quando disponivel.

## Fluxo de execucao

1. Boot carrega preferencias em NVS.
2. Conecta Wi-Fi e WebSocket.
3. Recebe `StreamFrameV1`, valida cabecalho (`version` e `messageType`) e atualiza `gBins` (tipo `1`) ou `gFrameRgb565` (tipo `2`).
4. Renderiza no HUB75: `drawBars` para tipo `1` e `drawFrame64x32` para tipo `2`.
5. Envia telemetria periodica e ACK de comando.
6. Quando o app desktop para o runtime GIF, um frame legado tipo `1` zerado desativa `frame-mode` imediatamente.

## Perfis de build

- `matrixportal_s3_stable` (dev)
- `matrixportal_s3_dma_exp` (dev)
- `matrixportal_s3_stable_release` (hardening minimo release)
- `matrixportal_s3_dma_exp_release` (hardening minimo release)

## Hardening minimo (dev vs release)

- Perfil `dev`: logs serial detalhados para diagnostico.
- Perfil `release`: reduz exposicao de detalhes de runtime e usa flags de seguranca separadas no build.
- Fluxo de stream ignora payload binario invalido sem travar loop principal.

## Pontos de alteracao frequente

- Parser de comando WS (`onWsEvent`).
- Telemetria e ACK.
- Render da matriz e limites de brilho (`initMatrixDisplay` e `drawBars`).
- Perfis de build em `platformio.ini`.

## Riscos e efeitos colaterais

- Mudar formato de frame sem atualizar app quebra stream.
- Perfil DMA pode impactar estabilidade de Wi-Fi dependendo do hardware.
- Alterar provisionamento sem fallback pode prender device offline.
- Firmware antigo ignora `messageType=2`; app deve manter preview local e aviso de compatibilidade.
- Se o app nao enviar fallback tipo `1` ao stop do GIF, o ultimo frame pode parecer "preso" ate timeout.

## Checklist apos alteracao

- Flash manual de firmware.
- Pareamento com app.
- Comando de teste LED.
- Stream de audio em tempo real.
- Validacao de reconexao apos queda de WS.

## Referencias de codigo

- [main.cpp kStreamFrameSize](../../../firmware/matrixportal-s3/src/main.cpp#L1) - assinatura: `constexpr size_t kStreamFrameSize = 81;`
- [initMatrixDisplay](../../../firmware/matrixportal-s3/src/main.cpp#L1) - assinatura: `bool initMatrixDisplay()`
- [onWsEvent](../../../firmware/matrixportal-s3/src/main.cpp#L1) - assinatura: `void onWsEvent(WStype_t type, uint8_t *payload, size_t len)`
- [drawBars](../../../firmware/matrixportal-s3/src/main.cpp#L1) - assinatura: `void drawBars()`
- [platformio.ini stable](../../../firmware/matrixportal-s3/platformio.ini#L1) - assinatura: `[env:matrixportal_s3_stable]`
- [platformio.ini stable_release](../../../firmware/matrixportal-s3/platformio.ini#L1) - assinatura: `[env:matrixportal_s3_stable_release]`
- [platformio.ini dma_exp_release](../../../firmware/matrixportal-s3/platformio.ini#L1) - assinatura: `[env:matrixportal_s3_dma_exp_release]`

## Backlinks no codigo

- `firmware/matrixportal-s3/src/main.cpp`
