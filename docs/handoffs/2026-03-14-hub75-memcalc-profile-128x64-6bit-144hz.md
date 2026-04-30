# Handoff - HUB75 memcalc profile 128x64 6-bit 144 Hz

## Objetivo

Alinhar o firmware oficial do ESP32-S3 ao perfil memcalc definido para o painel HUB75 `128x64 1/32`, fixando profundidade de cor em `6 bits`, clock HUB75 em `10 MHz` e alvo de refresh em `144 Hz`, sem alterar o protocolo wire nem o pipeline desktop.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o build do firmware define `PIXEL_COLOR_DEPTH_BITS=6`;
  - `initMatrixDisplay()` sobe com `double buffer = true`, `HZ_10M`, `clkphase = false`, `min_refresh_rate = 144` e `setPixelColorDepthBits(6)`;
  - o boot serial registra o perfil memcalc esperado para o painel;
  - o restante do caminho `Frame128x64` permanece inalterado.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/platformio.ini`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-14-hub75-memcalc-profile-128x64-6bit-144hz.md`

## Decisoes tomadas

1. O perfil oficial do painel passa a ser explicitamente `6-bit / 144 Hz`, em vez de depender do default `8-bit / 60 Hz` da biblioteca.
2. O build flag `PIXEL_COLOR_DEPTH_BITS=6` foi adicionado ao `platformio.ini` e protegido no firmware com `static_assert`, evitando drift silencioso entre build e runtime.
3. O firmware passou a registrar no boot o perfil memcalc-alvo do painel (`128x64`, `rows=32`, `i2s_type=16-bit`, `RGB24 buffer`, `DMA buffer`, `transfer rate`) para diagnostico de bancada.
4. O `lsbMsbTransitionBit` continua sendo resultado calculado internamente pela biblioteca `ESP32-HUB75-MatrixPanel-DMA`; nao foi adicionada tentativa de forcamento manual.
5. `setLatBlanking(...)` nao entrou nesta entrega para manter o tuning focado no perfil memcalc pedido; qualquer ajuste de latch blanking fica para uma fase 2, apenas se o painel `ICN6124D + FM7258E` ainda exigir isso no hardware.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
```

## Riscos e rollback

- Risco principal: `min_refresh_rate = 144` e `6 bits` melhoram a relacao flicker/memoria, mas ainda nao garantem sozinhos ausencia total de ghosting em todos os paineis `ICN6124D + FM7258E`.
- Risco secundario: o perfil memcalc e um alvo operacional; a taxa real e o `lsbMsbTransitionBit` final continuam dependendo dos calculos internos da biblioteca.
- Como reverter:
  - remover `-DPIXEL_COLOR_DEPTH_BITS=6` do `platformio.ini`;
  - restaurar o `HUB75_I2S_CFG` para o comportamento anterior sem `min_refresh_rate = 144` nem `setPixelColorDepthBits(6)`;
  - remover a documentacao/handoff desta entrega.

## Proximos passos

1. Gravar o firmware no ESP32-S3 e confirmar no serial boot o perfil `6-bit / 144 Hz`.
2. Validar no painel real se houve reducao perceptivel de flicker em frames estaticos e animacoes rapidas.
3. Se ainda houver ghosting/faixas apos este tuning, abrir uma fase separada para `setLatBlanking(...)` e ajuste fino do combo `ICN6124D + FM7258E`.
