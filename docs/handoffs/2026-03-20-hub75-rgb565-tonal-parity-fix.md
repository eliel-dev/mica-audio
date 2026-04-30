# Handoff - HUB75 RGB565 tonal parity fix

## Objetivo

Restaurar o contraste dos `Paineis` no HUB75 corrigindo o mapper `RGB565 -> BCM` do writer bulk `writeFrameRGB565()`, alinhando o caminho `Frame128x64` com a resposta tonal upstream da biblioteca DMA.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o caminho `Frame128x64` volta a apresentar pretos mais profundos e menos aspecto "lavado" no HUB75;
  - o writer bulk continua usando o `fb` correto e preserva a auditoria de `target_buffer_id`, `back_buffer_id` e `ROWS_PER_FRAME`;
  - o baseline oficial permanece `SHIFTREG`, `min_refresh_rate=60`, `clkphase=false`, `latch_blanking=2`;
  - `Bins128` permanece inalterado.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/scripts/patch_hub75_bulk_rgb565.py`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-20-hub75-rgb565-tonal-parity-fix.md`

## Decisoes tomadas

1. O defeito foi tratado como especifico do caminho `Frame128x64`:
   - o preview do app estava correto;
   - o compositor de `Paineis` nao foi alterado.
2. O patch do writer bulk deixou de usar a expansao simplificada `5/6 bit -> bitplanes` e passou a seguir a logica tonal upstream:
   - LUTs `R/B 5-bit -> luminancia 16-bit`
   - LUT `G 6-bit -> luminancia 16-bit`
   - `lumConvTab`
   - `PIXEL_COLOR_MASK_BIT(..., MASK_OFFSET)`
3. O ownership do back buffer foi preservado:
   - o writer continua capturando `target_fb = fb` antes do flip;
   - o log limitado por tempo foi mantido para diagnostico de campo.
4. O patch script foi endurecido para aceitar upgrade de libs ja patchadas em `.pio/libdeps`:
   - se `writeFrameRGB565()` ja existir, a implementacao e substituida;
   - se nao existir, ela continua sendo injetada no mesmo ponto.
5. O baseline de scan/timing nao foi reaberto nesta fase:
   - sem mudanca em `commitMatrixFrame()`
   - sem mudanca em `brightnessCap`, `clkphase`, `latch_blanking` ou `min_refresh_rate`

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
```

## Riscos e rollback

- Risco principal: o custo extra da curva tonal no writer bulk pode reduzir margem de throughput, embora o alvo atual de `Paineis` continue baixo (`12 FPS`).
- Risco secundario: se a distorcao residual vier do mapeamento BCM/bitplane e nao apenas da curva tonal, o ganho visual pode ser parcial.
- Rollback:
  - restaurar o mapper simplificado anterior no `patch_hub75_bulk_rgb565.py`;
  - rebuildar o env oficial do firmware;
  - manter o ownership/back-buffer fix intacto.

## Proximos passos

1. Validar em hardware na ordem:
   - imagem estatica
   - relogio
   - GIF
   - visualizer `Bins128`
2. Comparar visualmente contraste/saturacao entre preview local e HUB75.
3. Se ainda houver imagem "esbranquiçada", auditar o mapeamento BCM/bitplane residual do writer bulk como proximo suspeito oficial.
